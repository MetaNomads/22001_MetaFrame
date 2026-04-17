using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using static OVRFaceExpressions;

namespace MetaFrame.Data
{
    public class DataSource_FACS : DataSourceBase<DataSource_FACS.DataStructure, DataSource_FACS.RecordingConfig>
    {
        [SerializeField] private OVRFaceExpressions _faceExpressions;

        public override string SourceName => "FACS";

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _faceExpressions);
        }

        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            if (_faceExpressions.ValidExpressions)
            {
                // FIX: All AU entries previously used anonymous objects:
                //   new { InnerBrowRaiserL = au1.InnerBrowRaiserL, InnerBrowRaiserR = au1.InnerBrowRaiserR }
                // Anonymous objects are heap-allocated reference types. With ~25 AUs enabled
                // and recording at 100Hz, this produced ~2500 heap allocations per second,
                // continuously feeding the GC. Additionally, ApplyPrecisionToObject used
                // reflection to round these values — reflection is extremely expensive on
                // a hot path.
                //
                // Replaced with float[] which:
                //   - Is a single heap allocation (vs one per AU per field)
                //   - Goes through the fast float[] branch in ApplyPrecisionToValue
                //   - Eliminates all reflection from the recording pipeline
                //
                // Array layout is documented per entry below.
                // ValidExpressions is checked above so GetWeight() calls are safe here
                // without the redundant ?. null-conditional guard used in DataStructure.

                // Upper Face AUs
                if (RecordConfig.AU1_InnerBrowRaiser)
                    // [InnerBrowRaiserL, InnerBrowRaiserR]
                    data["AU1_InnerBrowRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.InnerBrowRaiserL),
                        _faceExpressions.GetWeight(FaceExpression.InnerBrowRaiserR) };

                if (RecordConfig.AU2_OuterBrowRaiser)
                    // [OuterBrowRaiserL, OuterBrowRaiserR]
                    data["AU2_OuterBrowRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.OuterBrowRaiserL),
                        _faceExpressions.GetWeight(FaceExpression.OuterBrowRaiserR) };

                if (RecordConfig.AU4_BrowLowerer)
                    // [BrowLowererL, BrowLowererR]
                    data["AU4_BrowLowerer"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.BrowLowererL),
                        _faceExpressions.GetWeight(FaceExpression.BrowLowererR) };

                if (RecordConfig.AU5_UpperLidRaiser)
                    // [UpperLidRaiserL, UpperLidRaiserR]
                    data["AU5_UpperLidRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.UpperLidRaiserL),
                        _faceExpressions.GetWeight(FaceExpression.UpperLidRaiserR) };

                if (RecordConfig.AU6_CheekRaiser)
                    // [CheekRaiserL, CheekRaiserR]
                    data["AU6_CheekRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.CheekRaiserL),
                        _faceExpressions.GetWeight(FaceExpression.CheekRaiserR) };

                if (RecordConfig.AU7_LidTightener)
                    // [LidTightenerL, LidTightenerR]
                    data["AU7_LidTightener"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LidTightenerL),
                        _faceExpressions.GetWeight(FaceExpression.LidTightenerR) };

                if (RecordConfig.AU9_NoseWrinkler)
                    // [NoseWrinklerL, NoseWrinklerR]
                    data["AU9_NoseWrinkler"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.NoseWrinklerL),
                        _faceExpressions.GetWeight(FaceExpression.NoseWrinklerR) };

                // Mouth and Lower Face AUs
                if (RecordConfig.AU10_UpperLipRaiser)
                    // [UpperLipRaiserL, UpperLipRaiserR]
                    data["AU10_UpperLipRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.UpperLipRaiserL),
                        _faceExpressions.GetWeight(FaceExpression.UpperLipRaiserR) };

                if (RecordConfig.AU12_LipCornerPuller)
                    // [LipCornerPullerL, LipCornerPullerR]
                    data["AU12_LipCornerPuller"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipCornerPullerL),
                        _faceExpressions.GetWeight(FaceExpression.LipCornerPullerR) };

                if (RecordConfig.AU14_Dimpler)
                    // [DimplerL, DimplerR]
                    data["AU14_Dimpler"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.DimplerL),
                        _faceExpressions.GetWeight(FaceExpression.DimplerR) };

                if (RecordConfig.AU15_LipCornerDepressor)
                    // [LipCornerDepressorL, LipCornerDepressorR]
                    data["AU15_LipCornerDepressor"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipCornerDepressorL),
                        _faceExpressions.GetWeight(FaceExpression.LipCornerDepressorR) };

                if (RecordConfig.AU16_LowerLipDepressor)
                    // [LowerLipDepressorL, LowerLipDepressorR]
                    data["AU16_LowerLipDepressor"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LowerLipDepressorL),
                        _faceExpressions.GetWeight(FaceExpression.LowerLipDepressorR) };

                if (RecordConfig.AU17_ChinRaiser)
                    // [ChinRaiserB, ChinRaiserT]
                    data["AU17_ChinRaiser"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.ChinRaiserB),
                        _faceExpressions.GetWeight(FaceExpression.ChinRaiserT) };

                if (RecordConfig.AU18_LipPucker)
                    // [LipPuckerL, LipPuckerR]
                    data["AU18_LipPucker"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipPuckerL),
                        _faceExpressions.GetWeight(FaceExpression.LipPuckerR) };

                if (RecordConfig.AU20_LipStretcher)
                    // [LipStretcherL, LipStretcherR]
                    data["AU20_LipStretcher"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipStretcherL),
                        _faceExpressions.GetWeight(FaceExpression.LipStretcherR) };

                if (RecordConfig.AU22_LipFunneler)
                    // [LipFunnelerLB, LipFunnelerLT, LipFunnelerRB, LipFunnelerRT]
                    data["AU22_LipFunneler"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipFunnelerLB),
                        _faceExpressions.GetWeight(FaceExpression.LipFunnelerLT),
                        _faceExpressions.GetWeight(FaceExpression.LipFunnelerRB),
                        _faceExpressions.GetWeight(FaceExpression.LipFunnelerRT) };

                if (RecordConfig.AU23_LipTightener)
                    // [LipTightenerL, LipTightenerR]
                    data["AU23_LipTightener"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipTightenerL),
                        _faceExpressions.GetWeight(FaceExpression.LipTightenerR) };

                if (RecordConfig.AU24_LipPressor)
                    // [LipPressorL, LipPressorR]
                    data["AU24_LipPressor"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipPressorL),
                        _faceExpressions.GetWeight(FaceExpression.LipPressorR) };

                if (RecordConfig.AU26_JawDrop)
                    data["AU26_JawDrop"] = _faceExpressions.GetWeight(FaceExpression.JawDrop);

                if (RecordConfig.AU28_LipSuck)
                    // [LipSuckLB, LipSuckLT, LipSuckRB, LipSuckRT]
                    data["AU28_LipSuck"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.LipSuckLB),
                        _faceExpressions.GetWeight(FaceExpression.LipSuckLT),
                        _faceExpressions.GetWeight(FaceExpression.LipSuckRB),
                        _faceExpressions.GetWeight(FaceExpression.LipSuckRT) };

                // Eye Movement AUs
                if (RecordConfig.AU43_EyesClosed)
                    // [EyesClosedL, EyesClosedR]
                    data["AU43_EyesClosed"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.EyesClosedL),
                        _faceExpressions.GetWeight(FaceExpression.EyesClosedR) };

                if (RecordConfig.AU61_EyesLookLeft)
                    // [EyesLookLeftL, EyesLookLeftR]
                    data["AU61_EyesLookLeft"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.EyesLookLeftL),
                        _faceExpressions.GetWeight(FaceExpression.EyesLookLeftR) };

                if (RecordConfig.AU62_EyesLookRight)
                    // [EyesLookRightL, EyesLookRightR]
                    data["AU62_EyesLookRight"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.EyesLookRightL),
                        _faceExpressions.GetWeight(FaceExpression.EyesLookRightR) };

                if (RecordConfig.AU63_EyesLookUp)
                    // [EyesLookUpL, EyesLookUpR]
                    data["AU63_EyesLookUp"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.EyesLookUpL),
                        _faceExpressions.GetWeight(FaceExpression.EyesLookUpR) };

                if (RecordConfig.AU64_EyesLookDown)
                    // [EyesLookDownL, EyesLookDownR]
                    data["AU64_EyesLookDown"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.EyesLookDownL),
                        _faceExpressions.GetWeight(FaceExpression.EyesLookDownR) };

                // Miscellaneous AUs
                if (RecordConfig.AU8_LipsToward)
                    data["AU8_LipsToward"] = _faceExpressions.GetWeight(FaceExpression.LipsToward);

                if (RecordConfig.AU29_JawThrust)
                    data["AU29_JawThrust"] = _faceExpressions.GetWeight(FaceExpression.JawThrust);

                if (RecordConfig.AU30_JawSideways)
                    // [JawSidewaysLeft, JawSidewaysRight]
                    data["AU30_JawSideways"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.JawSidewaysLeft),
                        _faceExpressions.GetWeight(FaceExpression.JawSidewaysRight) };

                if (RecordConfig.AU34_CheekPuff)
                    // [CheekPuffL, CheekPuffR]
                    data["AU34_CheekPuff"] = new float[] {
                        _faceExpressions.GetWeight(FaceExpression.CheekPuffL),
                        _faceExpressions.GetWeight(FaceExpression.CheekPuffR) };

                if (RecordConfig.AU36_TongueOut)
                    data["AU36_TongueOut"] = _faceExpressions.GetWeight(FaceExpression.TongueOut);
            }

            if (dataManager.Body._fullBodySkeleton.IsDataValid)
            {
                // Head Movement AUs
                if (RecordConfig.AU51_TurnLeft)  data["AU51_TurnLeft"]  = Data.AU51_TurnLeft;
                if (RecordConfig.AU52_TurnRight) data["AU52_TurnRight"] = Data.AU52_TurnRight;
                if (RecordConfig.AU53_HeadUp)    data["AU53_HeadUp"]    = Data.AU53_HeadUp;
                if (RecordConfig.AU54_HeadDown)  data["AU54_HeadDown"]  = Data.AU54_HeadDown;
                if (RecordConfig.AU55_TiltLeft)  data["AU55_TiltLeft"]  = Data.AU55_TiltLeft;
                if (RecordConfig.AU56_TiltRight) data["AU56_TiltRight"] = Data.AU56_TiltRight;
            }

            return data;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// FACS Data Structure — provides property access for external consumers (e.g. LSLService).
        /// CollectData() bypasses these and calls GetWeight() directly for recording performance,
        /// but these properties must remain for any code that reads DataStructure directly.
        /// All properties return ValueTuples (structs) — no heap allocation on access.
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_FACS _source;
            private readonly OVRFaceExpressions _faceExpressions;

            public DataStructure(DataSource_FACS source, OVRFaceExpressions faceExpressions)
            {
                _source          = source;
                _faceExpressions = faceExpressions;
            }

            // Upper Face AUs
            public (float? InnerBrowRaiserL, float? InnerBrowRaiserR) AU1_InnerBrowRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.InnerBrowRaiserL), _faceExpressions?.GetWeight(FaceExpression.InnerBrowRaiserR));

            public (float? OuterBrowRaiserL, float? OuterBrowRaiserR) AU2_OuterBrowRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.OuterBrowRaiserL), _faceExpressions?.GetWeight(FaceExpression.OuterBrowRaiserR));

            public (float? BrowLowererL, float? BrowLowererR) AU4_BrowLowerer =>
                (_faceExpressions?.GetWeight(FaceExpression.BrowLowererL), _faceExpressions?.GetWeight(FaceExpression.BrowLowererR));

            public (float? UpperLidRaiserL, float? UpperLidRaiserR) AU5_UpperLidRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.UpperLidRaiserL), _faceExpressions?.GetWeight(FaceExpression.UpperLidRaiserR));

            public (float? CheekRaiserL, float? CheekRaiserR) AU6_CheekRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.CheekRaiserL), _faceExpressions?.GetWeight(FaceExpression.CheekRaiserR));

            public (float? LidTightenerL, float? LidTightenerR) AU7_LidTightener =>
                (_faceExpressions?.GetWeight(FaceExpression.LidTightenerL), _faceExpressions?.GetWeight(FaceExpression.LidTightenerR));

            // Lower Face AUs
            public (float? NoseWrinklerL, float? NoseWrinklerR) AU9_NoseWrinkler =>
                (_faceExpressions?.GetWeight(FaceExpression.NoseWrinklerL), _faceExpressions?.GetWeight(FaceExpression.NoseWrinklerR));

            public (float? UpperLipRaiserL, float? UpperLipRaiserR) AU10_UpperLipRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.UpperLipRaiserL), _faceExpressions?.GetWeight(FaceExpression.UpperLipRaiserR));

            public (float? LipCornerPullerL, float? LipCornerPullerR) AU12_LipCornerPuller =>
                (_faceExpressions?.GetWeight(FaceExpression.LipCornerPullerL), _faceExpressions?.GetWeight(FaceExpression.LipCornerPullerR));

            public (float? DimplerL, float? DimplerR) AU14_Dimpler =>
                (_faceExpressions?.GetWeight(FaceExpression.DimplerL), _faceExpressions?.GetWeight(FaceExpression.DimplerR));

            public (float? LipCornerDepressorL, float? LipCornerDepressorR) AU15_LipCornerDepressor =>
                (_faceExpressions?.GetWeight(FaceExpression.LipCornerDepressorL), _faceExpressions?.GetWeight(FaceExpression.LipCornerDepressorR));

            public (float? LowerLipDepressorL, float? LowerLipDepressorR) AU16_LowerLipDepressor =>
                (_faceExpressions?.GetWeight(FaceExpression.LowerLipDepressorL), _faceExpressions?.GetWeight(FaceExpression.LowerLipDepressorR));

            public (float? ChinRaiserB, float? ChinRaiserT) AU17_ChinRaiser =>
                (_faceExpressions?.GetWeight(FaceExpression.ChinRaiserB), _faceExpressions?.GetWeight(FaceExpression.ChinRaiserT));

            public (float? LipPuckerL, float? LipPuckerR) AU18_LipPucker =>
                (_faceExpressions?.GetWeight(FaceExpression.LipPuckerL), _faceExpressions?.GetWeight(FaceExpression.LipPuckerR));

            public (float? LipStretcherL, float? LipStretcherR) AU20_LipStretcher =>
                (_faceExpressions?.GetWeight(FaceExpression.LipStretcherL), _faceExpressions?.GetWeight(FaceExpression.LipStretcherR));

            public (float? LipFunnelerLB, float? LipFunnelerLT, float? LipFunnelerRB, float? LipFunnelerRT) AU22_LipFunneler =>
                (_faceExpressions?.GetWeight(FaceExpression.LipFunnelerLB), _faceExpressions?.GetWeight(FaceExpression.LipFunnelerLT),
                 _faceExpressions?.GetWeight(FaceExpression.LipFunnelerRB), _faceExpressions?.GetWeight(FaceExpression.LipFunnelerRT));

            public (float? LipTightenerL, float? LipTightenerR) AU23_LipTightener =>
                (_faceExpressions?.GetWeight(FaceExpression.LipTightenerL), _faceExpressions?.GetWeight(FaceExpression.LipTightenerR));

            public (float? LipPressorL, float? LipPressorR) AU24_LipPressor =>
                (_faceExpressions?.GetWeight(FaceExpression.LipPressorL), _faceExpressions?.GetWeight(FaceExpression.LipPressorR));

            public float? AU26_JawDrop => _faceExpressions?.GetWeight(FaceExpression.JawDrop);

            public (float? LipSuckLB, float? LipSuckLT, float? LipSuckRB, float? LipSuckRT) AU28_LipSuck =>
                (_faceExpressions?.GetWeight(FaceExpression.LipSuckLB), _faceExpressions?.GetWeight(FaceExpression.LipSuckLT),
                 _faceExpressions?.GetWeight(FaceExpression.LipSuckRB), _faceExpressions?.GetWeight(FaceExpression.LipSuckRT));

            // Eye Movement AUs
            public (float? EyesClosedL, float? EyesClosedR) AU43_EyesClosed =>
                (_faceExpressions?.GetWeight(FaceExpression.EyesClosedL), _faceExpressions?.GetWeight(FaceExpression.EyesClosedR));

            public (float? EyesLookLeftL, float? EyesLookLeftR) AU61_EyesLookLeft =>
                (_faceExpressions?.GetWeight(FaceExpression.EyesLookLeftL), _faceExpressions?.GetWeight(FaceExpression.EyesLookLeftR));

            public (float? EyesLookRightL, float? EyesLookRightR) AU62_EyesLookRight =>
                (_faceExpressions?.GetWeight(FaceExpression.EyesLookRightL), _faceExpressions?.GetWeight(FaceExpression.EyesLookRightR));

            public (float? EyesLookUpL, float? EyesLookUpR) AU63_EyesLookUp =>
                (_faceExpressions?.GetWeight(FaceExpression.EyesLookUpL), _faceExpressions?.GetWeight(FaceExpression.EyesLookUpR));

            public (float? EyesLookDownL, float? EyesLookDownR) AU64_EyesLookDown =>
                (_faceExpressions?.GetWeight(FaceExpression.EyesLookDownL), _faceExpressions?.GetWeight(FaceExpression.EyesLookDownR));

            // Miscellaneous AUs
            public float? AU8_LipsToward => _faceExpressions?.GetWeight(FaceExpression.LipsToward);
            public float? AU29_JawThrust => _faceExpressions?.GetWeight(FaceExpression.JawThrust);

            public (float? JawSidewaysLeft, float? JawSidewaysRight) AU30_JawSideways =>
                (_faceExpressions?.GetWeight(FaceExpression.JawSidewaysLeft), _faceExpressions?.GetWeight(FaceExpression.JawSidewaysRight));

            public (float? CheekPuffL, float? CheekPuffR) AU34_CheekPuff =>
                (_faceExpressions?.GetWeight(FaceExpression.CheekPuffL), _faceExpressions?.GetWeight(FaceExpression.CheekPuffR));

            public float? AU36_TongueOut => _faceExpressions?.GetWeight(FaceExpression.TongueOut);

            // Head Movement AUs
            public float? AU51_TurnLeft  => GetHeadMovementData("TurnLeft");
            public float? AU52_TurnRight => GetHeadMovementData("TurnRight");
            public float? AU53_HeadUp    => GetHeadMovementData("HeadUp");
            public float? AU54_HeadDown  => GetHeadMovementData("HeadDown");
            public float? AU55_TiltLeft  => GetHeadMovementData("TiltLeft");
            public float? AU56_TiltRight => GetHeadMovementData("TiltRight");

            private float? GetHeadMovementData(string movementType)
            {
                if (_source?.dataManager?.Body?.Data?.Head == null ||
                    _source?.dataManager?.Body?.Data?.Chest == null)
                    return null;

                var headTransform  = _source.dataManager.Body.Data.Head;
                var chestTransform = _source.dataManager.Body.Data.Chest;

                return movementType switch
                {
                    "TurnLeft"  => _source.CalculateTurn(headTransform,  chestTransform, -1f),
                    "TurnRight" => _source.CalculateTurn(headTransform,  chestTransform,  1f),
                    "HeadUp"    => _source.CalculatePitch(headTransform, chestTransform, -1f),
                    "HeadDown"  => _source.CalculatePitch(headTransform, chestTransform,  1f),
                    "TiltLeft"  => _source.CalculateTilt(headTransform,  chestTransform, -1f),
                    "TiltRight" => _source.CalculateTilt(headTransform,  chestTransform,  1f),
                    _           => null
                };
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// FACS Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Upper Face AUs")]
            [Tooltip("[InnerBrowRaiserL, InnerBrowRaiserR]")]
            public bool AU1_InnerBrowRaiser = true;
            [Tooltip("[OuterBrowRaiserL, OuterBrowRaiserR]")]
            public bool AU2_OuterBrowRaiser = true;
            [Tooltip("[BrowLowererL, BrowLowererR]")]
            public bool AU4_BrowLowerer = true;
            [Tooltip("[UpperLidRaiserL, UpperLidRaiserR]")]
            public bool AU5_UpperLidRaiser = true;
            [Tooltip("[CheekRaiserL, CheekRaiserR]")]
            public bool AU6_CheekRaiser = true;
            [Tooltip("[LidTightenerL, LidTightenerR]")]
            public bool AU7_LidTightener = true;

            [Header("Lower Face AUs")]
            [Tooltip("[NoseWrinklerL, NoseWrinklerR]")]
            public bool AU9_NoseWrinkler = true;
            [Tooltip("[UpperLipRaiserL, UpperLipRaiserR]")]
            public bool AU10_UpperLipRaiser = true;
            [Tooltip("[LipCornerPullerL, LipCornerPullerR]")]
            public bool AU12_LipCornerPuller = true;
            [Tooltip("[DimplerL, DimplerR]")]
            public bool AU14_Dimpler = true;
            [Tooltip("[LipCornerDepressorL, LipCornerDepressorR]")]
            public bool AU15_LipCornerDepressor = true;
            [Tooltip("[LowerLipDepressorL, LowerLipDepressorR]")]
            public bool AU16_LowerLipDepressor = true;
            [Tooltip("[ChinRaiserB, ChinRaiserT]")]
            public bool AU17_ChinRaiser = true;
            [Tooltip("[LipPuckerL, LipPuckerR]")]
            public bool AU18_LipPucker = true;
            [Tooltip("[LipStretcherL, LipStretcherR]")]
            public bool AU20_LipStretcher = true;
            [Tooltip("[LipFunnelerLB, LipFunnelerLT, LipFunnelerRB, LipFunnelerRT]")]
            public bool AU22_LipFunneler = true;
            [Tooltip("[LipTightenerL, LipTightenerR]")]
            public bool AU23_LipTightener = true;
            [Tooltip("[LipPressorL, LipPressorR]")]
            public bool AU24_LipPressor = true;
            [Tooltip("JawDrop")]
            public bool AU26_JawDrop = true;
            [Tooltip("[LipSuckLB, LipSuckLT, LipSuckRB, LipSuckRT]")]
            public bool AU28_LipSuck = true;

            [Header("Head Movement AUs")]
            public bool AU51_TurnLeft  = false;
            public bool AU52_TurnRight = false;
            public bool AU53_HeadUp    = false;
            public bool AU54_HeadDown  = false;
            public bool AU55_TiltLeft  = false;
            public bool AU56_TiltRight = false;

            [Header("Eye Movement AUs")]
            [Tooltip("[EyesClosedL, EyesClosedR]")]
            public bool AU43_EyesClosed   = true;
            [Tooltip("[EyesLookLeftL, EyesLookLeftR]")]
            public bool AU61_EyesLookLeft  = true;
            [Tooltip("[EyesLookRightL, EyesLookRightR]")]
            public bool AU62_EyesLookRight = true;
            [Tooltip("[EyesLookUpL, EyesLookUpR]")]
            public bool AU63_EyesLookUp    = true;
            [Tooltip("[EyesLookDownL, EyesLookDownR]")]
            public bool AU64_EyesLookDown  = true;

            [Header("Miscellaneous AUs (Disabled by Default)")]
            public bool AU8_LipsToward  = false;
            public bool AU29_JawThrust  = false;
            [Tooltip("[JawSidewaysLeft, JawSidewaysRight]")]
            public bool AU30_JawSideways = false;
            [Tooltip("[CheekPuffL, CheekPuffR]")]
            public bool AU34_CheekPuff  = false;
            public bool AU36_TongueOut  = false;
        }
    }
}
