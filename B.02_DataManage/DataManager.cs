using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using static Oculus.Interaction.Input.TrackingToWorldTransformerOVR;
using Oculus.Interaction.PoseDetection;
using MetaNomads.Interaction;
using System.Collections;

namespace MetaNomads.Data
{   
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
        public DataSource_Gaze.DataStructure EyesData => Gaze?.Data;

        protected virtual void Start()
        {
            StartCoroutine(TestDebugLogs());
            // targetVerticalVector = OffsetVectorWithRotation(GetVerticalVector());
            InitializeDataSources();
        }

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
        private IEnumerator TestDebugLogs()
        {
            // Wait for data to be valid
            yield return new WaitUntil(() => Gaze != null && Body?.Data?.Head != null);
            yield return new WaitForSeconds(0.5f); // Extra delay to ensure everything is ready

            // Left Eye
            Debug.Log($"Data Test - Left Eye Position: {Gaze.Data.LeftEyePosition}");
            Debug.Log($"Data Test - Left Eye Rotation: {Gaze.Data.LeftEyeGazeRotation.eulerAngles}");
            Debug.Log($"Data Test - Left Eye Forward: {Gaze.Data.LeftEyeForward}");

            // Right Eye
            Debug.Log($"Data Test - Right Eye Position: {Gaze.Data.RightEyePostion}");
            Debug.Log($"Data Test - Right Eye Rotation: {Gaze.Data.RightEyeGazeRotation.eulerAngles}");
            Debug.Log($"Data Test - Right Eye Forward: {Gaze.Data.RightEyeForward}");

            // Combined Eye
            Debug.Log($"Data Test - Combined Eye Position: {Gaze.Data.CombinedEyePosition}");
            Debug.Log($"Data Test - Combined Eye Rotation: {Gaze.Data.CombinedEyeGazeRotation.eulerAngles}");
            Debug.Log($"Data Test - Combined Eye Forward: {Gaze.Data.CombinedEyeForward}");

            // Head
            Debug.Log($"Data Test - Head Position: {Body.Data.Head.position}");
            Debug.Log($"Data Test - Head Rotation: {Body.Data.Head.rotation.eulerAngles}");
            Debug.Log($"Data Test - Head Forward: {Body.Data.Head.forward}");

            // Chest
            Debug.Log($"Data Test - Chest Position: {Body.Data.Head.position}");
            Debug.Log($"Data Test - Chest Rotation: {Body.Data.Head.rotation.eulerAngles}");
            Debug.Log($"Data Test - Chest Forward: {Body.Data.Head.forward}");
        }
    }
}