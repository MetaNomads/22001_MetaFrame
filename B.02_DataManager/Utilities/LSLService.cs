// LSLService.cs
// Place anywhere in your Assets folder — no scene setup needed.
//
// [InitializeOnLoad]               → static ctor runs on every domain reload (Editor)
// [RuntimeInitializeOnLoadMethod]  → runs when play mode starts, finds DataManager
//
// Connection state (IP, connected flag) is stored in SessionState so it
// survives domain reloads. After reload the connector sends RECONNECT to
// LSL so LSL updates its target without a full re-handshake.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.Data
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class LSLService
    {
        // ── Config ────────────────────────────────────────────────────────────
        public static int Port = 12345;

        // ── State ─────────────────────────────────────────────────────────────
        private static UdpClient      _recv;
        private static UdpClient      _send;
        private static readonly object _sendLock = new object();
        private static Thread          _listenThread;
        private static bool            _running     = false;
        private static bool            _initialized = false;

        private static string _lslIP     = null;
        private static bool   _connected = false;
        private static string _deviceName;

        private static double      _nextAnnounceTime = 0;
        private const  double      AnnounceInterval  = 3.0;

        private static volatile bool   _dataRequested = false;
        private static volatile string _pendingPingId  = null;

        // Inbound experiment-controller messages (SUBJECT_ID:, CMD:, STATE_REQ).
        // These are queued from the listen thread and drained on the main thread
        // in Tick() so subscribers (LslExperimentRouter) can safely call Unity APIs.
        private static readonly ConcurrentQueue<string> _pendingHostMessages
            = new ConcurrentQueue<string>();

        /// <summary>
        /// Fires on the MAIN THREAD for every experiment-controller message
        /// received from the locked LSL host (SUBJECT_ID:, CMD:STEP/FORCE_STEP/
        /// SESSION:, STATE_REQ, etc). Subscribe in OnEnable / unsubscribe in
        /// OnDisable. Subscribers may call Unity APIs freely. Subscriber
        /// failures are isolated — one throwing handler doesn't stall others.
        /// </summary>
        public static event Action<string> OnHostMessage;

        /// <summary>
        /// Fires on the MAIN THREAD once the LSL host completes its CONNECT
        /// handshake (or RECONNECT after a domain reload). Use this to push
        /// initial state back to LSL — e.g. LslExperimentRouter sends a
        /// READY:subject=&lt;id&gt; message so the LSL operator's UI repopulates
        /// without a manual STATE_REQ. Fires once per CONNECT/RECONNECT.
        /// </summary>
        public static event Action OnHostConnected;

        // Set true by ListenLoop on CONNECT/RECONNECT, drained by Tick which
        // fires OnHostConnected on the main thread.
        private static volatile bool _pendingHostConnectedEvent = false;

        /// <summary>True when the LSL host has handshaken via CONNECT.</summary>
        public static bool IsHostLocked => _connected && !string.IsNullOrEmpty(_lslIP);

        /// <summary>The locked LSL host IP, or null. Read-only.</summary>
        public static string LockedHostIp => _lslIP;

        // Deferred recording requests — fired as soon as connection is ready
        private static volatile bool _pendingRecordingStart = false;
        private static volatile bool _pendingRecordingStop  = false;

        // Set by LSLServiceTicker when play mode starts
        internal static DataManager DataManager;

#if UNITY_EDITOR
        private const string SESSION_IP   = "LSLService.lslIP";
        private const string SESSION_CONN = "LSLService.connected";
#endif

        // ── Editor bootstrap (runs on every domain reload) ────────────────────
#if UNITY_EDITOR
        static LSLService()
        {
            Init();
            EditorApplication.update += EditorTick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                RequestRecordingStop();
                DataManager = null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                DataManager = null;
            }
        }

        private static void EditorTick()
        {
            if (!Application.isPlaying) Tick();
        }
