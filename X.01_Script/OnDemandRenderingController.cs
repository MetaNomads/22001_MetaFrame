using UnityEngine;
using UnityEngine.Rendering;

namespace MetaFrame.Performance
{
    /// <summary>
    /// Forces Unity to render only every Nth frame using OnDemandRendering.
    /// The headset's compositor fills in the gaps via reprojection, so the
    /// displayed image still updates at the headset's full refresh rate, but
    /// the game's CPU + GPU only runs at 1/N of that rate.
    ///
    /// Frame interval = 2 → app renders at half the display rate
    ///   (e.g. 36 fps render for a 72 Hz display).
    /// Frame interval = 3 → app renders at one-third (24 fps for 72 Hz).
    ///
    /// Trade-off: motion smoothness drops slightly. On Quest with reprojection
    /// it's usually acceptable. Test in-headset to confirm.
    ///
    /// Drop this on a GameObject in your bootstrap scene. The setting is
    /// global — only one instance needed per app.
    /// </summary>
    [DisallowMultipleComponent]
    public class OnDemandRenderingController : MonoBehaviour
    {
        [Header("Render Frequency")]
        [Tooltip("Render every Nth frame. 1 = every frame, 2 = every other, 3 = every third.")]
        [Range(1, 4)]
        [SerializeField] private int _renderFrameInterval = 2;

        [Header("Lifecycle")]
        [Tooltip("Re-apply on quality level change (some quality levels reset frame interval).")]
        [SerializeField] private bool _reapplyOnQualityChanged = true;

        [Tooltip("Log applied state to the console.")]
        [SerializeField] private bool _verbose = true;

        [Header("Debug Overlay")]
        [Tooltip("Show current effective render rate as on-screen text.")]
        [SerializeField] private bool _showOverlay = false;

        private string _statusText = "[OnDemand] Initializing...";

        private void OnEnable()
        {
            if (_reapplyOnQualityChanged)
                QualitySettings.activeQualityLevelChanged += OnQualityChanged;
        }

        private void OnDisable()
        {
            if (_reapplyOnQualityChanged)
                QualitySettings.activeQualityLevelChanged -= OnQualityChanged;
        }

        private void Awake() => Apply();

        private void OnQualityChanged(int previous, int current) => Apply();

        public void Apply()
        {
            int n = Mathf.Max(1, _renderFrameInterval);
            OnDemandRendering.renderFrameInterval = n;

            float effectiveRate = (float)Application.targetFrameRate / n;
            _statusText = $"[OnDemand] interval={n} (renders 1 of every {n} frames)";

            if (_verbose) Debug.Log(_statusText);
        }

        /// <summary>UnityEvent-friendly setter for runtime sliders / settings menus.</summary>
        public void SetInterval(int interval)
        {
            _renderFrameInterval = Mathf.Clamp(interval, 1, 4);
            Apply();
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;

            var prev = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(11, 81, 1200, 60), _statusText);
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, 80, 1200, 60), _statusText);
            GUI.color = prev;
        }
    }
}
