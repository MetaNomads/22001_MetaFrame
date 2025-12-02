using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaNomads.Data
{
    public class DataSource_Gaze : DataSourceBase<DataSource_Gaze.DataStructure, DataSource_Gaze.RecordingConfig>
    {

        public override string SourceName => "Eyes";

        [SerializeField] internal Gaze _ovrGaze;

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _ovrGaze);
        }



        public override Dictionary<string, object> CollectData()
        {

            // Debug.Log("Eye Referenced");
            var data = new Dictionary<string, object>();

            //In case there is a way to ensure the eye tracker is functional, add it here.
            if (true)
            {
                //Left Eye Vector
                if (RecordConfig.LeftEye)
                {
                    data["LeftEye"] = GetDataWithGaze(Data.LeftEyePosition, Data.LeftEyeGazeRotation, Data.LeftEyeForward);
                }
                if (RecordConfig.RightEye)
                {
                    data["RightEye"] = GetDataWithGaze(Data.RightEyePostion, Data.RightEyeGazeRotation, Data.RightEyeForward);
                }
                if (RecordConfig.CombinedEye)
                {
                    data["CombinedEye"] = GetDataWithGaze(Data.CombinedEyePosition, Data.CombinedEyeGazeRotation, Data.CombinedEyeForward);
                }
                if (RecordConfig.Head)
                {
                    data["Head"] = GetDataWithGaze(Data.HeadPosition, Data.HeadRotation, Data.HeadForward);
                }
                if (RecordConfig.Chest)
                {
                    data["Chest"] = GetDataWithGaze(Data.ChestPosition, Data.ChestRotation, Data.ChestForward);
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
            private readonly DataSource_Gaze _source;
            private readonly Gaze _ovrGaze;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Gaze source, Gaze OVRGaze)
            {
                _source = source;
                _ovrGaze = OVRGaze;
            }

            //Left Eye
            public Vector3 LeftEyePosition => _ovrGaze.GetEyePosition(Gaze.GazeData.Left);
            public Quaternion LeftEyeGazeRotation => _ovrGaze.GetGazeRotation(Gaze.GazeData.Left);
            public Vector3 LeftEyeForward => _ovrGaze.GetGazeForward(Gaze.GazeData.Left);

            //Right Eye
            public Vector3 RightEyePostion => _ovrGaze.GetEyePosition(Gaze.GazeData.Right);
            public Quaternion RightEyeGazeRotation => _ovrGaze.GetGazeRotation(Gaze.GazeData.Right);
            public Vector3 RightEyeForward => _ovrGaze.GetGazeForward(Gaze.GazeData.Right);

            //Combined Eyes
            public Vector3 CombinedEyePosition => _ovrGaze.GetCombinedEyePosition();
            public Quaternion CombinedEyeGazeRotation => _ovrGaze.GetCombinedGazeRotation();
            public Vector3 CombinedEyeForward => _ovrGaze.GetCombinedGazeForward();

            //Chest
            public Vector3 ChestPosition => _ovrGaze.GetChestPosition();
            public Quaternion ChestRotation => _ovrGaze.GetChestRotation();
            public Vector3 ChestForward => _ovrGaze.GetChestForward();

            //Head
            public Vector3 HeadPosition => _ovrGaze.GetHeadPosition();
            public Quaternion HeadRotation => _ovrGaze.GetHeadRotation();
            public Vector3 HeadForward => _ovrGaze.GetHeadForward();
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
            [Header("Body Options")]
            [Tooltip("Chest")]
            public bool Chest = true;
            [Tooltip("Head")]
            public bool Head = true;
        }
    }
}

