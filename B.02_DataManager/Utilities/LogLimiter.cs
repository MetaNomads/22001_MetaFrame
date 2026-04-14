using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class LogLimiter : MonoBehaviour, ILogHandler
{
    public static LogLimiter Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxMessages = 300;
    [SerializeField] private bool consoleEnabled = true;

    private readonly Queue<(LogType type, string message)> _logBuffer = new();
    private ILogHandler _defaultHandler;
    private bool _isReplaying = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _defaultHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = this;
    }

    void OnDestroy()
    {
        Debug.unityLogger.logHandler = _defaultHandler;
    }

    public void EnableConsole(bool enabled) => consoleEnabled = enabled;

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        if (_isReplaying)
        {
            _defaultHandler.LogFormat(logType, context, format, args);
            return;
        }

        string message = $"[{DateTime.Now:HH:mm:ss}][{logType}] {string.Format(format, args)}";

        if (_logBuffer.Count >= maxMessages)
        {
            _logBuffer.Dequeue();
            _logBuffer.Enqueue((logType, message));

            if (consoleEnabled)
            {
                ClearConsole();
                _isReplaying = true;
                foreach (var (type, msg) in _logBuffer)
                    _defaultHandler.LogFormat(type, null, "{0}", new object[] { msg });
                _isReplaying = false;
            }
        }
        else
        {
            _logBuffer.Enqueue((logType, message));
            if (consoleEnabled)
                _defaultHandler.LogFormat(logType, context, format, args);
        }
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        if (_isReplaying) return;

        string message = $"[{DateTime.Now:HH:mm:ss}][Exception] {exception}";

        if (_logBuffer.Count >= maxMessages)
            _logBuffer.Dequeue();

        _logBuffer.Enqueue((LogType.Exception, message));

        if (consoleEnabled)
            _defaultHandler.LogException(exception, context);
    }

    private void ClearConsole()
    {
#if UNITY_EDITOR
        var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
        var clearMethod = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
        clearMethod?.Invoke(null, null);
#endif
    }

    public IEnumerable<string> GetLatestLogs()
    {
        foreach (var (_, msg) in _logBuffer)
            yield return msg;
    }
}