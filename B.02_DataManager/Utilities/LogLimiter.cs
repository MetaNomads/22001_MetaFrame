using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class LogLimiter : MonoBehaviour, ILogHandler
{
    public static LogLimiter Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxMessages    = 300;
    [SerializeField] private bool consoleEnabled = true;

    // ── History buffer (unchanged) — used by GetLatestLogs() ─────────────────
    private readonly Queue<(LogType type, string message)> _logBuffer = new();

    // ── FIX: pending queue for deferred console output ────────────────────────
    // LogFormat() used to call _defaultHandler.LogFormat() synchronously.
    // Every GSM state transition calls Debug.Log, and every GSM transition is
    // driven by a CollisionTrigger.OnEnter event inside Physics:OnSceneContact.
    // When the buffer hit maxMessages the old code called ClearConsole() then
    // replayed all 300 history entries back through _defaultHandler in one shot —
    // 300 synchronous log calls fired from inside a physics callback.
    // Even in the normal path each message was forwarded immediately.
    //
    // Fix: LogFormat() only enqueues into _pending. LateUpdate() drains it once
    // per frame at a safe point, completely outside any physics callback.
    // The history buffer (_logBuffer / GetLatestLogs) is unchanged.
    private readonly Queue<(LogType type, UnityEngine.Object context, string formatted)> _pending = new();
    private readonly object _lock = new();

    private ILogHandler _defaultHandler;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _defaultHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = this;
    }

    private void OnDestroy()
    {
        if (Debug.unityLogger.logHandler == this)
            Debug.unityLogger.logHandler = _defaultHandler;
    }

    // ── ILogHandler ───────────────────────────────────────────────────────────

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        // Format the message once so we hold no reference to the args array,
        // which may be reused or mutated by the caller after this returns.
        string formatted;
        try   { formatted = $"[{DateTime.Now:HH:mm:ss}][{logType}] {string.Format(format, args)}"; }
        catch { formatted = $"[{DateTime.Now:HH:mm:ss}][{logType}] {format}"; }

        lock (_lock)
        {
            // Update circular history buffer (same behaviour as before)
            if (_logBuffer.Count >= maxMessages)
                _logBuffer.Dequeue();
            _logBuffer.Enqueue((logType, formatted));

            // FIX: enqueue for deferred console output instead of forwarding now.
            // Previously this called _defaultHandler.LogFormat() immediately,
            // which triggered native subsystems (OVRPlugin, LSL) to flush their
            // own pending message buffers synchronously mid-physics-step.
            if (consoleEnabled)
                _pending.Enqueue((logType, context, formatted));
        }
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        // Exceptions are safety-critical — record in history but forward immediately.
        string formatted = $"[{DateTime.Now:HH:mm:ss}][Exception] {exception}";
        lock (_lock)
        {
            if (_logBuffer.Count >= maxMessages)
                _logBuffer.Dequeue();
            _logBuffer.Enqueue((LogType.Exception, formatted));
        }
        _defaultHandler?.LogException(exception, context);
    }

    // ── Deferred drain — runs once per frame at a safe point ─────────────────

    private void LateUpdate()
    {
        if (!consoleEnabled) { lock (_lock) { _pending.Clear(); } return; }

        // Snapshot under lock, process outside it
        (LogType type, UnityEngine.Object context, string formatted)[] batch;
        lock (_lock)
        {
            if (_pending.Count == 0) return;
            batch = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var (type, context, formatted) in batch)
        {
            try { _defaultHandler?.LogFormat(type, context, "{0}", new object[] { formatted }); }
            catch { /* never let logging crash the game */ }
        }
    }

    // ── Public API (unchanged) ────────────────────────────────────────────────

    public void EnableConsole(bool enabled) => consoleEnabled = enabled;

    public IEnumerable<string> GetLatestLogs()
    {
        lock (_lock)
        {
            foreach (var (_, msg) in _logBuffer)
                yield return msg;
        }
    }

    // ── Editor utility (unchanged) ────────────────────────────────────────────

    private void ClearConsole()
    {
#if UNITY_EDITOR
        var logEntries  = Type.GetType("UnityEditor.LogEntries, UnityEditor");
        var clearMethod = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
        clearMethod?.Invoke(null, null);
#endif
    }
}
