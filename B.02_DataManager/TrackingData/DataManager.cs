using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using static Oculus.Interaction.Input.TrackingToWorldTransformerOVR;
using Oculus.Interaction.PoseDetection;
using MetaFrame.Interaction;
using System.Collections;

namespace MetaFrame.Data
{
    // NOTE on execution order: do NOT add [DefaultExecutionOrder] to this class or to
    // TrackingDataRecorder. OVR's subsystems (OVRManager, OVRFaceExpressions, OVRSkeleton,
    // GazePose) initialize in default Unity order and are sensitive to being preempted —
    // forcing DataManager to run earlier than default caused OVR face/eye/body/hand
    // tracking to fail silently. Default order is the right choice for everything in
    // this chain. Start-order between DataManager and TrackingDataRecorder is handled
    // defensively inside TrackingDataRecorder (see the null/empty check in
    // LogDataSourcesOnce, plus the deferred retry below).
    public class DataManager : MonoBehaviour
    {
        [SerializeField] internal TransformConfig config;

        internal Vector3 targetVerticalVector;

        // Plugin-based data source references
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_Hand Hand;
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_FACS FACS;
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_Body Body;
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_Gaze Gaze;

        // Plugin architecture for extensibility
        internal List<IDataSource> _dataSources = new List<IDataSource>();

        // Direct access to other data structures
        public DataSource_FACS.DataStructure FACSData => FACS?.Data;
        public DataSource_Body.DataStructure BodyData => Body?.Data;
        public DataSource_Hand.DataStructure HandData => Hand?.Data;
        public DataSource_Gaze.DataStructure GazeData => Gaze?.Data;

        protected virtual void Start()
        {
            // targetVerticalVector = OffsetVectorWithRotation(GetVerticalVector());
            InitializeDataSources();
        }

        // FIX: empty Update() removed. Unity invokes Update() on every MonoBehaviour that
        // defines one, even if the body is empty — a few hundred nanoseconds per frame.
        // Trivial by itself, but adds up across many empty Updates in a large project and
        // blocks any future batching optimizations Unity does on script callbacks.
        // If you need per-frame logic later, reintroduce Update() at that point.

        /// <summary>
        /// Initialize all data sources using plugin architecture
        /// </summary>
        private void InitializeDataSources()
        {
            if (Hand != null) Hand.Initialize(this);
            if (FACS != null) FACS.Initialize(this);
            if (Body != null) Body.Initialize(this);
            if (Gaze != null) Gaze.Initialize(this);
        }

        /// <summary>
        /// Register data source for plugin architecture
        /// </summary>
        public void RegisterDataSource(IDataSource dataSource)
        {
            if (!_dataSources.Contains(dataSource))
            {
                _dataSources.Add(dataSource);
            }
        }

        // private IEnumerator TestDebugLogs()
        // {
        //     // Wait for data to be valid
        //     yield return new WaitUntil(() => Gaze != null && Body?.Data?.Head != null);
        //     yield return new WaitForSeconds(0.5f); // Extra delay to ensure everything is ready

        //     // Left Eye
        //     if (Gaze.Data.LeftEye != null && Gaze.Data.RightEye != null)
        //     {
        //         Debug.Log($"Data Test - Left Eye Position: {Gaze.Data.LeftEye.Position}");
        //         Debug.Log($"Data Test - Left Eye Rotation: {Gaze.Data.LeftEye.Rotation?.eulerAngles}");
        //         Debug.Log($"Data Test - Left Eye Forward: {Gaze.Data.LeftEye.GazeForward}");
        //         Debug.DrawRay(Gaze.Data.LeftEye.Position.Value, Gaze.Data.LeftEye.GazeForward.Value * 15f, Color.green);
        //         Debug.Log($"Data Test - Right Eye Position: {Gaze.Data.RightEye.Position}");
        //         Debug.Log($"Data Test - Right Eye Rotation: {Gaze.Data.RightEye.Rotation?.eulerAngles}");
        //         Debug.Log($"Data Test - Right Eye Forward: {Gaze.Data.RightEye.GazeForward}");
        //         Debug.DrawRay(Gaze.Data.RightEye.Position.Value, Gaze.Data.RightEye.GazeForward.Value * 15f, Color.red);
        //     }
        // }
    }
}