#endif

        // ── Runtime bootstrap ─────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            if (!_initialized) Init();
            var go = new GameObject("[LSLServiceTicker]")
                { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<LSLServiceTicker>();
        }

        // ── Init ──────────────────────────────────────────────────────────────

        private static void Init()
        {
            if (_initialized) return;
            try
            {
                _deviceName = SystemInfo.deviceName ?? "Unity";

                _send = new UdpClient();
                _send.EnableBroadcast = true;

                _recv = new UdpClient();
                _recv.Client.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _recv.Client.Bind(new IPEndPoint(IPAddress.Any, Port));

                _running = true;
                _listenThread = new Thread(ListenLoop)
                    { IsBackground = true, Name = "LSLService" };
                _listenThread.Start();

                _initialized = true;

                // Restore previous connection across domain reload
#if UNITY_EDITOR
                string ip   = SessionState.GetString(SESSION_IP, null);
                bool   conn = SessionState.GetBool(SESSION_CONN, false);
                if (conn && !string.IsNullOrEmpty(ip))
                {
                    _lslIP     = ip;
                    _connected = true;
                    _pendingHostConnectedEvent = true;
                    SendTo($"RECONNECT,{_deviceName}", ip);
                    Debug.Log($"[LSLService] Restored — LSL at {ip}");
                    return;
                }
#endif
                Debug.Log($"[LSLService] Ready on :{Port} — broadcasting");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LSLService] Init failed: {e.Message}");
                _running = false;
                try { _send?.Close(); } catch { }
                try { _recv?.Close(); } catch { }
            }
        }

        // ── Tick ──────────────────────────────────────────────────────────────

        internal static void Tick()
        {
            if (!_initialized) return;
            try
            {
                double now = GetTime();
                if (!_connected && now >= _nextAnnounceTime)
                {
                    SendTo($"HELLO,unity,{_deviceName}", "255.255.255.255");
                    _nextAnnounceTime = now + AnnounceInterval;
                }

                if (_connected)
                {
                    if (_pendingRecordingStart)
                    {
                        _pendingRecordingStart = false;
                        SendTo("RECORDING_STARTED", _lslIP);
                        Debug.Log("[LSLService] RECORDING_STARTED → LSL (deferred)");
                    }
                    if (_pendingRecordingStop)
                    {
                        _pendingRecordingStop = false;
                        SendTo("RECORDING_STOPPED", _lslIP);
                        Debug.Log("[LSLService] RECORDING_STOPPED → LSL (deferred)");
                    }
                }

                if (_dataRequested)
                {
                    _dataRequested = false;
                    if (Application.isPlaying) SendDataSnapshot();
                }

                string pid = _pendingPingId;
                if (pid != null)
                {
                    _pendingPingId = null;
                    long ns = ToUnixNs(DateTime.UtcNow);
                    SendTo($"ACK:{pid}:{ns}", _lslIP);
                }

                // Fire OnHostConnected on the main thread once after CONNECT
                // or RECONNECT, so subscribers (LslExperimentRouter) can push
                // initial READY/STATE without a separate STATE_REQ from LSL.
                if (_pendingHostConnectedEvent)
                {
                    _pendingHostConnectedEvent = false;
                    Action handshakeHandler = OnHostConnected;
                    if (handshakeHandler != null)
                    {
                        try { handshakeHandler(); }
                        catch (Exception e)
                        {
                            Debug.LogError($"[LSLService] OnHostConnected subscriber threw: {e}");
                        }
                    }
                }

                // Drain experiment-controller messages on the main thread.
                // We catch per-handler so a buggy subscriber (e.g. router with
                // a null sequencer ref) doesn't stall the rest of Tick or
                // future drains.
                while (_pendingHostMessages.TryDequeue(out string hostMsg))
                {
                    Action<string> handler = OnHostMessage;
                    if (handler == null)
                    {
                        // Diagnostic: orphaned message — listen thread got it but
                        // no subscriber is attached. Means LslExperimentRouter is
                        // either not in the scene, or its OnEnable hasn't run yet.
                        Debug.LogWarning(
                            $"[LSLService] Dropping '{Truncate(hostMsg, 40)}' — " +
                            "no OnHostMessage subscriber. Check that " +
                            "LslExperimentRouter is attached to a GameObject in " +
                            "the active scene.");
                        continue;
                    }
                    Debug.Log($"[LSLService] Dispatching '{Truncate(hostMsg, 40)}' to subscriber.");
                    try { handler(hostMsg); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[LSLService] OnHostMessage subscriber threw on " +
                                       $"'{Truncate(hostMsg, 40)}': {e}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LSLService] Tick: {e.Message}");
            }
        }

        /// <summary>
        /// Send a message back to the locked LSL host. Safe to call from any
        /// thread — uses the same _sendLock as the rest of LSLService. No-op
        /// (with debug log) when no host is locked.
        /// </summary>
        public static void SendToHost(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            string ip = _lslIP;
            if (!_connected || string.IsNullOrEmpty(ip))
            {
                Debug.Log($"[LSLService] SendToHost dropped (no host): '{Truncate(msg, 40)}'");
                return;
            }
            SendTo(msg, ip);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        // ── Listen loop ───────────────────────────────────────────────────────

        private static void ListenLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    if (!_recv.Client.Poll(500000, SelectMode.SelectRead))
                        continue;

                    byte[] data = _recv.Receive(ref ep);
                    string msg  = Encoding.UTF8.GetString(data).Trim();
                    string src  = ep.Address.ToString();

                    if (msg == "DISCOVER")
                    {
                        SendTo($"HELLO,unity,{_deviceName}", src);
                    }
                    else if (msg == "CONNECT")
                    {
                        _lslIP     = src;
                        _connected = true;
                        _pendingHostConnectedEvent = true;
                        SendTo($"CONNECTED,{_deviceName}", src);
                        // Build-identifier LOG so the LSL operator can confirm the
                        // Quest is running the experiment-routing build. If you
                        // see this line in the LSL log, the new LSLService.cs is
                        // active and forwarding SUBJECT_ID/CMD messages.
                        SendTo($"LOG:LSLService experiment-routing build active (host={src})", src);
                        Debug.Log($"[LSLService] Connected — LSL at {src}");
#if UNITY_EDITOR
                        SessionState.SetString(SESSION_IP, src);
                        SessionState.SetBool(SESSION_CONN, true);
#endif
                        if (_pendingRecordingStart)
                        {
                            _pendingRecordingStart = false;
                            SendTo("RECORDING_STARTED", src);
                            Debug.Log("[LSLService] RECORDING_STARTED → LSL (on connect)");
                        }
                        if (_pendingRecordingStop)
                        {
                            _pendingRecordingStop = false;
                            SendTo("RECORDING_STOPPED", src);
                            Debug.Log("[LSLService] RECORDING_STOPPED → LSL (on connect)");
                        }
                    }
                    else if (msg == "DISCONNECT")
                    {
                        _connected = false;
                        _lslIP     = null;
#if UNITY_EDITOR
                        SessionState.SetBool(SESSION_CONN, false);
#endif
                        Debug.Log("[LSLService] Disconnected");
                    }
                    else if (msg.StartsWith("__calib_"))
                    {
                        SendTo("ACK:" + msg, src);
                    }
                    else if (msg == "REQUEST_DATA")
                    {
                        _dataRequested = true;
                    }
                    else if (msg.StartsWith("ping_"))
                    {
                        _pendingPingId = msg;
                    }
                    // Experiment-controller bridge: SUBJECT_ID:, CMD:, STATE_REQ
                    // are gated to the locked host (same as ping/data) and
                    // queued for the main thread. We do not parse them here —
                    // LslExperimentRouter does that on the main thread, where
                    // it can safely call into ExperimentSequencer/Controller.
                    else if (_connected && src == _lslIP &&
                             (msg.StartsWith("SUBJECT_ID:") ||
                              msg.StartsWith("SUBJECT_ID_OVERRIDE:") ||
                              msg.StartsWith("CMD:") ||
                              msg == "STATE_REQ"))
                    {
                        // Diagnostic: confirm receipt at the listen-thread level.
                        // If you don't see this message in the Unity console when
                        // LSL clicks Confirm, the packet isn't reaching LSLService
                        // at all (firewall, wrong IP, port collision).
                        Debug.Log($"[LSLService] Routing experiment msg: '{msg}'");
                        _pendingHostMessages.Enqueue(msg);
                    }
                    // Diagnostic: log any inbound from the locked host that
                    // we didn't recognise. Common culprit: typo in the LSL-side
                    // wire format, or LSLService.cs hasn't recompiled.
                    else if (_connected && src == _lslIP)
                    {
                        Debug.Log($"[LSLService] Unrecognised msg from locked host: '{(msg.Length > 60 ? msg.Substring(0, 60) : msg)}'");
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (ThreadAbortException)    { Thread.ResetAbort(); break; }
                catch (Exception e)
                {
                    // FIX (D-5): the previous bare `catch { }` swallowed every
                    // non-ObjectDisposed/non-ThreadAbort exception silently.
                    // For a research build whose LSL sync drives downstream
                    // alignment of EEG/audio/eye data, invisible network or
                    // parsing failures are a research-validity hazard. We can't
                    // raise here (would crash the listen thread and stop all
                    // LSL traffic), so log to stderr (safe from any thread)
                    // and continue. Burst-suppress with a coarse rate-limit
                    // so a sustained fault doesn't fill the log.
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (nowMs - _lastListenErrorLogMs > 1000)
                    {
                        _lastListenErrorLogMs = nowMs;
                        Console.Error.WriteLine(
                            $"[LSLService] ListenLoop: {e.GetType().Name}: {e.Message}");
                    }
                }
            }
        }

        // Rate-limit timestamp for the catch-all in ListenLoop. Touched only
        // from the listen thread, so no synchronisation needed.
        private static long _lastListenErrorLogMs;

        // ── Data snapshot ─────────────────────────────────────────────────────

        private static void SendDataSnapshot()
        {
            var dm = DataManager;
            if (dm == null || string.IsNullOrEmpty(_lslIP)) return;
            try
            {
                long ts = ToUnixNs(DateTime.UtcNow);
                var  sb = new System.Text.StringBuilder(256);
                sb.Append($"DATA,unity,{ts}");

                // Body — use public BodyData accessor (Body field is internal)
                try { var h = dm.BodyData?.Head;
                    if (h != null) { var r = h.rotation;
                        sb.Append($",headRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }
                try { var rp = dm.BodyData?.RightHandPalm;
                    if (rp != null) { var r = rp.rotation;
                        sb.Append($",rightPalmRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }
                try { var lp = dm.BodyData?.LeftHandPalm;
                    if (lp != null) { var r = lp.rotation;
                        sb.Append($",leftPalmRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }

                // Gaze — use public GazeData accessor
                try { var g = dm.GazeData?.CenterGaze?.GazePoint;
                    if (g.HasValue) sb.Append($",gazePointX={g.Value.x:F4}"); } catch { }

                // FACS — use public FACSData accessor (FACS field is internal)
                try {
                    var facs = dm.FACSData;
                    if (facs != null)
                    {
                        var au1 = facs.AU1_InnerBrowRaiser;
                        // AU1 Inner Brow Raiser — aggregate (mean L+R)
                        if (au1.InnerBrowRaiserL.HasValue && au1.InnerBrowRaiserR.HasValue)
                            sb.Append($",au1={(au1.InnerBrowRaiserL.Value + au1.InnerBrowRaiserR.Value) * 0.5f:F4}");

                        // AU2 Outer Brow Raiser — aggregate
                        var au2 = facs.AU2_OuterBrowRaiser;
                        if (au2.OuterBrowRaiserL.HasValue && au2.OuterBrowRaiserR.HasValue)
                            sb.Append($",au2={(au2.OuterBrowRaiserL.Value + au2.OuterBrowRaiserR.Value) * 0.5f:F4}");

                        // AU4 Brow Lowerer — aggregate
                        var au4 = facs.AU4_BrowLowerer;
                        if (au4.BrowLowererL.HasValue && au4.BrowLowererR.HasValue)
                            sb.Append($",au4={(au4.BrowLowererL.Value + au4.BrowLowererR.Value) * 0.5f:F4}");

                        // AU43 Eyes Closed (blink) — aggregate
                        var au43 = facs.AU43_EyesClosed;
                        if (au43.EyesClosedL.HasValue && au43.EyesClosedR.HasValue)
                            sb.Append($",blink={(au43.EyesClosedL.Value + au43.EyesClosedR.Value) * 0.5f:F4}");
                    }
                } catch { }

                SendTo(sb.ToString(), _lslIP);
            }
            catch { }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public static void RequestRecordingStart()
        {
            if (!_connected || string.IsNullOrEmpty(_lslIP))
            {
                _pendingRecordingStart = true;
                _pendingRecordingStop  = false;
                Debug.Log("[LSLService] RequestRecordingStart queued — waiting for connection");
                return;
            }
            _pendingRecordingStart = false;
            SendTo("RECORDING_STARTED", _lslIP);
            Debug.Log("[LSLService] RECORDING_STARTED → LSL");
        }

        public static void RequestRecordingStop()
        {
            _pendingRecordingStart = false;
            if (!_connected || string.IsNullOrEmpty(_lslIP))
            {
                _pendingRecordingStop = true;
                Debug.Log("[LSLService] RequestRecordingStop queued — waiting for connection");
                return;
            }
            _pendingRecordingStop = false;
            SendTo("RECORDING_STOPPED", _lslIP);
            Debug.Log("[LSLService] RECORDING_STOPPED → LSL");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SendTo(string msg, string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;
            byte[] data = Encoding.UTF8.GetBytes(msg);
            lock (_sendLock)
            {
                try { _send.Send(data, data.Length, ip, Port); }
                catch { }
            }
        }

        private static double GetTime() =>
            (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        private static long ToUnixNs(DateTime utc) =>
            (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks * 100L;
    }

    // ── Ticker: hidden GameObject, auto-created at runtime ────────────────────

    internal class LSLServiceTicker : MonoBehaviour
    {
        void Start()
        {
            // Find DataManager once scene is fully loaded
            LSLService.DataManager = FindObjectOfType<DataManager>();
            Debug.Log($"[LSLService] DataManager={(LSLService.DataManager != null ? "found" : "null")}");
        }

        void Update() => LSLService.Tick();

        void OnDestroy() => LSLService.DataManager = null;
    }
}
