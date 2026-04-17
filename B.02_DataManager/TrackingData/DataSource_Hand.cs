using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using MetaFrame.Interaction;

namespace MetaFrame.Data
{
    public class DataSource_Hand : DataSourceBase<DataSource_Hand.DataStructure, DataSource_Hand.RecordingConfig>
    {
        [SerializeField] private Hand _leftHand;
        [SerializeField] private TransformFeatureStateProvider _leftTransformFeatureStateProvider;
        [SerializeField] private FingerFeatureStateProvider _leftFingerFeatureStateProvider;

        [SerializeField] private Hand _rightHand;
        [SerializeField] private TransformFeatureStateProvider _rightTransformFeatureStateProvider;
        [SerializeField] private FingerFeatureStateProvider _rightFingerFeatureStateProvider;

        public override string SourceName => "Hand";

        protected override DataStructure CreateData()
        {
            return new DataStructure(this,
                _leftTransformFeatureStateProvider,  _leftFingerFeatureStateProvider,
                _rightTransformFeatureStateProvider, _rightFingerFeatureStateProvider);
        }

        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (_leftHand?.IsConnected == true)
            {
                if (RecordConfig.LeftPalm)
                    data["leftPalm"] = GetTransformData(Data.LeftPalm);

                if (RecordConfig.LeftPalmTowardsFace)
                    data["leftPalmTowardsFace"] = Data.LeftPalmTowardsFace;

                if (RecordConfig.LeftFingers)
                {
                    // FIX: was GetObjectData(Data.LeftThumb) etc. — GetObjectData used
                    // reflection (GetType().GetProperties()) on every call. With 10 finger
                    // entries at 100Hz that's 1000 reflection-based property lookups per
                    // second on the main thread. Replaced with ToArray() which reads the
                    // four known fields directly and builds a float[] with no reflection.
                    data["leftThumb"]  = Data.LeftThumb?.ToArray();
                    data["leftIndex"]  = Data.LeftIndex?.ToArray();
                    data["leftMiddle"] = Data.LeftMiddle?.ToArray();
                    data["leftRing"]   = Data.LeftRing?.ToArray();
                    data["leftPinky"]  = Data.LeftPinky?.ToArray();
                }
            }

            if (_rightHand?.IsConnected == true)
            {
                if (RecordConfig.RightPalm)
                    data["rightPalm"] = GetTransformData(Data.RightPalm);

                if (RecordConfig.RightPalmTowardsFace)
                    data["rightPalmTowardsFace"] = Data.RightPalmTowardsFace;

                if (RecordConfig.RightFingers)
                {
                    data["rightThumb"]  = Data.RightThumb?.ToArray();
                    data["rightIndex"]  = Data.RightIndex?.ToArray();
                    data["rightMiddle"] = Data.RightMiddle?.ToArray();
                    data["rightRing"]   = Data.RightRing?.ToArray();
                    data["rightPinky"]  = Data.RightPinky?.ToArray();
                }
            }

            return data;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Hand Data Structure
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Hand _source;
            private readonly TransformFeatureStateProvider _leftTransformProvider;
            private readonly FingerFeatureStateProvider _leftFingerProvider;
            private readonly TransformFeatureStateProvider _rightTransformProvider;
            private readonly FingerFeatureStateProvider _rightFingerProvider;

            public DataStructure(DataSource_Hand source,
                TransformFeatureStateProvider leftTransformProvider,  FingerFeatureStateProvider leftFingerProvider,
                TransformFeatureStateProvider rightTransformProvider, FingerFeatureStateProvider rightFingerProvider)
            {
                _source                = source;
                _leftTransformProvider  = leftTransformProvider;
                _leftFingerProvider     = leftFingerProvider;
                _rightTransformProvider = rightTransformProvider;
                _rightFingerProvider    = rightFingerProvider;
            }

