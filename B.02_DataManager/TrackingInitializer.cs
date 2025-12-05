using System.Collections;
using UnityEngine;
using Sirenix.OdinInspector;

namespace MetaNomads.Data
{
    /// <summary>
    /// Handles initialization and retry logic for Meta Quest tracking systems at the API level.
    /// Addresses the common SDK issue where tracking becomes disabled during XR initialization.
    /// </summary>
    public class TrackingInitializer : MonoBehaviour
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

        [BoxGroup("Face Tracking 2.0 Data Sources")]
        [Tooltip("Enable visual (camera-based) face tracking")]
        [SerializeField] private bool _useVisualFaceTracking = true;

        [BoxGroup("Face Tracking 2.0 Data Sources")]
        [Tooltip("Enable audio-based lip sync")]
        [SerializeField] private bool _useAudioFaceTracking = false;

        [BoxGroup("Retry Configuration")]
        [Range(1, 999)]
        [Tooltip("Maximum number of initialization attempts per tracking type")]
        [SerializeField] private int _maxRetryAttempts = 5;

        [BoxGroup("Retry Configuration")]
        [Range(0.1f, 2f)]
        [Tooltip("Time between retry attempts (seconds)")]
        [SerializeField] private float _retryFrequency = 0.5f;

        [BoxGroup("Retry Configuration")]
        [Range(0.1f, 1f)]
        [Tooltip("Time to wait after Start call before checking validity (seconds)")]
        [SerializeField] private float _initializationDelay = 0.5f;

        // Runtime state
        private bool _bodyTrackingInitialized = false;
        private bool _faceTrackingInitialized = false;
        private bool _eyeTrackingInitialized = false;

        void Awake()
        {
            StartCoroutine(InitializeTracking());
        }

        private IEnumerator InitializeTracking()
        {
            for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            {
                bool allSuccessful = true;

                // Try Body Tracking
                if (_enableBodyTracking && !_bodyTrackingInitialized)
                {
                    yield return StartCoroutine(TryInitializeBodyTracking(attempt));

                    if (_bodyTrackingInitialized)
                    {
                        Debug.Log($"[TrackingInitializer] ✓ Body tracking API enabled successfully");
                    }
                    else
                    {
                        allSuccessful = false;
                        if (attempt < _maxRetryAttempts)
                        {
                            Debug.Log($"[TrackingInitializer] Attempt {attempt}/{_maxRetryAttempts}: Retrying body tracking...");
                        }
                    }
                }

                // Try Face Tracking
                if (_enableFaceTracking && !_faceTrackingInitialized)
                {
                    yield return StartCoroutine(TryInitializeFaceTracking(attempt));

                    if (_faceTrackingInitialized)
                    {
                        Debug.Log($"[TrackingInitializer] ✓ Face tracking 2.0 API enabled successfully");
                    }
                    else
                    {
                        allSuccessful = false;
                        if (attempt < _maxRetryAttempts)
                        {
                            Debug.Log($"[TrackingInitializer] Attempt {attempt}/{_maxRetryAttempts}: Retrying face tracking 2.0...");
                        }
                    }
                }

                // Try Eye Tracking
                if (_enableEyeTracking && !_eyeTrackingInitialized)
                {
                    yield return StartCoroutine(TryInitializeEyeTracking(attempt));

                    if (_eyeTrackingInitialized)
                    {
                        Debug.Log($"[TrackingInitializer] ✓ Eye tracking API enabled successfully");
                    }
                    else
                    {
                        allSuccessful = false;
                        if (attempt < _maxRetryAttempts)
                        {
                            Debug.Log($"[TrackingInitializer] Attempt {attempt}/{_maxRetryAttempts}: Retrying eye tracking...");
                        }
                    }
                }

                // If all enabled tracking types are initialized, exit early
                if (allSuccessful)
                {
                    Debug.Log($"[TrackingInitializer] All enabled tracking systems initialized successfully");
                    yield break;
                }

                // Wait before next retry
                if (attempt < _maxRetryAttempts)
                {
                    yield return new WaitForSeconds(_retryFrequency);
                }
            }

            // Log warnings for any tracking that failed to initialize
            LogFinalStatus();
        }

        private IEnumerator TryInitializeBodyTracking(int attempt)
        {
            // Check if tracking is already enabled
            if (OVRPlugin.bodyTrackingEnabled)
            {
                _bodyTrackingInitialized = true;
                yield break;
            }

            // Try to start tracking
            bool startResult = false;
            try
            {
                startResult = OVRPlugin.StartBodyTracking();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TrackingInitializer] Body tracking exception: {e.Message}");
                _bodyTrackingInitialized = false;
                yield break;
            }

