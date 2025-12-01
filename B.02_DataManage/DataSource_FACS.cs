using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using static OVRFaceExpressions;

namespace MetaNomads.Data
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
                // Upper Face AUs
                if (RecordConfig.AU1_InnerBrowRaiser)
                {
                    var au1 = Data.AU1_InnerBrowRaiser;
                    data["AU1_InnerBrowRaiser"] = new { InnerBrowRaiserL = au1.InnerBrowRaiserL, InnerBrowRaiserR = au1.InnerBrowRaiserR };
                }

                if (RecordConfig.AU2_OuterBrowRaiser)
                {
                    var au2 = Data.AU2_OuterBrowRaiser;
                    data["AU2_OuterBrowRaiser"] = new { OuterBrowRaiserL = au2.OuterBrowRaiserL, OuterBrowRaiserR = au2.OuterBrowRaiserR };
                }

                if (RecordConfig.AU4_BrowLowerer)
                {
                    var au4 = Data.AU4_BrowLowerer;
                    data["AU4_BrowLowerer"] = new { BrowLowererL = au4.BrowLowererL, BrowLowererR = au4.BrowLowererR };
                }

                if (RecordConfig.AU5_UpperLidRaiser)
                {
                    var au5 = Data.AU5_UpperLidRaiser;
                    data["AU5_UpperLidRaiser"] = new { UpperLidRaiserL = au5.UpperLidRaiserL, UpperLidRaiserR = au5.UpperLidRaiserR };
                }

                if (RecordConfig.AU6_CheekRaiser)
                {
                    var au6 = Data.AU6_CheekRaiser;
                    data["AU6_CheekRaiser"] = new { CheekRaiserL = au6.CheekRaiserL, CheekRaiserR = au6.CheekRaiserR };
                }

                if (RecordConfig.AU7_LidTightener)
                {
                    var au7 = Data.AU7_LidTightener;
                    data["AU7_LidTightener"] = new { LidTightenerL = au7.LidTightenerL, LidTightenerR = au7.LidTightenerR };
                }

                if (RecordConfig.AU9_NoseWrinkler)
                {
                    var au9 = Data.AU9_NoseWrinkler;
                    data["AU9_NoseWrinkler"] = new { NoseWrinklerL = au9.NoseWrinklerL, NoseWrinklerR = au9.NoseWrinklerR };
                }

                // Mouth and Lower Face AUs
                if (RecordConfig.AU10_UpperLipRaiser)
                {
                    var au10 = Data.AU10_UpperLipRaiser;
                    data["AU10_UpperLipRaiser"] = new { UpperLipRaiserL = au10.UpperLipRaiserL, UpperLipRaiserR = au10.UpperLipRaiserR };
                }

                if (RecordConfig.AU12_LipCornerPuller)
                {
                    var au12 = Data.AU12_LipCornerPuller;
                    data["AU12_LipCornerPuller"] = new { LipCornerPullerL = au12.LipCornerPullerL, LipCornerPullerR = au12.LipCornerPullerR };
                }

                if (RecordConfig.AU14_Dimpler)
                {
                    var au14 = Data.AU14_Dimpler;
                    data["AU14_Dimpler"] = new { DimplerL = au14.DimplerL, DimplerR = au14.DimplerR };
                }

                if (RecordConfig.AU15_LipCornerDepressor)
                {
                    var au15 = Data.AU15_LipCornerDepressor;
                    data["AU15_LipCornerDepressor"] = new { LipCornerDepressorL = au15.LipCornerDepressorL, LipCornerDepressorR = au15.LipCornerDepressorR };
                }

                if (RecordConfig.AU16_LowerLipDepressor)
                {
                    var au16 = Data.AU16_LowerLipDepressor;
                    data["AU16_LowerLipDepressor"] = new { LowerLipDepressorL = au16.LowerLipDepressorL, LowerLipDepressorR = au16.LowerLipDepressorR };
                }

                if (RecordConfig.AU17_ChinRaiser)
                {
                    var au17 = Data.AU17_ChinRaiser;
                    data["AU17_ChinRaiser"] = new { ChinRaiserB = au17.ChinRaiserB, ChinRaiserT = au17.ChinRaiserT };
                }

                if (RecordConfig.AU18_LipPucker)
                {
                    var au18 = Data.AU18_LipPucker;
                    data["AU18_LipPucker"] = new { LipPuckerL = au18.LipPuckerL, LipPuckerR = au18.LipPuckerR };
                }

                if (RecordConfig.AU20_LipStretcher)
                {
                    var au20 = Data.AU20_LipStretcher;
                    data["AU20_LipStretcher"] = new { LipStretcherL = au20.LipStretcherL, LipStretcherR = au20.LipStretcherR };
                }

                if (RecordConfig.AU22_LipFunneler)
                {
                    var au22 = Data.AU22_LipFunneler;
                    data["AU22_LipFunneler"] = new { LipFunnelerLB = au22.LipFunnelerLB, LipFunnelerLT = au22.LipFunnelerLT, LipFunnelerRB = au22.LipFunnelerRB, LipFunnelerRT = au22.LipFunnelerRT };
                }

                if (RecordConfig.AU23_LipTightener)
                {
                    var au23 = Data.AU23_LipTightener;
                    data["AU23_LipTightener"] = new { LipTightenerL = au23.LipTightenerL, LipTightenerR = au23.LipTightenerR };
                }

                if (RecordConfig.AU24_LipPressor)
                {
                    var au24 = Data.AU24_LipPressor;
                    data["AU24_LipPressor"] = new { LipPressorL = au24.LipPressorL, LipPressorR = au24.LipPressorR };
                }

                if (RecordConfig.AU26_JawDrop)
                {
                    var au26 = Data.AU26_JawDrop;
                    data["AU26_JawDrop"] = au26;
                }

                if (RecordConfig.AU28_LipSuck)
                {
                    var au28 = Data.AU28_LipSuck;
                    data["AU28_LipSuck"] = new { LipSuckLB = au28.LipSuckLB, LipSuckLT = au28.LipSuckLT, LipSuckRB = au28.LipSuckRB, LipSuckRT = au28.LipSuckRT };
                }

                // Eye Movement AUs
                if (RecordConfig.AU43_EyesClosed)
                {
                    var au61 = Data.AU43_EyesClosed;
                    data["AU43_EyesClosed"] = new { EyesClosedL = au61.EyesClosedL, EyesClosedR = au61.EyesClosedR };
                }

                if (RecordConfig.AU61_EyesLookLeft)
                {
                    var au61 = Data.AU61_EyesLookLeft;
                    data["AU61_EyesLookLeft"] = new { EyesLookLeftL = au61.EyesLookLeftL, EyesLookLeftR = au61.EyesLookLeftR };
                }

                if (RecordConfig.AU62_EyesLookRight)
                {
                    var au62 = Data.AU62_EyesLookRight;
                    data["AU62_EyesLookRight"] = new { EyesLookRightL = au62.EyesLookRightL, EyesLookRightR = au62.EyesLookRightR };
                }

                if (RecordConfig.AU63_EyesLookUp)
                {
                    var au63 = Data.AU63_EyesLookUp;
                    data["AU63_EyesLookUp"] = new { EyesLookUpL = au63.EyesLookUpL, EyesLookUpR = au63.EyesLookUpR };
                }

                if (RecordConfig.AU64_EyesLookDown)
                {
                    var au64 = Data.AU64_EyesLookDown;
                    data["AU64_EyesLookDown"] = new { EyesLookDownL = au64.EyesLookDownL, EyesLookDownR = au64.EyesLookDownR };
                }

                // Miscellaneous AUs
                if (RecordConfig.AU8_LipsToward)
                {
                    var au8 = Data.AU8_LipsToward;
                    data["AU8_LipsToward"] = au8;
                }

                if (RecordConfig.AU29_JawThrust)
                {
                    var au29 = Data.AU29_JawThrust;
                    data["AU29_JawThrust"] = au29;
                }

                if (RecordConfig.AU30_JawSideways)
                {
                    var au30 = Data.AU30_JawSideways;
                    data["AU30_JawSideways"] = new { JawSidewaysLeft = au30.JawSidewaysLeft, JawSidewaysRight = au30.JawSidewaysRight };
                }

                if (RecordConfig.AU34_CheekPuff)
                {
                    var au34 = Data.AU34_CheekPuff;
                    data["AU34_CheekPuff"] = new { CheekPuffL = au34.CheekPuffL, CheekPuffR = au34.CheekPuffR };
                }

                if (RecordConfig.AU36_TongueOut)
                {
                    var au36 = Data.AU36_TongueOut;
                    data["AU36_TongueOut"] = au36;
                }
            }

            if (dataManager.Body._fullBodySkeleton.IsDataValid)
            {
                // Head Movement AUs
                if (RecordConfig.AU51_TurnLeft)
                {
                    var au51 = Data.AU51_TurnLeft;
                    data["AU51_TurnLeft"] = au51;
                }

                if (RecordConfig.AU52_TurnRight)
                {
                    var au52 = Data.AU52_TurnRight;
                    data["AU52_TurnRight"] = au52;
                }

                if (RecordConfig.AU53_HeadUp)
                {
                    var au53 = Data.AU53_HeadUp;
                    data["AU53_HeadUp"] = au53;
                }

                if (RecordConfig.AU54_HeadDown)
                {
                    var au54 = Data.AU54_HeadDown;
                    data["AU54_HeadDown"] = au54;
                }

                if (RecordConfig.AU55_TiltLeft)
                {
                    var au55 = Data.AU55_TiltLeft;
                    data["AU55_TiltLeft"] = au55;
                }

                if (RecordConfig.AU56_TiltRight)
                {
                    var au56 = Data.AU56_TiltRight;
                    data["AU56_TiltRight"] = au56;
                }
            }

            return data;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// FACS Data Structure - Clean property-based access for consistent static typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_FACS _source;
            private readonly OVRFaceExpressions _faceExpressions;

            public DataStructure(DataSource_FACS source, OVRFaceExpressions faceExpressions)
            {
                _source = source;
                _faceExpressions = faceExpressions;
            }

            // Upper Face AUs - Direct property access, trust Meta APIs for null handling
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

            // Head Movement AUs - Using body data if available
            public float? AU51_TurnLeft => GetHeadMovementData("TurnLeft");
            public float? AU52_TurnRight => GetHeadMovementData("TurnRight");
            public float? AU53_HeadUp => GetHeadMovementData("HeadUp");
            public float? AU54_HeadDown => GetHeadMovementData("HeadDown");
            public float? AU55_TiltLeft => GetHeadMovementData("TiltLeft");
            public float? AU56_TiltRight => GetHeadMovementData("TiltRight");

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

            // Helper method to get head movement data
            private float? GetHeadMovementData(string movementType)
            {
                if (_source?.dataManager?.Body?.Data?.Head == null || _source?.dataManager?.Body?.Data?.Chest == null) 
                    return null;

                var headTransform = _source.dataManager.Body.Data.Head;
                var chestTransform = _source.dataManager.Body.Data.Chest;

                return movementType switch
                {
                    "TurnLeft" => _source.CalculateTurn(headTransform, chestTransform, -1f),
                    "TurnRight" => _source.CalculateTurn(headTransform, chestTransform, 1f),
                    "HeadUp" => _source.CalculatePitch(headTransform, chestTransform, -1f),
                    "HeadDown" => _source.CalculatePitch(headTransform, chestTransform, 1f),
                    "TiltLeft" => _source.CalculateTilt(headTransform, chestTransform, -1f),
                    "TiltRight" => _source.CalculateTilt(headTransform, chestTransform, 1f),
                    _ => null
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
            [Tooltip("(InnerBrowRaiserL, InnerBrowRaiserR)")]
            public bool AU1_InnerBrowRaiser = true;
            [Tooltip("(OuterBrowRaiserL, OuterBrowRaiserR)")]
            public bool AU2_OuterBrowRaiser = true;
            [Tooltip("(BrowLowererL, BrowLowererR)")]
            public bool AU4_BrowLowerer = true;
            [Tooltip("(UpperLidRaiserL, UpperLidRaiserR)")]
            public bool AU5_UpperLidRaiser = true;
            [Tooltip("(CheekRaiserL, CheekRaiserR)")]
            public bool AU6_CheekRaiser = true;
            [Tooltip("(LidTightenerL, LidTightenerR)")]
            public bool AU7_LidTightener = true;

            [Header("Lower Face AUs")]
            [Tooltip("(NoseWrinklerL, NoseWrinklerR)")]
            public bool AU9_NoseWrinkler = true;
            [Tooltip("(UpperLipRaiserL, UpperLipRaiserR)")]
            public bool AU10_UpperLipRaiser = true;
            [Tooltip("(LipCornerPullerL, LipCornerPullerR)")]
            public bool AU12_LipCornerPuller = true;
            [Tooltip("(DimplerL, DimplerR)")]
            public bool AU14_Dimpler = true;
            [Tooltip("(LipCornerDepressorL, LipCornerDepressorR)")]
            public bool AU15_LipCornerDepressor = true;
            [Tooltip("(LowerLipDepressorL, LowerLipDepressorR)")]
            public bool AU16_LowerLipDepressor = true;
            [Tooltip("(ChinRaiserB, ChinRaiserT)")]
            public bool AU17_ChinRaiser = true;
            [Tooltip("(LipPuckerL, LipPuckerR)")]
            public bool AU18_LipPucker = true;
            [Tooltip("(LipStretcherL, LipStretcherR)")]
            public bool AU20_LipStretcher = true;
            [Tooltip("(LipFunnelerLB, LipFunnelerLT, LipFunnelerRB, LipFunnelerRT)")]
            public bool AU22_LipFunneler = true;
            [Tooltip("(LipTightenerL, LipTightenerR)")]
            public bool AU23_LipTightener = true;
            [Tooltip("(LipPressorL, LipPressorR)")]
            public bool AU24_LipPressor = true;
            [Tooltip("JawDrop")]
            public bool AU26_JawDrop = true;
            [Tooltip("(LipSuckLB, LipSuckLT, LipSuckRB, LipSuckRT)")]            
            public bool AU28_LipSuck = true;

            [Header("Head Movement AUs")]
            [Tooltip("float?")]
            public bool AU51_TurnLeft = false;
            [Tooltip("float?")]
            public bool AU52_TurnRight = false;
            [Tooltip("float?")]
            public bool AU53_HeadUp = false;
            [Tooltip("float?")]
            public bool AU54_HeadDown = false;
            [Tooltip("float?")]
            public bool AU55_TiltLeft = false;
            [Tooltip("float?")]
            public bool AU56_TiltRight = false;

            [Header("Eye Movement AUs")]
            [Tooltip("float?")]
            public bool AU43_EyesClosed = true;
            [Tooltip("float?")]
            public bool AU61_EyesLookLeft = true;
            [Tooltip("float?")]
            public bool AU62_EyesLookRight = true;
            [Tooltip("float?")]
            public bool AU63_EyesLookUp = true;
            [Tooltip("float?")]
            public bool AU64_EyesLookDown = true;

            [Header("Miscellaneous AUs (Disabled by Default)")]
            [Tooltip("LipsToward")]
            public bool AU8_LipsToward = false;
            [Tooltip("JawThrust")]
            public bool AU29_JawThrust = false;
            [Tooltip("(JawSidewaysLeft, JawSidewaysRight)")]
            public bool AU30_JawSideways = false;
            [Tooltip("(CheekPuffL, CheekPuffR)")]
            public bool AU34_CheekPuff = false;
            [Tooltip("TongueOut")]
            public bool AU36_TongueOut = false;
        }
    }
}