            // Left Hand
            public Transform LeftPalm             => GetPalm(true);
            public float?    LeftPalmTowardsFace  => GetPalmTowardsFaceValue(true);
            public FingerData LeftThumb  => GetFingerData(_leftFingerProvider, HandFinger.Thumb);
            public FingerData LeftIndex  => GetFingerData(_leftFingerProvider, HandFinger.Index);
            public FingerData LeftMiddle => GetFingerData(_leftFingerProvider, HandFinger.Middle);
            public FingerData LeftRing   => GetFingerData(_leftFingerProvider, HandFinger.Ring);
            public FingerData LeftPinky  => GetFingerData(_leftFingerProvider, HandFinger.Pinky);

            // Right Hand
            public Transform RightPalm            => GetPalm(false);
            public float?    RightPalmTowardsFace => GetPalmTowardsFaceValue(false);
            public FingerData RightThumb  => GetFingerData(_rightFingerProvider, HandFinger.Thumb);
            public FingerData RightIndex  => GetFingerData(_rightFingerProvider, HandFinger.Index);
            public FingerData RightMiddle => GetFingerData(_rightFingerProvider, HandFinger.Middle);
            public FingerData RightRing   => GetFingerData(_rightFingerProvider, HandFinger.Ring);
            public FingerData RightPinky  => GetFingerData(_rightFingerProvider, HandFinger.Pinky);

            private Transform GetPalm(bool isLeft)
            {
                try
                {
                    return isLeft
                        ? _source.dataManager.Body.Data.LeftHandPalm
                        : _source.dataManager.Body.Data.RightHandPalm;
                }
                catch { return null; }
            }

            private float? GetPalmTowardsFaceValue(bool isLeft)
            {
                try
                {
                    var provider = isLeft ? _leftTransformProvider : _rightTransformProvider;
                    return provider.GetFeatureValue(_source.dataManager.config, TransformFeature.PalmTowardsFace);
                }
                catch { return null; }
            }

            private FingerData GetFingerData(FingerFeatureStateProvider provider, HandFinger finger)
            {
                try
                {
                    var curl       = provider.GetFeatureValue(finger, FingerFeature.Curl);
                    var flexion    = provider.GetFeatureValue(finger, FingerFeature.Flexion);
                    var abduction  = provider.GetFeatureValue(finger, FingerFeature.Abduction);
                    var opposition = provider.GetFeatureValue(finger, FingerFeature.Opposition);

                    var d = new FingerData(curl, flexion, abduction, opposition);
                    return d.IsAllNull ? null : d;
                }
                catch { return null; }
            }

            public class FingerData
            {
                public float? Curl       { get; }
                public float? Flexion    { get; }
                public float? Abduction  { get; }
                public float? Opposition { get; }

                public FingerData(float? curl, float? flexion, float? abduction, float? opposition)
                {
                    Curl       = curl;
                    Flexion    = flexion;
                    Abduction  = abduction;
                    Opposition = opposition;
                }

                public bool IsAllNull =>
                    Curl == null && Flexion == null && Abduction == null && Opposition == null;

                /// <summary>
                /// Returns finger values as a float array — no reflection, no heap overhead
                /// beyond the array itself. Layout: [Curl, Flexion, Abduction, Opposition]
                /// </summary>
                public float[] ToArray() => new float[]
                {
                    Curl       ?? 0f,
                    Flexion    ?? 0f,
                    Abduction  ?? 0f,
                    Opposition ?? 0f,
                };
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Hand Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Left Hand")]
            [Tooltip("transform?")]
            public bool LeftPalm = true;
            [Tooltip("float?")]
            public bool LeftPalmTowardsFace = true;
            [Tooltip("[Curl, Flexion, Abduction, Opposition]")]
            public bool LeftFingers = true;

            [Header("Right Hand")]
            [Tooltip("transform?")]
            public bool RightPalm = true;
            [Tooltip("float?")]
            public bool RightPalmTowardsFace = true;
            [Tooltip("[Curl, Flexion, Abduction, Opposition]")]
            public bool RightFingers = true;
        }
    }
}
