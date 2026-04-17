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

            if (RecordConfig.LeftEye)    { var d = Data.LeftEye;    if (d != null) data["leftEye"]    = GetGazeDataDictionary(d); }
            if (RecordConfig.RightEye)   { var d = Data.RightEye;   if (d != null) data["rightEye"]   = GetGazeDataDictionary(d); }
            if (RecordConfig.CenterGaze) { var d = Data.CenterGaze; if (d != null) data["centerGaze"] = GetGazeDataDictionary(d); }
            if (RecordConfig.HeadGaze)   { var d = Data.HeadGaze;   if (d != null) data["headGaze"]   = GetGazeDataDictionary(d); }
            if (RecordConfig.ChestGaze)  { var d = Data.ChestGaze;  if (d != null) data["chestGaze"]  = GetGazeDataDictionary(d); }

            return data;
        }

        private Dictionary<string, object> GetGazeDataDictionary(DataStructure.GazeData gazeData)
        {
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
        /// Gaze Data Structure
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Gaze _source;
            private readonly GazePose _gazePose;

            // FIX: pre-allocated GazeData instances — previously every property access
            // called GetSingleEyeData / GetGazePoseData which did `new GazeData(...)`,
            // creating 5 heap-allocated objects per recording tick (100Hz = 500/sec).
            // Now each property updates and returns its cached instance in-place.
            // This is safe because GazeData is consumed immediately in CollectData
            // and never held across frames.
            private readonly GazeData _leftEyeCache   = new GazeData();
            private readonly GazeData _rightEyeCache  = new GazeData();
            private readonly GazeData _centerGazeCache = new GazeData();
            private readonly GazeData _headGazeCache   = new GazeData();
            private readonly GazeData _chestGazeCache  = new GazeData();

            public DataStructure(DataSource_Gaze source, GazePose gazePose)
            {
                _source   = source;
                _gazePose = gazePose;
            }

            public GazeData LeftEye    => UpdateSingleEyeData(_leftEyeCache,   _gazePose?.LeftEye);
            public GazeData RightEye   => UpdateSingleEyeData(_rightEyeCache,  _gazePose?.RightEye);
            public GazeData CenterGaze => UpdateGazePoseData(_centerGazeCache, _gazePose?.CenterGaze);
            public GazeData HeadGaze   => UpdateGazePoseData(_headGazeCache,   _gazePose?.HeadGaze);
            public GazeData ChestGaze  => UpdateGazePoseData(_chestGazeCache,  _gazePose?.ChestGaze);

            /// <summary>
            /// Mutable gaze data container — updated in place each tick, never re-allocated.
            /// </summary>
            public class GazeData
            {
                public Vector3?    Position    { get; private set; }
                public Quaternion? Rotation    { get; private set; }
                public Vector3?    GazeForward { get; private set; }
                public Vector3?    GazePoint   { get; private set; }

                public bool IsAllNull =>
                    Position == null && Rotation == null && GazeForward == null && GazePoint == null;

                public void Update(Vector3? position, Quaternion? rotation, Vector3? forward, Vector3? point)
                {
                    Position    = position;
                    Rotation    = rotation;
                    GazeForward = forward;
                    GazePoint   = point;
                }
            }

            private GazeData UpdateSingleEyeData(GazeData cache, Transform eyeTransform)
            {
                if (eyeTransform == null)
                {
                    cache.Update(null, null, null, null);
                    return null;
                }

                try
                {
                    cache.Update(eyeTransform.position, eyeTransform.rotation, eyeTransform.forward, null);
                    return cache.IsAllNull ? null : cache;
                }
                catch
                {
                    cache.Update(null, null, null, null);
                    return null;
                }
            }

            private GazeData UpdateGazePoseData(GazeData cache, GazePose.GazePoseData gazePoseData)
            {
                if (gazePoseData == null)
                {
                    cache.Update(null, null, null, null);
                    return null;
                }

                try
                {
                    var t = gazePoseData.GetTransform();
                    if (t == null)
                    {
                        cache.Update(null, null, null, null);
                        return null;
                    }

                    cache.Update(t.position, t.rotation, t.forward, gazePoseData.GetGazePoint());
                    return cache.IsAllNull ? null : cache;
                }
                catch
                {
                    cache.Update(null, null, null, null);
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
