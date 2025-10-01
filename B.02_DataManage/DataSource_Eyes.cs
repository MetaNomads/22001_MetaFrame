using System;
using System.Collections.Generic;
using UnityEngine;




namespace MetaFrame.Data
{
    public class DataSource_Eyes : DataSourceBase<DataSource_Eyes.DataStructure, DataSource_Eyes.RecordingConfig>
    {

        [SerializeField] internal OVRPlugin.EyeGazesState _eyeGazeState;
        public override string SourceName => "Eyes";
        private bool _eyeTrackingFunctional = false;



        private void Start()
        {
            // start eye tracking
            if (!OVRPlugin.StartEyeTracking())
            {
                _eyeTrackingFunctional = false;
                Debug.LogError("Failed to start eye tracking!");
            }
            else
            {
                _eyeTrackingFunctional = true;
            }

         
        }

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _eyeGazeState);
        }



        public override Dictionary<string, object> CollectData()
        {

            Debug.Log("Eye Referenced");
            var data = new Dictionary<string, object>();

            //In case there is a way to ensure the eye tracker is functional, add it here.
            if (true)
            {
                //Left Eye Vector
                if (RecordConfig.LeftEye)
                {
                    data["Left"] = GetPositionData(GazeRayCombined.leftEyeDirectionGazeForRecording);
                }
                if (RecordConfig.RightEye)
                {
                    data["rightEyeDir"] = GetPositionData(GazeRayCombined.rightEyeDirectionGazeForRecording);
                }
                if (RecordConfig.CombinedEye)
                {
                    data["combinedEyeDir"] = GetPositionData(GazeRayCombined.combinedEyeDirectionGazeForRecording);
                }

            }



            return data;
        }






        /*=========================================================================================================================*/
        /// <summary>
        /// Eyes Data Structure - Clean property-based access for consistent typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Eyes _source;
            private readonly OVRPlugin.EyeGazesState _eyeGazesState;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Eyes source, OVRPlugin.EyeGazesState eyeGazes)
            {
                _source = source;
                _eyeGazesState = eyeGazes;
            }

            //Left Eye
            /*
            public Transform LeftEye => Vector3.zero;
            public Vector3 LeftEyeGazeVector => Vector3.zero;


            //Right Eye
            public Vector3 RightEyePosition => Vector3.zero;
            public Quaternion RightEyeRotation => Quaternion.Euler(Vector3.zero);
            public Vector3 RightEyeGazeVector => Vector3.zero;


            //Combined Eyes
            public Vector3 CombinedEyePosition => Vector3.zero;
            public Vector3 CombinedEyeGazeVector => Vector3.zero;

            */

        }






        /*=========================================================================================================================*/
        /// <summary>
        /// Eye Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Eye Options")]
            [Tooltip("LeftEyeDir")]
            public bool LeftEye = true;
            [Tooltip("RightEyeDir")]
            public bool RightEye = true;
            [Tooltip("CombinedEyeDir")]
            public bool CombinedEye = true;
        }






    }



}

