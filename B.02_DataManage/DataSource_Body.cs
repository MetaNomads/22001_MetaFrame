using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Oculus.Interaction.Input;

namespace MetaFrame.Data
{
    /// <summary>
    /// Complete FullBody skeleton data source with all 70+ bones
    /// Only valid when skeleton type is FullBody - invalid otherwise
    /// </summary>
    public class DataSource_Body : DataSourceBase<DataSource_Body.DataStructure, DataSource_Body.RecordingConfig>
    {
        [SerializeField] internal OVRSkeleton _fullBodySkeleton;

        public override string SourceName => "Body";

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _fullBodySkeleton);
        }

        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (_fullBodySkeleton.IsDataValid)
            {
                // Upper Core bones
                if (RecordConfig.UpperCore)
                {
                    data["start"] = GetTransformData(Data.Start);
                    data["root"] = GetTransformData(Data.Root);
                    data["hips"] = GetTransformData(Data.Hips);
                    data["spineLower"] = GetTransformData(Data.SpineLower);
                    data["spineMiddle"] = GetTransformData(Data.SpineMiddle);
                    data["spineUpper"] = GetTransformData(Data.SpineUpper);
                    data["chest"] = GetTransformData(Data.Chest);
                    data["neck"] = GetTransformData(Data.Neck);
                    data["head"] = GetTransformData(Data.Head);
                }

                // Arms
                if (RecordConfig.Arms)
                {
                    // Left Arm
                    data["leftShoulder"] = GetTransformData(Data.LeftShoulder);
                    data["leftScapula"] = GetTransformData(Data.LeftScapula);
                    data["leftArmUpper"] = GetTransformData(Data.LeftArmUpper);
                    data["leftArmLower"] = GetTransformData(Data.LeftArmLower);
                    data["leftHandWristTwist"] = GetTransformData(Data.LeftHandWristTwist);

                    // Right Arm
                    data["rightShoulder"] = GetTransformData(Data.RightShoulder);
                    data["rightScapula"] = GetTransformData(Data.RightScapula);
                    data["rightArmUpper"] = GetTransformData(Data.RightArmUpper);
                    data["rightArmLower"] = GetTransformData(Data.RightArmLower);
                    data["rightHandWristTwist"] = GetTransformData(Data.RightHandWristTwist);
                }

                // Hands
                if (RecordConfig.Hands)
                {
                    // Left Hand
                    data["leftHandPalm"] = GetTransformData(Data.LeftHandPalm);
                    data["leftHandWrist"] = GetTransformData(Data.LeftHandWrist);
                    data["leftHandThumbMetacarpal"] = GetTransformData(Data.LeftHandThumbMetacarpal);
                    data["leftHandThumbProximal"] = GetTransformData(Data.LeftHandThumbProximal);
                    data["leftHandThumbDistal"] = GetTransformData(Data.LeftHandThumbDistal);
                    data["leftHandThumbTip"] = GetTransformData(Data.LeftHandThumbTip);
                    data["leftHandIndexMetacarpal"] = GetTransformData(Data.LeftHandIndexMetacarpal);
                    data["leftHandIndexProximal"] = GetTransformData(Data.LeftHandIndexProximal);
                    data["leftHandIndexIntermediate"] = GetTransformData(Data.LeftHandIndexIntermediate);
                    data["leftHandIndexDistal"] = GetTransformData(Data.LeftHandIndexDistal);
                    data["leftHandIndexTip"] = GetTransformData(Data.LeftHandIndexTip);
                    data["leftHandMiddleMetacarpal"] = GetTransformData(Data.LeftHandMiddleMetacarpal);
                    data["leftHandMiddleProximal"] = GetTransformData(Data.LeftHandMiddleProximal);
                    data["leftHandMiddleIntermediate"] = GetTransformData(Data.LeftHandMiddleIntermediate);
                    data["leftHandMiddleDistal"] = GetTransformData(Data.LeftHandMiddleDistal);
                    data["leftHandMiddleTip"] = GetTransformData(Data.LeftHandMiddleTip);
                    data["leftHandRingMetacarpal"] = GetTransformData(Data.LeftHandRingMetacarpal);
                    data["leftHandRingProximal"] = GetTransformData(Data.LeftHandRingProximal);
                    data["leftHandRingIntermediate"] = GetTransformData(Data.LeftHandRingIntermediate);
                    data["leftHandRingDistal"] = GetTransformData(Data.LeftHandRingDistal);
                    data["leftHandRingTip"] = GetTransformData(Data.LeftHandRingTip);
                    data["leftHandLittleMetacarpal"] = GetTransformData(Data.LeftHandLittleMetacarpal);
                    data["leftHandLittleProximal"] = GetTransformData(Data.LeftHandLittleProximal);
                    data["leftHandLittleIntermediate"] = GetTransformData(Data.LeftHandLittleIntermediate);
                    data["leftHandLittleDistal"] = GetTransformData(Data.LeftHandLittleDistal);
                    data["leftHandLittleTip"] = GetTransformData(Data.LeftHandLittleTip);

                    // Right Hand
                    data["rightHandPalm"] = GetTransformData(Data.RightHandPalm);
                    data["rightHandWrist"] = GetTransformData(Data.RightHandWrist);
                    data["rightHandThumbMetacarpal"] = GetTransformData(Data.RightHandThumbMetacarpal);
                    data["rightHandThumbProximal"] = GetTransformData(Data.RightHandThumbProximal);
                    data["rightHandThumbDistal"] = GetTransformData(Data.RightHandThumbDistal);
                    data["rightHandThumbTip"] = GetTransformData(Data.RightHandThumbTip);
                    data["rightHandIndexMetacarpal"] = GetTransformData(Data.RightHandIndexMetacarpal);
                    data["rightHandIndexProximal"] = GetTransformData(Data.RightHandIndexProximal);
                    data["rightHandIndexIntermediate"] = GetTransformData(Data.RightHandIndexIntermediate);
                    data["rightHandIndexDistal"] = GetTransformData(Data.RightHandIndexDistal);
                    data["rightHandIndexTip"] = GetTransformData(Data.RightHandIndexTip);
                    data["rightHandMiddleMetacarpal"] = GetTransformData(Data.RightHandMiddleMetacarpal);
                    data["rightHandMiddleProximal"] = GetTransformData(Data.RightHandMiddleProximal);
                    data["rightHandMiddleIntermediate"] = GetTransformData(Data.RightHandMiddleIntermediate);
                    data["rightHandMiddleDistal"] = GetTransformData(Data.RightHandMiddleDistal);
                    data["rightHandMiddleTip"] = GetTransformData(Data.RightHandMiddleTip);
                    data["rightHandRingMetacarpal"] = GetTransformData(Data.RightHandRingMetacarpal);
                    data["rightHandRingProximal"] = GetTransformData(Data.RightHandRingProximal);
                    data["rightHandRingIntermediate"] = GetTransformData(Data.RightHandRingIntermediate);
                    data["rightHandRingDistal"] = GetTransformData(Data.RightHandRingDistal);
                    data["rightHandRingTip"] = GetTransformData(Data.RightHandRingTip);
                    data["rightHandLittleMetacarpal"] = GetTransformData(Data.RightHandLittleMetacarpal);
                    data["rightHandLittleProximal"] = GetTransformData(Data.RightHandLittleProximal);
                    data["rightHandLittleIntermediate"] = GetTransformData(Data.RightHandLittleIntermediate);
                    data["rightHandLittleDistal"] = GetTransformData(Data.RightHandLittleDistal);
                    data["rightHandLittleTip"] = GetTransformData(Data.RightHandLittleTip);
                }

                // Legs
                if (RecordConfig.Legs)
                {
                    // Left Leg
                    data["leftUpperLeg"] = GetTransformData(Data.LeftUpperLeg);
                    data["leftLowerLeg"] = GetTransformData(Data.LeftLowerLeg);
                    data["leftFootAnkleTwist"] = GetTransformData(Data.LeftFootAnkleTwist);
                    data["leftFootAnkle"] = GetTransformData(Data.LeftFootAnkle);
                    data["leftFootSubtalar"] = GetTransformData(Data.LeftFootSubtalar);
                    data["leftFootTransverse"] = GetTransformData(Data.LeftFootTransverse);
                    data["leftFootBall"] = GetTransformData(Data.LeftFootBall);

                    // Right Leg
                    data["rightUpperLeg"] = GetTransformData(Data.RightUpperLeg);
                    data["rightLowerLeg"] = GetTransformData(Data.RightLowerLeg);
                    data["rightFootAnkleTwist"] = GetTransformData(Data.RightFootAnkleTwist);
                    data["rightFootAnkle"] = GetTransformData(Data.RightFootAnkle);
                    data["rightFootSubtalar"] = GetTransformData(Data.RightFootSubtalar);
                    data["rightFootTransverse"] = GetTransformData(Data.RightFootTransverse);
                    data["rightFootBall"] = GetTransformData(Data.RightFootBall);
                }
            }
            return data;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Body Data Structure - Clean property-based access for consistent static typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Body _source;
            private readonly OVRSkeleton _skeleton;
            public DataStructure(DataSource_Body source, OVRSkeleton skeleton)
            {
                _source = source;
                _skeleton = skeleton;
            }

            // Direct Transform access - Usage: dataManager.Body.Head.Position
            // UpperCore
            public Transform Start => GetBoneData(OVRSkeleton.BoneId.FullBody_Start);
            public Transform Root => GetBoneData(OVRSkeleton.BoneId.FullBody_Root);
            public Transform Hips => GetBoneData(OVRSkeleton.BoneId.FullBody_Hips);
            public Transform SpineLower => GetBoneData(OVRSkeleton.BoneId.FullBody_SpineLower);
            public Transform SpineMiddle => GetBoneData(OVRSkeleton.BoneId.FullBody_SpineMiddle);
            public Transform SpineUpper => GetBoneData(OVRSkeleton.BoneId.FullBody_SpineUpper);
            public Transform Chest => GetBoneData(OVRSkeleton.BoneId.FullBody_Chest);
            public Transform Neck => GetBoneData(OVRSkeleton.BoneId.FullBody_Neck);
            public Transform Head => GetBoneData(OVRSkeleton.BoneId.FullBody_Head);

            // Left Arm
            public Transform LeftShoulder => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftShoulder);
            public Transform LeftScapula => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftScapula);
            public Transform LeftArmUpper => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftArmUpper);
            public Transform LeftArmLower => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftArmLower);
            public Transform LeftHandWristTwist => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandWristTwist);

            // Right Arm
            public Transform RightShoulder => GetBoneData(OVRSkeleton.BoneId.FullBody_RightShoulder);
            public Transform RightScapula => GetBoneData(OVRSkeleton.BoneId.FullBody_RightScapula);
            public Transform RightArmUpper => GetBoneData(OVRSkeleton.BoneId.FullBody_RightArmUpper);
            public Transform RightArmLower => GetBoneData(OVRSkeleton.BoneId.FullBody_RightArmLower);
            public Transform RightHandWristTwist => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandWristTwist);

            // Left Hand
            public Transform LeftHandPalm => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandPalm);
            public Transform LeftHandWrist => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandWrist);
            public Transform LeftHandThumbMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandThumbMetacarpal);
            public Transform LeftHandThumbProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandThumbProximal);
            public Transform LeftHandThumbDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandThumbDistal);
            public Transform LeftHandThumbTip => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandThumbTip);
            public Transform LeftHandIndexMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandIndexMetacarpal);
            public Transform LeftHandIndexProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandIndexProximal);
            public Transform LeftHandIndexIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandIndexIntermediate);
            public Transform LeftHandIndexDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandIndexDistal);
            public Transform LeftHandIndexTip => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandIndexTip);
            public Transform LeftHandMiddleMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandMiddleMetacarpal);
            public Transform LeftHandMiddleProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandMiddleProximal);
            public Transform LeftHandMiddleIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandMiddleIntermediate);
            public Transform LeftHandMiddleDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandMiddleDistal);
            public Transform LeftHandMiddleTip => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandMiddleTip);
            public Transform LeftHandRingMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandRingMetacarpal);
            public Transform LeftHandRingProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandRingProximal);
            public Transform LeftHandRingIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandRingIntermediate);
            public Transform LeftHandRingDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandRingDistal);
            public Transform LeftHandRingTip => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandRingTip);
            public Transform LeftHandLittleMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandLittleMetacarpal);
            public Transform LeftHandLittleProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandLittleProximal);
            public Transform LeftHandLittleIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandLittleIntermediate);
            public Transform LeftHandLittleDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandLittleDistal);
            public Transform LeftHandLittleTip => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftHandLittleTip);

            // Right Hand
            public Transform RightHandPalm => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandPalm);
            public Transform RightHandWrist => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandWrist);
            public Transform RightHandThumbMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandThumbMetacarpal);
            public Transform RightHandThumbProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandThumbProximal);
            public Transform RightHandThumbDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandThumbDistal);
            public Transform RightHandThumbTip => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandThumbTip);
            public Transform RightHandIndexMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandIndexMetacarpal);
            public Transform RightHandIndexProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandIndexProximal);
            public Transform RightHandIndexIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandIndexIntermediate);
            public Transform RightHandIndexDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandIndexDistal);
            public Transform RightHandIndexTip => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandIndexTip);
            public Transform RightHandMiddleMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandMiddleMetacarpal);
            public Transform RightHandMiddleProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandMiddleProximal);
            public Transform RightHandMiddleIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandMiddleIntermediate);
            public Transform RightHandMiddleDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandMiddleDistal);
            public Transform RightHandMiddleTip => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandMiddleTip);
            public Transform RightHandRingMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandRingMetacarpal);
            public Transform RightHandRingProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandRingProximal);
            public Transform RightHandRingIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandRingIntermediate);
            public Transform RightHandRingDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandRingDistal);
            public Transform RightHandRingTip => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandRingTip);
            public Transform RightHandLittleMetacarpal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandLittleMetacarpal);
            public Transform RightHandLittleProximal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandLittleProximal);
            public Transform RightHandLittleIntermediate => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandLittleIntermediate);
            public Transform RightHandLittleDistal => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandLittleDistal);
            public Transform RightHandLittleTip => GetBoneData(OVRSkeleton.BoneId.FullBody_RightHandLittleTip);

            // Left Leg
            public Transform LeftUpperLeg => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftUpperLeg);
            public Transform LeftLowerLeg => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftLowerLeg);
            public Transform LeftFootAnkleTwist => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftFootAnkleTwist);
            public Transform LeftFootAnkle => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftFootAnkle);
            public Transform LeftFootSubtalar => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftFootSubtalar);
            public Transform LeftFootTransverse => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftFootTransverse);
            public Transform LeftFootBall => GetBoneData(OVRSkeleton.BoneId.FullBody_LeftFootBall);

            // Right Leg
            public Transform RightUpperLeg => GetBoneData(OVRSkeleton.BoneId.FullBody_RightUpperLeg);
            public Transform RightLowerLeg => GetBoneData(OVRSkeleton.BoneId.FullBody_RightLowerLeg);
            public Transform RightFootAnkleTwist => GetBoneData(OVRSkeleton.BoneId.FullBody_RightFootAnkleTwist);
            public Transform RightFootAnkle => GetBoneData(OVRSkeleton.BoneId.FullBody_RightFootAnkle);
            public Transform RightFootSubtalar => GetBoneData(OVRSkeleton.BoneId.FullBody_RightFootSubtalar);
            public Transform RightFootTransverse => GetBoneData(OVRSkeleton.BoneId.FullBody_RightFootTransverse);
            public Transform RightFootBall => GetBoneData(OVRSkeleton.BoneId.FullBody_RightFootBall);

            // helper method to get bone data
            private Transform GetBoneData(OVRSkeleton.BoneId boneId)
            {
                if (_skeleton?.Bones == null) return null;

                int boneIndex = (int)boneId;
                if (boneIndex >= 0 && boneIndex < _skeleton.Bones.Count)
                {
                    var bone = _skeleton.Bones[boneIndex];
                    return bone?.Transform;
                }
                return null;
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Body Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            public bool metadata = true;
            public bool UpperCore = true;
            public bool Arms = true;
            public bool Hands = true;
            public bool Legs = true;
        }
    }
}