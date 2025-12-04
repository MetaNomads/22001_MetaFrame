using System.Collections;
using UnityEngine;

namespace MetaNomads.Data
{
    /// <summary>
    /// Handles initialization retry for Meta tracking systems
    /// </summary>
    public class TrackingInitializer : MonoBehaviour
    {
        [Header("Tracking Types")]
        [SerializeField] private bool _enableBodyTracking = true;
        [SerializeField] private bool _enableFaceTracking = true;
        [SerializeField] private bool _enableEyeTracking = true;

        [Header("Retry Settings")]
        [SerializeField] private float _retryInterval = 1.0f;
        [SerializeField] private int _maxRetries = 30;

        private bool _bodyReady, _faceReady, _eyeReady;

        void Start()
        {
            if (_enableBodyTracking)
                StartCoroutine(InitializeBodyTracking());

            if (_enableFaceTracking)
                StartCoroutine(InitializeFaceTracking());

            if (_enableEyeTracking)
                StartCoroutine(InitializeEyeTracking());
        }

        private IEnumerator InitializeBodyTracking()
        {
            int attempts = 0;

            while (!_bodyReady && (_maxRetries == 0 || attempts < _maxRetries))
            {
                attempts++;
                OVRPlugin.StartBodyTracking();
                yield return null;

                if (OVRPlugin.bodyTrackingEnabled)
                {
                    _bodyReady = true;
                    Debug.Log($"[TrackingInitializer] Body tracking initialized (attempt {attempts})");
                    yield break;
                }

                Debug.LogWarning($"[TrackingInitializer] Body tracking attempt {attempts} failed, retrying...");
                yield return new WaitForSeconds(_retryInterval);
            }

            if (!_bodyReady)
                Debug.LogError($"[TrackingInitializer] Body tracking failed after {attempts} attempts");
        }

        private IEnumerator InitializeFaceTracking()
        {
            int attempts = 0;

            while (!_faceReady && (_maxRetries == 0 || attempts < _maxRetries))
            {
                attempts++;
                OVRPlugin.StartFaceTracking();
                yield return null;

                if (OVRPlugin.faceTrackingEnabled)
                {
                    _faceReady = true;
                    Debug.Log($"[TrackingInitializer] Face tracking initialized (attempt {attempts})");
                    yield break;
                }

                Debug.LogWarning($"[TrackingInitializer] Face tracking attempt {attempts} failed, retrying...");
                yield return new WaitForSeconds(_retryInterval);
            }

            if (!_faceReady)
                Debug.LogError($"[TrackingInitializer] Face tracking failed after {attempts} attempts");
        }

        private IEnumerator InitializeEyeTracking()
        {
            int attempts = 0;

            while (!_eyeReady && (_maxRetries == 0 || attempts < _maxRetries))
            {
                attempts++;
                OVRPlugin.StartEyeTracking();
                yield return null;

                if (OVRPlugin.eyeTrackingEnabled)
                {
                    _eyeReady = true;
                    Debug.Log($"[TrackingInitializer] Eye tracking initialized (attempt {attempts})");
                    yield break;
                }

                Debug.LogWarning($"[TrackingInitializer] Eye tracking attempt {attempts} failed, retrying...");
                yield return new WaitForSeconds(_retryInterval);
            }

            if (!_eyeReady)
                Debug.LogError($"[TrackingInitializer] Eye tracking failed after {attempts} attempts");
        }

        public bool IsFullyInitialized() =>
            (!_enableBodyTracking || _bodyReady) &&
            (!_enableFaceTracking || _faceReady) &&
            (!_enableEyeTracking || _eyeReady);

        void OnDestroy()
        {
            if (_bodyReady) OVRPlugin.StopBodyTracking();
            if (_faceReady) OVRPlugin.StopFaceTracking();
            if (_eyeReady) OVRPlugin.StopEyeTracking();
        }
    }
}
