using UnityEngine;
using UnityEngine.XR;

namespace MetaFrame.Performance
{
    /// <summary>
    /// Enables Fixed (or Eye-Tracked) Foveated Rendering on Meta Quest using
    /// the Meta XR Core SDK's OVRManager API — matching Meta's official docs:
    /// https://developers.meta.com/horizon/documentation/unity/unity-eye-tracked-foveated-rendering/
    ///
    /// Requirements:
    ///  - Meta XR Core SDK installed (provides OVRManager).
    ///  - In OpenXR settings (Android tab): "Meta XR Foveation" enabled,
    ///    "Subsampled Layout" enabled, Foveated Rendering Method set to FFR or ETFR.
    ///  - Vulkan as the only Graphics API; Multiview stereo rendering mode.
    ///
    /// Eye-Tracked Foveated Rendering (ETFR) is Quest Pro only. Quest 2/3 fall back
    /// to Fixed Foveated Rendering (FFR) automatically because the OVRManager
    /// guard checks `eyeTrackedFoveatedRenderingSupported`.
    /// </summary>
    [DisallowMultipleComponent]
    public class FoveatedRenderingController : MonoBehaviour
    {
        // Mirrors OVRManager.FoveatedRenderingLevel so we don't need OVR-typed
        // serialized fields (cleaner inspector, no version coupling).
        public enum Level
        {
            Off = 0,
            Low = 1,
            Medium = 2,
            High = 3,
            HighTop = 4,
        }

        [Header("Foveation")]
        [Tooltip("Static foveation level. Higher = more peripheral pixel reduction. Quest typically uses High.")]
        [SerializeField] private Level _level = Level.High;

        [Tooltip("Let the Quest runtime auto-adjust the level based on GPU load. Recommended ON.")]
        [SerializeField] private bool _useDynamic = true;

        [Tooltip("Eye-tracked foveation. Quest Pro only — auto-falls-back to FFR if unsupported.")]
        [SerializeField] private bool _useEyeTracking = false;

        [Header("Lifecycle")]
        [Tooltip("Retry interval (seconds) until OVRManager is initialized.")]
        [SerializeField] private float _retryInterval = 1f;

        [Tooltip("Log applied state to the console.")]
        [SerializeField] private bool _verbose = true;

        [Header("On-Screen Debug")]
        [Tooltip("Yellow text overlay showing applied state. May not render in URP+stereo on Quest.")]
        [SerializeField] private bool _showOverlay = true;

        private float _nextRetryTime;
        private bool _applied;
        private string _statusText = "[Foveation] Initializing...";

        private void OnEnable()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityChanged;
        }

        private void OnDisable()
        {
            QualitySettings.activeQualityLevelChanged -= OnQualityChanged;
        }

        private void Start() => ApplyFoveation();

        private void Update()
        {
            if (_applied || Time.unscaledTime < _nextRetryTime) return;
            ApplyFoveation();
        }

        private void OnQualityChanged(int previous, int current)
        {
            _applied = false;
            ApplyFoveation();
        }

        public void ApplyFoveation()
        {
            _nextRetryTime = Time.unscaledTime + _retryInterval;

            try
            {
                // Eye-tracked foveation — opt-in, Quest Pro only.
                bool etfrEnabled = false;
                if (_useEyeTracking && OVRManager.eyeTrackedFoveatedRenderingSupported)
                {
                    OVRManager.eyeTrackedFoveatedRenderingEnabled = true;
                    etfrEnabled = OVRManager.eyeTrackedFoveatedRenderingEnabled;
                }

                // Static level (drives both FFR and ETFR).
                OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)_level;

                // Dynamic adjustment based on GPU headroom.
                OVRManager.useDynamicFoveatedRendering = _useDynamic;

                _applied = true;

                int eyeW = XRSettings.eyeTextureWidth;
                int eyeH = XRSettings.eyeTextureHeight;
                float scale = XRSettings.eyeTextureResolutionScale;
                float vp = XRSettings.renderViewportScale;

                _statusText = $"[Fov] L={_level} dyn={_useDynamic} " +
                              $"ETFR_sup={OVRManager.eyeTrackedFoveatedRenderingSupported} " +
                              $"ETFR={etfrEnabled} | Eye={eyeW}x{eyeH} resScale={scale:F2} vp={vp:F2}";

                if (_verbose) Debug.Log(_statusText);
            }
            catch (System.Exception e)
            {
                _statusText = $"[Foveation] EXCEPTION: {e.GetType().Name}: {e.Message}";
                if (_verbose) Debug.LogError(_statusText);
            }
        }

        /// <summary>UnityEvent-friendly setter (e.g. from a settings menu).</summary>
        public void SetLevel(int level)
        {
            _level = (Level)Mathf.Clamp(level, 0, 4);
            _applied = false;
            ApplyFoveation();
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;

            var prev = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(11, 11, 1200, 60), _statusText);
            GUI.color = Color.yellow;
            GUI.Label(new Rect(10, 10, 1200, 60), _statusText);
            GUI.color = prev;
        }
    }
}
