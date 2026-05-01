using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using MetaFrame.Contracts;

namespace MetaNomads.Data
{
    /// <summary>
    /// Ensures Meta XR body, face, and eye tracking start (and stay started) for
    /// the entire session. Designed for Meta Link to PC where the OVR session
    /// can take >1s after Play to fully establish, and where mid-session
    /// dropouts (link drops, headset removed, thermal throttling) are common.
    ///
    /// Strategy:
    ///   1. Wait for OVRManager.OVRManagerinitialized before the first Start —
    ///      Awake is too early on Link.
    ///   2. After initial setup, run a heartbeat coroutine FOREVER. Every
    ///      _heartbeatIntervalSeconds, check each enabled tracking system; if
    ///      any reports OVRPlugin.xTrackingEnabled == false, re-attempt Start.
    ///      This recovers from any mid-session dropout automatically.
    ///   3. Implements ISelfHealing so the existing SelfHealRunner can also
    ///      probe and repair on its own schedule.
    ///   4. Exposes a public ForceReinitialize() method (and Inspector button)
    ///      for manual recovery if automatic recovery fails.
    /// </summary>
    public class TrackingInitializer : MonoBehaviour, ISelfHealing
    {
        [BoxGroup("Tracking Settings")]
        [Tooltip("Enable body tracking initialization")]
        [SerializeField] private bool _enableBodyTracking = true;

        [BoxGroup("Tracking Settings")]
        [Tooltip("Enable face tracking initialization (Face Tracking 2.0)")]
        [SerializeField] private bool _enableFaceTracking = true;

        [BoxGroup("Tracking Settings")]
        [Tooltip("Enable eye tracking initialization")]
        [SerializeField] private bool _enableEyeTracking = true;

        [BoxGroup("Face Tracking v2 Data Sources")]
        [Tooltip("Enable visual (camera-based) face tracking")]
        [SerializeField] private bool _useVisualFaceTracking = true;

        [BoxGroup("Face Tracking v2 Data Sources")]
        [Tooltip("Enable audio-based lip sync")]
        [SerializeField] private bool _useAudioFaceTracking = false;

        // ── Timing ────────────────────────────────────────────────────────────

        [BoxGroup("Timing")]
        [Tooltip("How long to wait for OVRManager to finish initializing before " +
                 "we start trying anyway. On Meta Link this can take 1–3s after " +
                 "the Editor enters Play Mode.")]
        [SerializeField, Range(1f, 30f)] private float _ovrReadyTimeoutSeconds = 10f;

        [BoxGroup("Timing")]
        [Tooltip("Heartbeat interval (seconds). Every tick, the heartbeat checks " +
                 "OVRPlugin.bodyTrackingEnabled / faceTracking2Enabled / eyeTrackingEnabled " +
                 "and re-attempts Start for any system that has dropped.\n\n" +
                 "Lower = faster recovery, higher = less overhead.")]
        [SerializeField, Range(0.5f, 10f)] private float _heartbeatIntervalSeconds = 1.5f;

        [BoxGroup("Timing")]
        [Tooltip("Time between calling Start* and reading the verification flag (seconds). " +
                 "OVRPlugin needs a moment after Start to update the *Enabled flag.")]
        [SerializeField, Range(0.1f, 2f)] private float _verifyDelaySeconds = 0.5f;

        // ── Runtime state ─────────────────────────────────────────────────────

        // Heartbeat-tracked dropout counts. Preserved across recoveries so the
        // research log retains a record of how stable the link was during a session.
        private int _bodyStartCount;
        private int _faceStartCount;
        private int _eyeStartCount;
        private int _bodyDropoutCount;
        private int _faceDropoutCount;
        private int _eyeDropoutCount;

        // Previous-tick state used to distinguish "still down" from "just dropped".
        private bool _bodyWasActive;
        private bool _faceWasActive;
        private bool _eyeWasActive;

        private Coroutine _supervisor;
        private bool _ovrReady;

        // ── Public API ────────────────────────────────────────────────────────

