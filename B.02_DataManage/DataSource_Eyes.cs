using System;
using System.Collections.Generic;
using UnityEngine;




namespace MetaFrame.Data
{
    public class DataSource_Eyes : DataSourceBase<DataSource_Eyes.DataStructure, DataSource_Eyes.RecordingConfig>
    {

        public override string SourceName => "Eyes";

        [SerializeField] internal OVRGaze _ovrGaze;

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _ovrGaze);
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
                    data["LeftEye"] = GetTransformDataWithGaze(Data.LeftEyeTransform, Data.LeftEyeGazeVector);
                }
                if (RecordConfig.RightEye)
                {
                    data["RightEye"] = GetTransformDataWithGaze(Data.RightEyeTransform, Data.RightEyeGazeVector);
                }
                if (RecordConfig.CombinedEye)
                {
                    data["CombinedEye"] = GetPositionDataWithGaze(Data.CombinedEyePosition, Data.CombinedEyeGazeVector);
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
            private readonly OVRGaze _ovrGaze;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Eyes source, OVRGaze OVRGaze)
            {
                _source = source;
                _ovrGaze = OVRGaze;
            }

            //Left Eye
            public Transform LeftEyeTransform => _ovrGaze.GetEyeTransform(OVRGaze.Eye.Left);
            public Vector3 LeftEyeGazeVector => _ovrGaze.GetGazeVector(OVRGaze.Eye.Left);


            //Right Eye
            public Transform RightEyeTransform => _ovrGaze.GetEyeTransform(OVRGaze.Eye.Right);
            public Vector3 RightEyeGazeVector => _ovrGaze.GetGazeVector(OVRGaze.Eye.Right);


            //Combined Eyes
            public Vector3 CombinedEyePosition => _ovrGaze.CalculateCombinedEyePosition();
            public Vector3 CombinedEyeGazeVector => _ovrGaze.CalculateCombinedGazeVector();


        }






        /*=========================================================================================================================*/
        /// <summary>
        /// Eye Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Eye Options")]
            [Tooltip("Left Eye")]
            public bool LeftEye = true;
            [Tooltip("Right Eye")]
            public bool RightEye = true;
            [Tooltip("Combined Eyes")]
            public bool CombinedEye = true;
        }





        /// <summary>
        // Utility for extracting position data with an additional Gaze Vector
        /// <summary>
        protected object GetPositionDataWithGaze(Vector3 transform, Vector3 gazeVector)
        {
            if (transform == null) return null;
            return new
            {
                Position = new float[] { transform.x, transform.y, transform.z },
                Gaze_Vector = new float[] { gazeVector.x, gazeVector.y, gazeVector.z }
            };
        }
    }
}

