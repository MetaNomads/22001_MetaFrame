// LSLConnector.cs
// Attach to any persistent GameObject in your Unity scene.
//
// Background thread handles all incoming UDP — lightweight, reliable.
// Uses Poll(500ms) instead of ReceiveTimeout to avoid continuous
// SocketException allocations that cause GC pressure.
// Debug.Log limited to Start / Connect / Disconnect only.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MetaFrame.Data
{
    public class LSLConnector : MonoBehaviour
    {
        [Header("Network")]
        public int udpPort = 12345;

        [Header("Data Sources")]
        public DataManager          dataManager;
        public TrackingDataRecorder recorder;

        // ── State ─────────────────────────────────────────────────────────────
        private UdpClient _recv;
        private UdpClient _send;
        private readonly object _sendLock = new object();

        private Thread _listenThread;
        private bool   _running     = false;
        private bool   _initialized = false;

        private string _lslIP     = null;
        private bool   _connected = false;
        private string _deviceName;

        private float       _nextAnnounceTime;
        private const float AnnounceInterval = 3f;

        // Shared flags — set by listen thread, read/cleared by Update()
        private volatile bool   _dataRequested = false;
        private volatile string _pendingPingId  = null;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Start()
        {
            try
            {
                _deviceName = SystemInfo.deviceName ?? "Unity";

                _send = new UdpClient();
                _send.EnableBroadcast = true;

                _recv = new UdpClient(udpPort);

                _running = true;
                _listenThread = new Thread(ListenLoop) { IsBackground = true };
                _listenThread.Start();

                _nextAnnounceTime = Time.unscaledTime + AnnounceInterval;
                _initialized = true;

                Debug.Log($"[LSLConnector] Ready on :{udpPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LSLConnector] Start failed: {e.Message}");
                _running = false;
                try { _send?.Close(); } catch { }
                try { _recv?.Close(); } catch { }
            }
        }

        void Update()
        {
            if (!_initialized) return;
            try
            {
                // Broadcast HELLO until connected
                if (!_connected && Time.unscaledTime >= _nextAnnounceTime)
                {
                    SendTo($"HELLO,unity,{_deviceName}", "255.255.255.255");
                    _nextAnnounceTime = Time.unscaledTime + AnnounceInterval;
                }

                // Data snapshot — requested by listen thread
                if (_dataRequested)
                {
                    _dataRequested = false;
                    SendDataSnapshot();
                }

                // Ping ACK — timestamp taken on main thread
                string pingId = _pendingPingId;
                if (pingId != null)
                {
                    _pendingPingId = null;
                    long ns = ToUnixNs(DateTime.UtcNow);
                    SendTo($"ACK:{pingId}:{ns}", _lslIP);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LSLConnector] Update: {e.Message}");
            }
        }

        void OnDestroy()
        {
            if (_initialized && _connected && !string.IsNullOrEmpty(_lslIP))
                SendTo("RECORDING_STOPPED", _lslIP);

            _running = false;
            _listenThread?.Join(1500);
            try { _recv?.Close(); } catch { }
            try { _send?.Close(); } catch { }
        }

        // ── Listen loop ───────────────────────────────────────────────────────
        // Poll(500ms): returns false with zero allocations if no data.
        // Returns true instantly when a packet arrives.

        private void ListenLoop()
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
                        SendTo($"CONNECTED,{_deviceName}", src);
                        Debug.Log($"[LSLConnector] Connected — LSL at {src}");
                    }
                    else if (msg == "DISCONNECT")
                    {
                        _connected = false;
                        _lslIP     = null;
                        Debug.Log("[LSLConnector] Disconnected");
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
                        // Hand to Update() for main-thread timestamp
                        _pendingPingId = msg;
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (ThreadAbortException)
                {
                    Thread.ResetAbort();
                    break;
                }
                catch { /* swallow all other errors silently */ }
            }
        }

        // ── Data snapshot ─────────────────────────────────────────────────────

        private void SendDataSnapshot()
        {
            if (dataManager == null || string.IsNullOrEmpty(_lslIP)) return;
            try
            {
                long ts = ToUnixNs(DateTime.UtcNow);
                var  sb = new System.Text.StringBuilder(256);
                sb.Append($"DATA,unity,{ts}");

                try { var h = dataManager?.Body?.Data?.Head;
                    if (h != null) { var r = h.rotation;
                        sb.Append($",headRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }

                try { var rp = dataManager?.Body?.Data?.RightHandPalm;
                    if (rp != null) { var r = rp.rotation;
                        sb.Append($",rightPalmRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }

                try { var lp = dataManager?.Body?.Data?.LeftHandPalm;
                    if (lp != null) { var r = lp.rotation;
                        sb.Append($",leftPalmRot={r.x:F4},{r.y:F4},{r.z:F4},{r.w:F4}"); } } catch { }

                try { var g = dataManager?.Gaze?.Data?.CenterGaze?.GazePoint;
                    if (g.HasValue) sb.Append($",gazePointX={g.Value.x:F4}"); } catch { }

                try { if (dataManager?.FACS?.Data != null) {
                    var lip = dataManager.FACS.Data.AU10_UpperLipRaiser;
                    if (lip.UpperLipRaiserL.HasValue) sb.Append($",upperLipL={lip.UpperLipRaiserL.Value:F4}");
                    if (lip.UpperLipRaiserR.HasValue) sb.Append($",upperLipR={lip.UpperLipRaiserR.Value:F4}");
                } } catch { }

                SendTo(sb.ToString(), _lslIP);
            }
            catch { }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void RequestLSLRecordingStart()
        {
            if (!_connected || string.IsNullOrEmpty(_lslIP)) return;
            SendTo("RECORDING_STARTED", _lslIP);
        }

        public void RequestLSLRecordingStop()
        {
            if (!_connected || string.IsNullOrEmpty(_lslIP)) return;
            SendTo("RECORDING_STOPPED", _lslIP);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SendTo(string msg, string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;
            byte[] data = Encoding.UTF8.GetBytes(msg);
            lock (_sendLock)
            {
                try { _send.Send(data, data.Length, ip, udpPort); }
                catch { }
            }
        }

        private static long ToUnixNs(DateTime utc) =>
            (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks * 100L;
    }
}