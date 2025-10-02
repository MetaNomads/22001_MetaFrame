using System;
using System.Collections.Generic;
using UnityEngine;




namespace MetaFrame.Data
{
    public class DataSource_Eyes : DataSourceBase<DataSource_Eyes.DataStructure, DataSource_Eyes.RecordingConfig>
    {

        public override string SourceName => "Eyes";

        [SerializeField] internal OVREyes _ovrEyes;

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _ovrEyes);
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
            private readonly OVREyes _ovrEyes;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Eyes source, OVREyes ovrEyes)
            {
                _source = source;
                _ovrEyes = ovrEyes;
            }

            //Left Eye
            public Transform LeftEyeTransform => _ovrEyes.GetEyeTransform(OVREyes.Eye.Left);
            public Vector3 LeftEyeGazeVector => _ovrEyes.GetGazeVector(OVREyes.Eye.Left);


            //Right Eye
            public Transform RightEyeTransform => _ovrEyes.GetEyeTransform(OVREyes.Eye.Right);
            public Vector3 RightEyeGazeVector => _ovrEyes.GetGazeVector(OVREyes.Eye.Right);


            //Combined Eyes
            public Vector3 CombinedEyePosition => _ovrEyes.CalculateCombinedEyePosition();
            public Vector3 CombinedEyeGazeVector => _ovrEyes.CalculateCombinedGazeVector();


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






    }



}

