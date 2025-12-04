using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.XR.CoreUtils;

namespace MetaFrame.Data
{
    public class DataSource_Gaze : DataSourceBase<DataSource_Gaze.DataStructure, DataSource_Gaze.RecordingConfig>
    {
        [SerializeField] private OVREyeGaze _leftEyeGaze;
        [SerializeField] private OVREyeGaze _rightEyeGaze;
        [SerializeField] private GameObject _centerEyeGaze;

        public override string SourceName => "Gaze";

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _leftEyeGaze, _rightEyeGaze, _centerEyeGaze);
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Gaze Data Structure - Clean property-based access for consistent static typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Gaze _source;
            private readonly OVREyeGaze _leftEyeGaze;
            private readonly OVREyeGaze _rightEyeGaze;
            private readonly GameObject _centerEyeGaze;

            public DataStructure(DataSource_Gaze source, OVREyeGaze leftEyeGaze, OVREyeGaze rightEyeGaze, GameObject centerEyeGaze)
            {
                _source = source;
                _leftEyeGaze = leftEyeGaze;
                _rightEyeGaze = rightEyeGaze;
                _centerEyeGaze = centerEyeGaze;
            }

            // Eye Gaze Data Properties
            public GazeData LeftEye => GetSingleEyeData(_leftEyeGaze?.transform);
            public GazeData RightEye => GetSingleEyeData(_rightEyeGaze?.transform);
            public GazeData CombinedEye => GetGazeData(_centerEyeGaze?.transform);
            public GazeData Head => GetGazeData(_source.dataManager?.Body?.Data?.Head);
            public GazeData Chest => GetGazeData(_source.dataManager?.Body?.Data?.Chest);

            // Nested GazeData class for structured gaze information
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


            // Helper method to get single eye gaze data with null safety
            private GazeData GetSingleEyeData(Transform gazeTransform)
            {
                if (gazeTransform == null)
                    return null;

                try
                {
                    Vector3? position = gazeTransform.GetWorldPose().position;
                    Quaternion? rotation = gazeTransform.localRotation;
                    Vector3? forward = gazeTransform.forward;

                    var data = new GazeData(position, rotation, forward, null);
                    return data.IsAllNull ? null : data;
                }
                catch
                {
                    return null;
                }
            }
            
            // Helper method to get gaze data with null safety
            private GazeData GetGazeData(Transform gazeTransform)
            {
                if (gazeTransform == null)
                    return null;

                try
                {
                    Vector3? position = gazeTransform.position;
                    Quaternion? rotation = gazeTransform.rotation;
                    Vector3? forward = gazeTransform.forward;
                    Vector3? point = GetGazePoint(gazeTransform);

                    var data = new GazeData(position, rotation, forward, point);
                    return data.IsAllNull ? null : data;
                }
                catch
                {
                    return null;
                }
            }

            // Helper method to get gaze point via raycast (update after merge)
            private Vector3? GetGazePoint(Transform gazeTransform)
            {
                if (gazeTransform == null)
                    return null;

                try
                {
                    RaycastHit hit;
                    if (Physics.Raycast(gazeTransform.position, gazeTransform.forward, out hit, Mathf.Infinity))
                    {
                        return hit.point;
                    }
                }
                catch
                {
                    // Raycast failed, return null
                }

                return null;
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
            public bool CombinedEye = true;
            [Tooltip("Head gaze data")]
            public bool Head = true;
            [Tooltip("Chest gaze data")]
            public bool Chest = true;
        }
        
        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (RecordConfig.LeftEye) data["leftEye"] = GetGazeDataDictionary(Data.LeftEye);
            if (RecordConfig.RightEye) data["rightEye"] = GetGazeDataDictionary(Data.RightEye);
            if (RecordConfig.CombinedEye) data["combinedEye"] = GetGazeDataDictionary(Data.CombinedEye);
            if (RecordConfig.Head) data["head"] = GetGazeDataDictionary(Data.Head);
            if (RecordConfig.Chest) data["chest"] = GetGazeDataDictionary(Data.Chest);

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
    }
}