        public bool BodyTrackingActive => OVRPlugin.bodyTrackingEnabled;
        public bool FaceTrackingActive => OVRPlugin.faceTracking2Enabled;
        public bool EyeTrackingActive  => OVRPlugin.eyeTrackingEnabled;

        /// <summary>
        /// True iff every tracking system the user enabled in the Inspector is
        /// currently reporting active. Useful for HUD indicators or for a
        /// trial-start gate that refuses to begin if anything is offline.
        /// </summary>
        public bool AllRequestedActive =>
            (!_enableBodyTracking || BodyTrackingActive) &&
            (!_enableFaceTracking || FaceTrackingActive) &&
            (!_enableEyeTracking  || EyeTrackingActive);

        /// <summary>
        /// Stops the running heartbeat (if any), waits for OVR to be ready,
        /// re-attempts Start on every enabled system, and restarts the heartbeat.
        /// Wire this to a UI button or call from your scene's recovery flow.
        /// </summary>
        [BoxGroup("Controls"), PropertyOrder(99)]
        [Button("Force Re-initialize Tracking", ButtonSizes.Large), GUIColor(1f, 0.7f, 0.4f)]
        public void ForceReinitialize()
        {
            Debug.Log("[TrackingInitializer] ForceReinitialize requested.");
            if (_supervisor != null)
            {
                StopCoroutine(_supervisor);
                _supervisor = null;
            }
            _ovrReady       = false;
            _bodyWasActive  = false;
            _faceWasActive  = false;
            _eyeWasActive   = false;
            _supervisor = StartCoroutine(SuperviseTracking());
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            _supervisor = StartCoroutine(SuperviseTracking());
        }

        void OnDestroy()
        {
            if (_supervisor != null) StopCoroutine(_supervisor);
        }

        private void OnValidate()
        {
            if (_heartbeatIntervalSeconds < 0.5f) _heartbeatIntervalSeconds = 0.5f;
            if (_ovrReadyTimeoutSeconds   < 1f)   _ovrReadyTimeoutSeconds   = 1f;
            if (_verifyDelaySeconds       < 0.1f) _verifyDelaySeconds       = 0.1f;
        }

        // ── Supervisor — wait for OVR, do initial Start, then heartbeat forever ─

        private IEnumerator SuperviseTracking()
        {
            // Phase 1 — wait for OVR to be ready before first Start.
            float waitStart = Time.realtimeSinceStartup;
            while (!OvrIsReady())
            {
                if (Time.realtimeSinceStartup - waitStart > _ovrReadyTimeoutSeconds)
                {
                    Debug.LogWarning(
                        $"[TrackingInitializer] OVRManager not initialized after " +
                        $"{_ovrReadyTimeoutSeconds:F1}s — proceeding anyway. The heartbeat " +
                        "will keep retrying as soon as it becomes available.");
                    break;
                }
                yield return null;
            }
            _ovrReady = true;
            Debug.Log(
                $"[TrackingInitializer] OVRManager ready after " +
                $"{(Time.realtimeSinceStartup - waitStart):F2}s. Starting initial tracking attempts.");

            // Phase 2 — initial Start attempt for each enabled system.
            yield return AttemptAllStarts();

            // Phase 3 — heartbeat forever. Each tick verifies the flags and
            // re-attempts Start for any system that has dropped.
            var heartbeatWait = new WaitForSeconds(_heartbeatIntervalSeconds);
            while (true)
            {
                yield return heartbeatWait;
                yield return Heartbeat();
            }
        }

        private bool OvrIsReady()
        {
            // OVRManager.OVRManagerinitialized goes true after OVRManager.Awake
            // completes its setup. On Meta Link this happens AFTER the link's
            // OVR session is actually usable — making it the right gate.
            return OVRManager.OVRManagerinitialized;
        }

        // ── Heartbeat ─────────────────────────────────────────────────────────

