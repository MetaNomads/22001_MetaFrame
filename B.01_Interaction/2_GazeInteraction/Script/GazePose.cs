using MetaFrame.Data;
using MetaFrame.Interaction.GazeInteraction;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaFrame.Interaction
{
    public class GazePose : MonoBehaviour
    {
        // =========================================================================
        // Inspector fields
        // =========================================================================

        [SerializeField] private DataManager _dataManager;

        [Header("Eye Transforms")]
        [SerializeField] private Transform _leftEye;
        [SerializeField] private Transform _rightEye;

        [Header("Gaze Poses")]
        [SerializeField] private Transform _centerGazeTransform;
        [SerializeField] private GazeInteractor _centerGazeInteractor;

        [SerializeField] private Transform _headGazeTransform;
        [SerializeField] private GazeInteractor _headGazeInteractor;

        [SerializeField] private Transform _chestGazeTransform;
        [SerializeField] private GazeInteractor _chestGazeInteractor;

        [Header("Debug Ray Visualization")]
        [SerializeField] private VisibilityMode _rayVisibility = VisibilityMode.EditorOnly;
        [SerializeField] private float _rayLength = 2f;
        [SerializeField] private float _rayWidth = 0.005f;
        [SerializeField] private Color _centerRayColor = Color.cyan;
        [SerializeField] private Color _headRayColor = Color.green;
        [SerializeField] private Color _chestRayColor = Color.yellow;

        // =========================================================================
        // Enums
        // =========================================================================

        public enum VisibilityMode { Disabled, PlayerOnly, EditorOnly, Both }

        // =========================================================================
        // Public accessors
        // =========================================================================

        public Transform LeftEye => _leftEye;
        public Transform RightEye => _rightEye;
        public GazePoseData CenterGaze { get; private set; }
        public GazePoseData HeadGaze { get; private set; }
        public GazePoseData ChestGaze { get; private set; }

        public VisibilityMode RayVisibility
        {
            get => _rayVisibility;
            set => _rayVisibility = value;
        }

        // =========================================================================
        // UnityEvent-accessible methods
        // =========================================================================

        /// <summary>
        /// Sets the ray visibility mode. Accepts the int value of VisibilityMode
        /// (0 = Disabled, 1 = PlayerOnly, 2 = EditorOnly, 3 = Both).
        /// Use this for dynamic UnityEvent bindings that pass an int argument.
        /// </summary>
        public void SetRayVisibility(int mode)
        {
            if (System.Enum.IsDefined(typeof(VisibilityMode), mode))
                _rayVisibility = (VisibilityMode)mode;
            else
                Debug.LogWarning($"[GazePose] Invalid VisibilityMode value: {mode}");
        }

        /// <summary> Hides the rays in all cameras. </summary>
        public void SetRayVisibilityDisabled() => _rayVisibility = VisibilityMode.Disabled;

        /// <summary> Shows the rays only in the in-game (Player) view. </summary>
        public void SetRayVisibilityPlayerOnly() => _rayVisibility = VisibilityMode.PlayerOnly;

        /// <summary> Shows the rays only in the Scene view (editor). </summary>
        public void SetRayVisibilityEditorOnly() => _rayVisibility = VisibilityMode.EditorOnly;

        /// <summary> Shows the rays in both the Scene view and the Player view. </summary>
        public void SetRayVisibilityBoth() => _rayVisibility = VisibilityMode.Both;

        /// <summary>
        /// Toggles rays on/off in the Player view. When turning on,
        /// restores to PlayerOnly; when turning off, sets Disabled.
        /// </summary>
        public void ToggleRayVisibility()
        {
            _rayVisibility = _rayVisibility == VisibilityMode.Disabled
                ? VisibilityMode.PlayerOnly
                : VisibilityMode.Disabled;
        }

        /// <summary>
        /// Bool overload for UnityEvents that pass a bool
        /// (e.g. Toggle.onValueChanged). True = PlayerOnly, False = Disabled.
        /// </summary>
        public void SetRaysVisible(bool visible)
        {
            _rayVisibility = visible ? VisibilityMode.PlayerOnly : VisibilityMode.Disabled;
        }

        // =========================================================================
        // Runtime state
        // =========================================================================

        private LineRenderer _centerLine;
        private LineRenderer _headLine;
        private LineRenderer _chestLine;

        // =========================================================================
        // Lifecycle
        // =========================================================================

        void Awake()
        {
            CenterGaze = new GazePoseData(_centerGazeTransform, _centerGazeInteractor, UpdateCenterGaze);
            HeadGaze = new GazePoseData(_headGazeTransform, _headGazeInteractor, UpdateHeadGaze);
            ChestGaze = new GazePoseData(_chestGazeTransform, _chestGazeInteractor, UpdateChestGaze);

            _centerLine = CreateLineRenderer("GazeRay_Center", _centerRayColor);
            _headLine = CreateLineRenderer("GazeRay_Head", _headRayColor);
            _chestLine = CreateLineRenderer("GazeRay_Chest", _chestRayColor);

            SetLinesEnabled(false);
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        void LateUpdate()
        {
            CenterGaze?.UpdatePose();
            HeadGaze?.UpdatePose();
            ChestGaze?.UpdatePose();

            // Positions are always updated on the main thread before rendering.
            // Visibility is toggled per-camera in OnBeginCamera/OnEndCamera.
            UpdateLine(_centerLine, CenterGaze);
            UpdateLine(_headLine, HeadGaze);
            UpdateLine(_chestLine, ChestGaze);
        }

        // =========================================================================
        // Camera visibility callbacks
        // =========================================================================

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            bool isScene = cam.cameraType == CameraType.SceneView;
            bool isGame = cam.cameraType == CameraType.Game;

            bool show = _rayVisibility switch
            {
                VisibilityMode.Disabled => false,
                VisibilityMode.EditorOnly => isScene,
                VisibilityMode.PlayerOnly => isGame,
                VisibilityMode.Both => isScene || isGame,
                _ => false,
            };

            SetLinesEnabled(show);
        }

        private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            SetLinesEnabled(false);
        }

        private void SetLinesEnabled(bool value)
        {
            if (_centerLine != null) _centerLine.enabled = value;
            if (_headLine != null) _headLine.enabled = value;
            if (_chestLine != null) _chestLine.enabled = value;
        }

        // =========================================================================
        // LineRenderer helpers
        // =========================================================================

        private LineRenderer CreateLineRenderer(string goName, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startWidth = _rayWidth;
            lr.endWidth = _rayWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;

            return lr;
        }

        private void UpdateLine(LineRenderer lr, GazePoseData gaze)
        {
            if (lr == null || gaze == null) return;

            Transform t = gaze.GetTransform();
            if (t == null) return;

            Vector3 origin = t.position;
            Vector3? hitPoint = gaze.GetGazePoint();
            Vector3 end = hitPoint ?? origin + t.forward * _rayLength;

            lr.SetPosition(0, origin);
            lr.SetPosition(1, end);
        }

        // =========================================================================
        // Nested GazePoseData class
        // =========================================================================

        public class GazePoseData
        {
            private readonly Transform _gazeTransform;
            private readonly GazeInteractor _gazeInteractor;
            private readonly System.Action _updateAction;

            public GazePoseData(Transform gazeTransform, GazeInteractor gazeInteractor, System.Action updateAction)
            {
                _gazeTransform = gazeTransform;
                _gazeInteractor = gazeInteractor;
                _updateAction = updateAction;
            }

            public Transform GetTransform() => _gazeTransform;

            public Vector3? GetGazePoint()
            {
                if (_gazeInteractor == null) return null;
                try { return _gazeInteractor.GetCollisionPoint(); }
                catch { return null; }
            }

            public void UpdatePose() => _updateAction?.Invoke();
        }

        // =========================================================================
        // Gaze update methods
        // =========================================================================

        private void UpdateCenterGaze()
        {
            if (_centerGazeTransform == null || _leftEye == null || _rightEye == null) return;

            try
            {
                var leftPose = _leftEye.GetWorldPose();
                var rightPose = _rightEye.GetWorldPose();
                _centerGazeTransform.position = (leftPose.position + rightPose.position) / 2f;
                _centerGazeTransform.rotation = Quaternion.Slerp(_leftEye.localRotation, _rightEye.localRotation, 0.5f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update center gaze: {e.Message}");
            }
        }

        private void UpdateHeadGaze()
        {
            if (_headGazeTransform == null || _dataManager?.Body?.Data?.Head == null) return;

            try
            {
                var headTransform = _dataManager.Body.Data.Head;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _headGazeTransform.position = headTransform.position;
                _headGazeTransform.rotation = headTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update head gaze: {e.Message}");
            }
        }

        private void UpdateChestGaze()
        {
            if (_chestGazeTransform == null || _dataManager?.Body?.Data?.Chest == null) return;

            try
            {
                var chestTransform = _dataManager.Body.Data.Chest;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _chestGazeTransform.position = chestTransform.position;
                _chestGazeTransform.rotation = chestTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update chest gaze: {e.Message}");
            }
        }
    }
}