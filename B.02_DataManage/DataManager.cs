using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using static Oculus.Interaction.Input.TrackingToWorldTransformerOVR;
using Oculus.Interaction.PoseDetection;
using MetaFrame.Interaction;

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
        [BoxGroup("DataSource Config")][SerializeField] internal DataSource_Eyes Eyes;

        // Plugin architecture for extensibility
        internal List<IDataSource> _dataSources = new List<IDataSource>();
        
        // Direct access to other data structures
        public DataSource_FACS.DataStructure FACSData => FACS?.Data;
        public DataSource_Body.DataStructure BodyData => Body?.Data;
        public DataSource_Hand.DataStructure HandData => Hand?.Data;
        public DataSource_Eyes.DataStructure EyesData => Eyes?.Data;

        protected virtual void Start()
        {
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
            if (Eyes != null) Eyes.Initialize(this);
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
    }
}