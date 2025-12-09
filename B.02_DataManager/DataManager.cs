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
using MetaNomads.Data;

namespace MetaFrame.Data
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
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_Interactable Interactable;


        // Plugin architecture for extensibility
        internal List<IDataSource> _dataSources = new List<IDataSource>();
        
        // Direct access to other data structures
        public DataSource_FACS.DataStructure FACSData => FACS?.Data;
        public DataSource_Body.DataStructure BodyData => Body?.Data;
        public DataSource_Hand.DataStructure HandData => Hand?.Data;
        public DataSource_Gaze.DataStructure GazeData => Gaze?.Data;
        public DataSource_Interactable.DataStructure InteractableData => Interactable?.Data;

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
            if (Interactable !=  null) Interactable.Initialize(this);
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
            if (Gaze.Data.LeftEye != null)
            {
                Debug.Log($"Data Test - Left Eye Position: {Gaze.Data.LeftEye.Position}");
                Debug.Log($"Data Test - Left Eye Rotation: {Gaze.Data.LeftEye.Rotation?.eulerAngles}");
                Debug.Log($"Data Test - Left Eye Forward: {Gaze.Data.LeftEye.GazeForward}");
            }
        }
    }
}