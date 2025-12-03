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
                    data["LeftEye"] = GetDataWithGaze(Data.LeftEyePosition, Data.LeftGazeRotation, Data.LeftGazeForward);
                }
                if (RecordConfig.RightEye)
                {
                    data["RightEye"] = GetDataWithGaze(Data.RightEyePostion, Data.RightGazeRotation, Data.RightGazeForward);
                }
                if (RecordConfig.CombinedEye)
                {
                    data["CombinedEye"] = GetDataWithGaze(Data.CombinedEyePosition, Data.CombinedGazeRotation, Data.CombinedGazeForward);
                }
                if (RecordConfig.Head)
                {
                    data["Head"] = GetDataWithGaze(Data.HeadPosition, Data.HeadGazeRotation, Data.HeadGazeForward);
                }
                if (RecordConfig.Chest)
                {
                    data["Chest"] = GetDataWithGaze(Data.ChestPosition, Data.ChestGazeRotation, Data.ChestGazeForward);
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
            public Vector3? LeftEyePosition => _ovrGaze.GetEyePosition(Gaze.GazeData.Left);
            public Quaternion? LeftGazeRotation => _ovrGaze.GetGazeRotation(Gaze.GazeData.Left);
            public Vector3? LeftGazeForward => _ovrGaze.GetGazeForward(Gaze.GazeData.Left);

            //Right Eye
            public Vector3? RightEyePostion => _ovrGaze.GetEyePosition(Gaze.GazeData.Right);
            public Quaternion? RightGazeRotation => _ovrGaze.GetGazeRotation(Gaze.GazeData.Right);
            public Vector3? RightGazeForward => _ovrGaze.GetGazeForward(Gaze.GazeData.Right);

            //Combined Eyes
            public Vector3? CombinedEyePosition => _ovrGaze.GetCombinedEyePosition();
            public Quaternion? CombinedGazeRotation => _ovrGaze.GetCombinedGazeRotation();
            public Vector3? CombinedGazeForward => _ovrGaze.GetCombinedGazeForward();

            //Chest
            public Vector3? ChestPosition => _ovrGaze.GetChestPosition();
            public Quaternion? ChestGazeRotation => _ovrGaze.GetChestRotation();
            public Vector3? ChestGazeForward => _ovrGaze.GetChestForward();

            //Head
            public Vector3? HeadPosition => _ovrGaze.GetHeadPosition();
            public Quaternion? HeadGazeRotation => _ovrGaze.GetHeadRotation();
            public Vector3? HeadGazeForward => _ovrGaze.GetHeadForward();
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

        /// <summary>
        // Utility for extracting position, rotation, and forward for the data source gaze
        /// <summary>
        protected object GetDataWithGaze(Vector3? position, Quaternion? rotation, Vector3? forward)
        {
            return new
            {
                Position = position.HasValue
                    ? new float[] { position.Value.x, position.Value.y, position.Value.z }
                    : null,
                Rotation = rotation.HasValue
                    ? new float[] { rotation.Value.x, rotation.Value.y, rotation.Value.z, rotation.Value.w }
                    : null,
                Forward = forward.HasValue
                    ? new float[] { forward.Value.x, forward.Value.y, forward.Value.z }
                    : null
            };
        }

    }
}