        private IEnumerator Heartbeat()
        {
            // Detect dropouts BEFORE attempting any restart so the dropout count
            // increments exactly once per dropout, not once per heartbeat tick.
            if (_enableBodyTracking && _bodyWasActive && !OVRPlugin.bodyTrackingEnabled)
            {
                _bodyDropoutCount++;
                Debug.LogWarning(
                    $"[TrackingInitializer] ⚠ Body tracking DROPPED at " +
                    $"{Time.realtimeSinceStartup:F2}s (dropout #{_bodyDropoutCount}). Re-attempting Start.");
            }
            if (_enableFaceTracking && _faceWasActive && !OVRPlugin.faceTracking2Enabled)
            {
                _faceDropoutCount++;
                Debug.LogWarning(
                    $"[TrackingInitializer] ⚠ Face tracking DROPPED at " +
                    $"{Time.realtimeSinceStartup:F2}s (dropout #{_faceDropoutCount}). Re-attempting Start.");
            }
            if (_enableEyeTracking && _eyeWasActive && !OVRPlugin.eyeTrackingEnabled)
            {
                _eyeDropoutCount++;
                Debug.LogWarning(
                    $"[TrackingInitializer] ⚠ Eye tracking DROPPED at " +
                    $"{Time.realtimeSinceStartup:F2}s (dropout #{_eyeDropoutCount}). Re-attempting Start.");
            }

            yield return AttemptAllStarts();

            // Update active flags AFTER the attempt so dropout detection on the
            // next tick uses the post-recovery state as the baseline.
            _bodyWasActive = OVRPlugin.bodyTrackingEnabled;
            _faceWasActive = OVRPlugin.faceTracking2Enabled;
            _eyeWasActive  = OVRPlugin.eyeTrackingEnabled;
        }

        // ── Start attempts ────────────────────────────────────────────────────

        private IEnumerator AttemptAllStarts()
        {
            if (_enableBodyTracking && !OVRPlugin.bodyTrackingEnabled)
                yield return TryStartBody();

            if (_enableFaceTracking && !OVRPlugin.faceTracking2Enabled)
                yield return TryStartFace();

            if (_enableEyeTracking && !OVRPlugin.eyeTrackingEnabled)
                yield return TryStartEye();
        }

        private IEnumerator TryStartBody()
        {
            bool started = SafeStart("Body", () => OVRPlugin.StartBodyTracking());
            if (started) _bodyStartCount++;
            yield return new WaitForSeconds(_verifyDelaySeconds);

            if (OVRPlugin.bodyTrackingEnabled)
                Debug.Log("[TrackingInitializer] ✓ Body tracking confirmed active.");
        }

        private IEnumerator TryStartFace()
        {
            // Build the data sources list each call — flags can be toggled live.
            var sources = new List<OVRPlugin.FaceTrackingDataSource>();
            if (_useVisualFaceTracking) sources.Add(OVRPlugin.FaceTrackingDataSource.Visual);
            if (_useAudioFaceTracking)  sources.Add(OVRPlugin.FaceTrackingDataSource.Audio);

            if (sources.Count == 0)
            {
                Debug.LogWarning("[TrackingInitializer] No face tracking data sources selected — skipping.");
                yield break;
            }

            bool started = SafeStart("Face", () => OVRPlugin.StartFaceTracking2(sources.ToArray()));
            if (started) _faceStartCount++;
            yield return new WaitForSeconds(_verifyDelaySeconds);

            if (OVRPlugin.faceTracking2Enabled)
                Debug.Log("[TrackingInitializer] ✓ Face tracking 2.0 confirmed active.");
        }

        private IEnumerator TryStartEye()
        {
            bool started = SafeStart("Eye", () => OVRPlugin.StartEyeTracking());
            if (started) _eyeStartCount++;
            yield return new WaitForSeconds(_verifyDelaySeconds);

            if (OVRPlugin.eyeTrackingEnabled)
                Debug.Log("[TrackingInitializer] ✓ Eye tracking confirmed active.");
        }