            if (startResult)
            {
                // Wait for tracking to initialize
                yield return new WaitForSeconds(_initializationDelay);

                // Check if API reports tracking as enabled
                _bodyTrackingInitialized = OVRPlugin.bodyTrackingEnabled;
            }
            else
            {
                _bodyTrackingInitialized = false;
            }
        }

        private IEnumerator TryInitializeFaceTracking(int attempt)
        {
            // Check if tracking is already enabled
            if (OVRPlugin.faceTracking2Enabled)
            {
                _faceTrackingInitialized = true;
                yield break;
            }

            // Build data sources array based on settings
            var dataSources = new System.Collections.Generic.List<OVRPlugin.FaceTrackingDataSource>();
            if (_useVisualFaceTracking) dataSources.Add(OVRPlugin.FaceTrackingDataSource.Visual);
            if (_useAudioFaceTracking) dataSources.Add(OVRPlugin.FaceTrackingDataSource.Audio);

            if (dataSources.Count == 0)
            {
                Debug.LogWarning($"[TrackingInitializer] No face tracking data sources selected");
                _faceTrackingInitialized = false;
                yield break;
            }

            // Try to start tracking
            bool startResult = false;
            try
            {
                startResult = OVRPlugin.StartFaceTracking2(dataSources.ToArray());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TrackingInitializer] Face tracking 2.0 exception: {e.Message}");
                _faceTrackingInitialized = false;
                yield break;
            }

            if (startResult)
            {
                // Wait for tracking to initialize
                yield return new WaitForSeconds(_initializationDelay);

                // Check if API reports tracking as enabled
                _faceTrackingInitialized = OVRPlugin.faceTracking2Enabled;
            }
            else
            {
                _faceTrackingInitialized = false;
            }
        }

        private IEnumerator TryInitializeEyeTracking(int attempt)
        {
            // Check if tracking is already enabled
            if (OVRPlugin.eyeTrackingEnabled)
            {
                _eyeTrackingInitialized = true;
                yield break;
            }

            // Try to start tracking
            bool startResult = false;
            try
            {
                startResult = OVRPlugin.StartEyeTracking();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TrackingInitializer] Eye tracking exception: {e.Message}");
                _eyeTrackingInitialized = false;
                yield break;
            }

            if (startResult)
            {
                // Wait for tracking to initialize
                yield return new WaitForSeconds(_initializationDelay);

                // Check if API reports tracking as enabled
                _eyeTrackingInitialized = OVRPlugin.eyeTrackingEnabled;
            }
            else
            {
                _eyeTrackingInitialized = false;
            }
        }

        private void LogFinalStatus()
        {
            if (_enableBodyTracking && !_bodyTrackingInitialized)
            {
                Debug.LogWarning($"[TrackingInitializer] ⚠ Body tracking API failed to enable after {_maxRetryAttempts} attempts");
            }

            if (_enableFaceTracking && !_faceTrackingInitialized)
            {
                Debug.LogWarning($"[TrackingInitializer] ⚠ Face tracking 2.0 API failed to enable after {_maxRetryAttempts} attempts");
            }

            if (_enableEyeTracking && !_eyeTrackingInitialized)
            {
                Debug.LogWarning($"[TrackingInitializer] ⚠ Eye tracking API failed to enable after {_maxRetryAttempts} attempts");
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Inspector Information
        /// </summary>

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Body Tracking API")]
        private string BodyTrackingStatus
        {
            get
            {
                if (!_enableBodyTracking) return "○ Disabled";
                return OVRPlugin.bodyTrackingEnabled ? "✓ Enabled" : "⚠ Not Enabled";
            }
        }

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Face Tracking 2.0 API")]
        private string FaceTrackingStatus
        {
            get
            {
                if (!_enableFaceTracking) return "○ Disabled";
                return OVRPlugin.faceTracking2Enabled ? "✓ Enabled" : "⚠ Not Enabled";
            }
        }

        [BoxGroup("Runtime Status")]
        [ShowInInspector, ReadOnly]
        [LabelText("Eye Tracking API")]
        private string EyeTrackingStatus
        {
            get
            {
                if (!_enableEyeTracking) return "○ Disabled";
                return OVRPlugin.eyeTrackingEnabled ? "✓ Enabled" : "⚠ Not Enabled";
            }
        }
    }
}