using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using MetaFrame.Interaction;
using Sirenix.OdinInspector.Demos;
using NUnit.Framework;


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
                _leftTransformFeatureStateProvider, _leftFingerFeatureStateProvider,
                _rightTransformFeatureStateProvider, _rightFingerFeatureStateProvider);
        }

        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (_leftHand?.IsConnected == true)
            {
                if (RecordConfig.LeftPalm)
                {
                    data["leftPalm"] = GetTransformData(Data.LeftPalm);
                }

                if (RecordConfig.LeftPalmTowardsFace)
                {
                    data["leftPalmTowardsFace"] = Data.LeftPalmTowardsFace;
                }

                // Left Hand - Finger Data
                if (RecordConfig.LeftFingers)
                {
                    data["leftThumb"] = GetObjectData(Data.LeftThumb);
                    data["leftIndex"] = GetObjectData(Data.LeftIndex);
                    data["leftMiddle"] = GetObjectData(Data.LeftMiddle);
                    data["leftRing"] = GetObjectData(Data.LeftRing);
                    data["leftPinky"] = GetObjectData(Data.LeftPinky);
                }
            }

            if (_rightHand?.IsConnected == true)
            {
                if (RecordConfig.RightPalm)
                {
                    data["rightPalm"] = GetTransformData(Data.RightPalm);
                }

                if (RecordConfig.RightPalmTowardsFace)
                {
                    data["rightPalmTowardsFace"] = Data.RightPalmTowardsFace;
                }

                // Right Hand - Finger Data
                if (RecordConfig.RightFingers)
                {
                    data["rightThumb"] = GetObjectData(Data.RightThumb);
                    data["rightIndex"] = GetObjectData(Data.RightIndex);
                    data["rightMiddle"] = GetObjectData(Data.RightMiddle);
                    data["rightRing"] = GetObjectData(Data.RightRing);
                    data["rightPinky"] = GetObjectData(Data.RightPinky);
                }
            }

            return data;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Hand Data Structure - Clean property-based access for consistent static typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Hand _source;
            private readonly TransformFeatureStateProvider _leftTransformProvider;
            private readonly FingerFeatureStateProvider _leftFingerProvider;
            private readonly TransformFeatureStateProvider _rightTransformProvider;
            private readonly FingerFeatureStateProvider _rightFingerProvider;

            public DataStructure(DataSource_Hand source,
                TransformFeatureStateProvider leftTransformProvider, FingerFeatureStateProvider leftFingerProvider,
                TransformFeatureStateProvider rightTransformProvider, FingerFeatureStateProvider rightFingerProvider)
            {
                _source = source;
                _leftTransformProvider = leftTransformProvider;
                _leftFingerProvider = leftFingerProvider;
                _rightTransformProvider = rightTransformProvider;
                _rightFingerProvider = rightFingerProvider;
            }

            // Left Hand Data
            public Transform LeftPalm => GetPalm(true);
            public float? LeftPalmTowardsFace => GetPalmTowardsFaceValue(true);
            public FingerData LeftThumb => GetFingerData(_leftFingerProvider, HandFinger.Thumb);
            public FingerData LeftIndex => GetFingerData(_leftFingerProvider, HandFinger.Index);
            public FingerData LeftMiddle => GetFingerData(_leftFingerProvider, HandFinger.Middle);
            public FingerData LeftRing => GetFingerData(_leftFingerProvider, HandFinger.Ring);
            public FingerData LeftPinky => GetFingerData(_leftFingerProvider, HandFinger.Pinky);

            // Right Hand Data
            public Transform RightPalm => GetPalm(false);
            public float? RightPalmTowardsFace => GetPalmTowardsFaceValue(false);
            public FingerData RightThumb => GetFingerData(_rightFingerProvider, HandFinger.Thumb);
            public FingerData RightIndex => GetFingerData(_rightFingerProvider, HandFinger.Index);
            public FingerData RightMiddle => GetFingerData(_rightFingerProvider, HandFinger.Middle);
            public FingerData RightRing => GetFingerData(_rightFingerProvider, HandFinger.Ring);
            public FingerData RightPinky => GetFingerData(_rightFingerProvider, HandFinger.Pinky);

            // Helper method to get palm transform with null safety
            private Transform GetPalm(bool isLeftHand)
            {
                try
                {
                    if (isLeftHand)
                        return _source.dataManager.Body.Data.LeftHandPalm;
                    else
                        return _source.dataManager.Body.Data.RightHandPalm;
                }
                catch { return null; }
            }
            // Helper method to get palm towards face value with null safety
            private float? GetPalmTowardsFaceValue(bool isLeftHand)
            {
                try
                {
                    if (isLeftHand)
                        return _leftTransformProvider.GetFeatureValue(_source.dataManager.config, TransformFeature.PalmTowardsFace);
                    else
                        return _rightTransformProvider.GetFeatureValue(_source.dataManager.config, TransformFeature.PalmTowardsFace);
                }
                catch { return null; }
            }
            // Helper method to get finger data with null safety
            private FingerData GetFingerData(FingerFeatureStateProvider fingerProvider, HandFinger finger)
            {
                try
                {
                    var curl = fingerProvider.GetFeatureValue(finger, FingerFeature.Curl);
                    var flexion = fingerProvider.GetFeatureValue(finger, FingerFeature.Flexion);
                    var abduction = fingerProvider.GetFeatureValue(finger, FingerFeature.Abduction);
                    var opposition = fingerProvider.GetFeatureValue(finger, FingerFeature.Opposition);

                    var data = new FingerData(curl, flexion, abduction, opposition);
                    return data.IsAllNull ? null : data;
                }
                catch { return null; }
            }

            public class FingerData
            {
                public float? Curl { get; }
                public float? Flexion { get; }
                public float? Abduction { get; }
                public float? Opposition { get; }

                public FingerData(float? curl, float? flexion, float? abduction, float? opposition)
                {
                    Curl = curl;
                    Flexion = flexion;
                    Abduction = abduction;
                    Opposition = opposition;
                }

                public bool IsAllNull =>
                    Curl == null && Flexion == null && Abduction == null && Opposition == null;
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
            [Tooltip("(curl, flexion, abduction, opposition)?")]
            public bool LeftFingers = true;

            [Header("Right Hand")]
            [Tooltip("transform?")]
            public bool RightPalm = true;
            [Tooltip("float?")]
            public bool RightPalmTowardsFace = true;
            [Tooltip("(curl, flexion, abduction, opposition)?")]
            public bool RightFingers = true;
        }
    }
}