        // Single helper that wraps the OVRPlugin.Start* call in a try/catch and
        // returns whether the call succeeded. Avoids try-around-yield issues.
        private static bool SafeStart(string label, System.Func<bool> startFn)
        {
            try
            {
                bool ok = startFn();
                if (!ok)
                    Debug.LogWarning($"[TrackingInitializer] OVRPlugin.Start{label}Tracking() returned false.");
                return ok;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TrackingInitializer] OVRPlugin.Start{label}Tracking() threw: {e.Message}");
                return false;
            }
        }

        // ── ISelfHealing ──────────────────────────────────────────────────────
        // The SelfHealRunner scans every few seconds and calls RunSelfHeal on
        // every implementer. This gives us a SECOND independent recovery path
        // alongside the heartbeat — useful belt-and-braces redundancy for
        // research-critical data capture.

        public string SelfHealLabel => $"TrackingInitializer({name})";

        public bool RunSelfHeal()
        {
            // Don't try to start anything before OVR is ready — would just spam
            // failed Start calls and confuse the dropout counters.
            if (!_ovrReady) return false;

            bool healed = false;

            if (_enableBodyTracking)
            {
                if (!Contract.Healed(
                        () => OVRPlugin.bodyTrackingEnabled,
                        () => { OVRPlugin.StartBodyTracking(); _bodyStartCount++; },
                        "body tracking offline; called StartBodyTracking",
                        this))
                    healed = true;
            }

            if (_enableFaceTracking)
            {
                if (!Contract.Healed(
                        () => OVRPlugin.faceTracking2Enabled,
                        () =>
                        {
                            var sources = new List<OVRPlugin.FaceTrackingDataSource>();
                            if (_useVisualFaceTracking) sources.Add(OVRPlugin.FaceTrackingDataSource.Visual);
                            if (_useAudioFaceTracking)  sources.Add(OVRPlugin.FaceTrackingDataSource.Audio);
                            if (sources.Count > 0)
                            {
                                OVRPlugin.StartFaceTracking2(sources.ToArray());
                                _faceStartCount++;
                            }
                        },
                        "face tracking offline; called StartFaceTracking2",
                        this))
                    healed = true;
            }

            if (_enableEyeTracking)
            {
                if (!Contract.Healed(
                        () => OVRPlugin.eyeTrackingEnabled,
                        () => { OVRPlugin.StartEyeTracking(); _eyeStartCount++; },
                        "eye tracking offline; called StartEyeTracking",
                        this))
                    healed = true;
            }

            return healed;
        }

        // ── Inspector status (Odin) ───────────────────────────────────────────

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("All Requested Active")]
        private string AllActiveLabel => AllRequestedActive ? "✓ ALL ACTIVE" : "⚠ SOMETHING IS DOWN";

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("OVR Ready")]
        private string OvrReadyLabel => _ovrReady ? "✓ Ready" : "⧗ Waiting…";

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Body Tracking")]
        private string BodyStatus =>
            !_enableBodyTracking ? "○ Disabled"
                : OVRPlugin.bodyTrackingEnabled
                    ? $"✓ Active  (starts: {_bodyStartCount}, dropouts: {_bodyDropoutCount})"
                    : $"⚠ Offline (starts: {_bodyStartCount}, dropouts: {_bodyDropoutCount})";

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Face Tracking 2.0")]
        private string FaceStatus =>
            !_enableFaceTracking ? "○ Disabled"
                : OVRPlugin.faceTracking2Enabled
                    ? $"✓ Active  (starts: {_faceStartCount}, dropouts: {_faceDropoutCount})"
                    : $"⚠ Offline (starts: {_faceStartCount}, dropouts: {_faceDropoutCount})";

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Eye Tracking")]
        private string EyeStatus =>
            !_enableEyeTracking ? "○ Disabled"
                : OVRPlugin.eyeTrackingEnabled
                    ? $"✓ Active  (starts: {_eyeStartCount}, dropouts: {_eyeDropoutCount})"
                    : $"⚠ Offline (starts: {_eyeStartCount}, dropouts: {_eyeDropoutCount})";
    }
}
