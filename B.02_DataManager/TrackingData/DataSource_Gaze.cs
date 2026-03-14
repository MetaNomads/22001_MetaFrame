using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.XR.CoreUtils;
using MetaFrame.Interaction;

namespace MetaFrame.Data
{
    public class DataSource_Gaze : DataSourceBase<DataSource_Gaze.DataStructure, DataSource_Gaze.RecordingConfig>
    {
        [SerializeField] private GazePose _gazePose;

        public override string SourceName => "Gaze";

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _gazePose);
        }

        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (RecordConfig.LeftEye) data["leftEye"] = GetGazeDataDictionary(Data.LeftEye);
            if (RecordConfig.RightEye) data["rightEye"] = GetGazeDataDictionary(Data.RightEye);
            if (RecordConfig.CenterGaze) data["centerGaze"] = GetGazeDataDictionary(Data.CenterGaze);
            if (RecordConfig.HeadGaze) data["headGaze"] = GetGazeDataDictionary(Data.HeadGaze);
            if (RecordConfig.ChestGaze) data["chestGaze"] = GetGazeDataDictionary(Data.ChestGaze);

            return data;
        }

        /// <summary>
        /// Convert GazeData to dictionary with proper Unity type conversion
        /// </summary>
        private Dictionary<string, object> GetGazeDataDictionary(DataStructure.GazeData gazeData)
        {
            if (gazeData == null) return null;

            var dict = new Dictionary<string, object>();

            if (gazeData.Position.HasValue)
            {
                var pos = gazeData.Position.Value;
                dict["Position"] = new float[] { pos.x, pos.y, pos.z };
            }

            if (gazeData.Rotation.HasValue)
            {
                var rot = gazeData.Rotation.Value;
                dict["Rotation"] = new float[] { rot.x, rot.y, rot.z, rot.w };
            }

            if (gazeData.GazeForward.HasValue)
            {
                var fwd = gazeData.GazeForward.Value;
                dict["GazeForward"] = new float[] { fwd.x, fwd.y, fwd.z };
            }

            if (gazeData.GazePoint.HasValue)
            {
                var point = gazeData.GazePoint.Value;
                dict["GazePoint"] = new float[] { point.x, point.y, point.z };
            }

            return dict.Count > 0 ? dict : null;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Gaze Data Structure - Clean property-based access for consistent static typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Gaze _source;
            private readonly GazePose _gazePose;

            public DataStructure(DataSource_Gaze source, GazePose gazePose)
            {
                _source = source;
                _gazePose = gazePose;
            }

            // Eye Gaze Data Properties
            public GazeData LeftEye => GetSingleEyeData(_gazePose?.LeftEye);
            public GazeData RightEye => GetSingleEyeData(_gazePose?.RightEye);
            public GazeData CenterGaze => GetGazePoseData(_gazePose?.CenterGaze);
            public GazeData HeadGaze => GetGazePoseData(_gazePose?.HeadGaze);
            public GazeData ChestGaze => GetGazePoseData(_gazePose?.ChestGaze);

            /// <summary>
            /// Nested GazeData class for structured gaze information
            /// </summary>
            public class GazeData
            {
                public Vector3? Position { get; }
                public Quaternion? Rotation { get; }
                public Vector3? GazeForward { get; }
                public Vector3? GazePoint { get; }

                public GazeData(Vector3? position, Quaternion? rotation, Vector3? forward, Vector3? point)
                {
                    Position = position;
                    Rotation = rotation;
                    GazeForward = forward;
                    GazePoint = point;
                }

                public bool IsAllNull =>
                    Position == null && Rotation == null && GazeForward == null && GazePoint == null;
            }

            /// <summary>
            /// Helper method to get single eye gaze data (no raycast point)
            /// </summary>
            private GazeData GetSingleEyeData(Transform eyeTransform)
            {
                if (eyeTransform == null) return null;

                try
                {
                    Vector3? position = eyeTransform.position;
                    Quaternion? rotation = eyeTransform.rotation;
                    Vector3? forward = eyeTransform.forward;

                    var data = new GazeData(position, rotation, forward, null);
                    return data.IsAllNull ? null : data;
                }
                catch
                {
                    return null;
                }
            }
            
            /// <summary>
            /// Helper method to get gaze pose data (includes raycast point)
            /// </summary>
            private GazeData GetGazePoseData(GazePose.GazePoseData gazePoseData)
            {
                if (gazePoseData == null) return null;

                try
                {
                    var transform = gazePoseData.GetTransform();
                    if (transform == null) return null;

                    Vector3? position = transform.position;
                    Quaternion? rotation = transform.rotation;
                    Vector3? forward = transform.forward;
                    Vector3? point = gazePoseData.GetGazePoint();

                    var data = new GazeData(position, rotation, forward, point);
                    return data.IsAllNull ? null : data;
                }
                catch
                {
                    return null;
                }
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Gaze Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Tooltip("Left Eye gaze data")]
            public bool LeftEye = true;
            [Tooltip("Right Eye gaze data")]
            public bool RightEye = true;
            [Tooltip("Combined Eyes gaze data")]
            public bool CenterGaze = true;
            [Tooltip("Head gaze data")]
            public bool HeadGaze = true;
            [Tooltip("Chest gaze data")]
            public bool ChestGaze = true;
        }
    }
}