using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.Interaction.SkeletonInteraction
{
    /// <summary>
    /// Visualizes an IBody skeleton with per-camera visibility control
    /// and independent size controls for bones, joints, and axes.
    ///
    /// Manages its own PolylineRenderer (local copy) so that RenderLines()
    /// can be called with a specific Camera, restricting the draw to only
    /// the Game window, only the Scene view, or both.
    /// </summary>
    public class SkeletonVisualizor : Oculus.Interaction.SkeletonDebugGizmos
    {
        // =========================================================================
        // Enums
        // =========================================================================

        public enum CoordSpace { World, Local }

        public enum VisibilityMode
        {
            Disabled,
            PlayerOnly,
            EditorOnly,
            Both,
        }

        // =========================================================================
        // Inspector fields
        // =========================================================================

        [SerializeField, Interface(typeof(IBody))]
        private UnityEngine.Object _body;
        private IBody Body;

        [Tooltip("World: draws at the world body location. " +
                 "Local: draws relative to this transform.")]
        [SerializeField]
        private CoordSpace _space = CoordSpace.World;

        [Tooltip("Which cameras see the skeleton visualization.")]
        [SerializeField]
        private VisibilityMode _visibilityMode = VisibilityMode.EditorOnly;

        [Tooltip("Thickness of the bone lines connecting joints.")]
        [SerializeField, Min(0.001f)]
        private float _boneThickness = 0.01f;

        [Tooltip("Radius of the joint spheres.")]
        [SerializeField, Min(0.001f)]
        private float _jointRadius = 0.02f;

        [Tooltip("Length and line width of the joint orientation axes.")]
        [SerializeField, Min(0.001f)]
        private float _axesSize = 0.02f;

        // =========================================================================
        // Properties
        // =========================================================================

        public CoordSpace Space
        {
            get => _space;
            set => _space = value;
        }

        public VisibilityMode VisMode
        {
            get => _visibilityMode;
            set => _visibilityMode = value;
        }

        // =========================================================================
        // Runtime state
        // =========================================================================

        protected bool _started = false;

        // Segment lists fed into our own PolylineRenderer each frame.
        private List<Vector4> _points = new List<Vector4>();
        private List<Color>   _colors = new List<Color>();
        private int           _segmentCount = 0;
        private bool          _segmentsDirty = false;

        // Our own renderer — bypasses DebugGizmos so we control which camera sees it.
        private PolylineRenderer _polylineRenderer;

        // =========================================================================
        // Lifecycle
        // =========================================================================

        protected virtual void Awake()
        {
            Body = _body as IBody;
            _polylineRenderer = new PolylineRenderer(null, DebugGizmos.RenderSinglePass);
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            Assert.IsNotNull(Body);
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started)
                Body.WhenBodyUpdated += HandleBodyUpdated;

            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        protected virtual void OnDisable()
        {
            if (_started)
                Body.WhenBodyUpdated -= HandleBodyUpdated;

            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        }

        protected virtual void OnDestroy()
        {
            _polylineRenderer?.Cleanup();
        }

        // =========================================================================
        // Body update — rebuild segments when new pose arrives
        // =========================================================================

        private void HandleBodyUpdated()
        {
            BuildSegments();
        }

        private void BuildSegments()
        {
            _segmentCount  = 0;
            _segmentsDirty = false;

            VisibilityFlags flags = GetModifiedDrawFlags();

            foreach (BodyJointId joint in Body.SkeletonMapping.Joints)
            {
                if (!TryGetJointPose((int)joint, out Pose pose)) continue;

                if (flags.HasFlag(VisibilityFlags.Axes))
                {
                    float lw = _axesSize * 0.5f;
                    AddSegment(pose.position, pose.position + pose.rotation * Vector3.right   * _axesSize, lw, Color.red,   Color.red);
                    AddSegment(pose.position, pose.position + pose.rotation * Vector3.up      * _axesSize, lw, Color.green, Color.green);
                    AddSegment(pose.position, pose.position + pose.rotation * Vector3.forward * _axesSize, lw, Color.blue,  Color.blue);
                }

                if (flags.HasFlag(VisibilityFlags.Joints))
                    AddSegment(pose.position, pose.position, _jointRadius, JointColor, JointColor);

                if (flags.HasFlag(VisibilityFlags.Bones)
                    && TryGetParentJointId((int)joint, out int parent)
                    && TryGetJointPose(parent, out Pose parentPose))
                {
                    AddSegment(pose.position, parentPose.position, _boneThickness, BoneColor, BoneColor);
                }
            }

            // Upload new geometry to the renderer once per body update.
            _polylineRenderer.SetLines(_points, _colors, _segmentCount);
            _segmentsDirty = true;
        }

        private void AddSegment(Vector3 p0, Vector3 p1, float width, Color c0, Color c1)
        {
            while (_segmentCount + 2 > _points.Count)
            {
                _points.Add(Vector4.zero);
                _colors.Add(Color.white);
            }
            _points[_segmentCount]     = new Vector4(p0.x, p0.y, p0.z, width);
            _points[_segmentCount + 1] = new Vector4(p1.x, p1.y, p1.z, width);
            _colors[_segmentCount]     = c0;
            _colors[_segmentCount + 1] = c1;
            _segmentCount += 2;
        }

        // =========================================================================
        // Camera callback — render only for the matching camera type
        // =========================================================================

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!_segmentsDirty)           return;
            if (!ShouldDrawForCamera(cam)) return;

            _polylineRenderer.RenderLines(cam);

            // FIX: clear the dirty flag after the first successful render call.
            // Previously it was never cleared here, so RenderLines fired for every
            // camera pass in the scene (both XR eyes, reflection probes, shadow
            // cameras, scene view) rather than just the first matching one per
            // body-tracking update. Each call submits a DrawMeshInstancedIndirect
            // to the GPU, multiplying draw call cost proportionally to how many
            // camera passes exist. _segmentsDirty is re-set to true by
            // BuildSegments() on the next body tracking update, so rendering
            // resumes correctly on the following frame.
            _segmentsDirty = false;
        }

        private bool ShouldDrawForCamera(Camera cam)
        {
            switch (_visibilityMode)
            {
                case VisibilityMode.PlayerOnly: return cam.cameraType == CameraType.Game;
                case VisibilityMode.EditorOnly: return cam.cameraType == CameraType.SceneView;
                case VisibilityMode.Both:       return cam.cameraType == CameraType.Game
                                                    || cam.cameraType == CameraType.SceneView;
                default: return false;
            }
        }

        // =========================================================================
        // SkeletonDebugGizmos overrides
        // =========================================================================

        protected override bool TryGetJointPose(int jointId, out Pose pose)
        {
            switch (_space)
            {
                case CoordSpace.Local:
                    bool result = Body.GetJointPoseFromRoot((BodyJointId)jointId, out pose);
                    pose.position = transform.TransformPoint(pose.position);
                    pose.rotation = transform.rotation * pose.rotation;
                    return result;
                default:
                    return Body.GetJointPose((BodyJointId)jointId, out pose);
            }
        }

        protected override bool TryGetParentJointId(int jointId, out int parent)
        {
            if (Body.SkeletonMapping.TryGetParentJointId(
                (BodyJointId)jointId, out BodyJointId parentJointId))
            {
                parent = (int)parentJointId;
                return true;
            }
            parent = default;
            return false;
        }

        private VisibilityFlags GetModifiedDrawFlags()
        {
            VisibilityFlags flags = base.Visibility;
            if (HasNegativeScale && Space == CoordSpace.Local)
                flags &= ~VisibilityFlags.Axes;
            return flags;
        }

        // =========================================================================
        // Inject
        // =========================================================================

        public void InjectAllBodyJointDebugGizmos(IBody body) => InjectBody(body);

        public void InjectBody(IBody body)
        {
            _body = body as UnityEngine.Object;
            Body  = body;
        }
    }

    // =========================================================================
    // Custom Editor — hides inherited _radius field (unused)
    // =========================================================================

#if UNITY_EDITOR
    [CustomEditor(typeof(SkeletonVisualizor))]
    public class SkeletonVisualizorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true);

            while (prop.NextVisible(false))
            {
                if (prop.name == "_radius") continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}
