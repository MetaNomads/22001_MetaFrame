using System;
using System.Collections.Generic;
using UnityEngine;




namespace MetaFrame.Data
{
    public class DataSource_Eyes : DataSourceBase<DataSource_Eyes.DataStructure, DataSource_Eyes.RecordingConfig>
    {

        [SerializeField] internal OVRPlugin.EyeGazesState _eyeGazeState;
        public override string SourceName => "Eyes";
        private bool _eyeTrackingFunctional = false;

        private void Start()
        {
            // start eye tracking
            if (!OVRPlugin.StartEyeTracking())
            {
                _eyeTrackingFunctional = false;
                Debug.LogError("Failed to start eye tracking!");
            }
            else
            {
                _eyeTrackingFunctional = true;
            }
        }

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _eyeGazeState);
        }



        public override Dictionary<string, object> CollectData()
        {
            var data = new Dictionary<string, object>();

            //In case there is a way to ensure the eye tracker is functional, add it here.
            if (_eyeTrackingFunctional)
            {
                //Left Eye Vector
                if (RecordConfig.leftEyeDir)
                {
                    data["leftEyeDir"] = GetPositionData(Data.LeftEyeVector);
                }
                if (RecordConfig.rightEyeDir)
                {
                    data["rightEyeDir"] = GetPositionData(Data.RightEyeVector);
                }
                if (RecordConfig.combinedEyeDir)
                {
                    data["combinedEyeDir"] = GetPositionData(Data.CombinedEyeVector);
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
            private readonly DataSource_Eyes _source;
            private readonly OVRPlugin.EyeGazesState _eyeGazesState;

            public DataStructure(DataSource_Eyes source, OVRPlugin.EyeGazesState eyeGazes)
            {
                _source = source;
                _eyeGazesState = eyeGazes;
            }

            //Direct Vector3 access for the gaze local directions
            public Vector3 LeftEyeVector => GetVectorData(Eye_Options.Left);
            public Vector3 RightEyeVector => GetVectorData(Eye_Options.Right);
            public Vector3 CombinedEyeVector => GetVectorData(Eye_Options.Combined);



            //Helper labels for the helper method
            private enum Eye_Options
            {
                Left,
                Right,
                Combined
            }
            //Helper method which returns the Vector data for the desired eye. Built familiarly to the script GazeRayCombined
            private Vector3 GetVectorData(Eye_Options eyeOption)
            {


                //Find and return the gaze data
                OVRPlugin.EyeGazesState _currentEyeGazesState = new OVRPlugin.EyeGazesState();
                if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _currentEyeGazesState))
                    return Vector3.zero;

                var leftEyeGaze = _currentEyeGazesState.EyeGazes[(int)0];
                var rightEyeGaze = _currentEyeGazesState.EyeGazes[(int)1];



                if (!leftEyeGaze.IsValid || !rightEyeGaze.IsValid)
                {
                    Debug.LogWarning("Gaze not valid this frame.");
                    return Vector3.zero;
                }

                var leftPose = leftEyeGaze.Pose.ToOVRPose();
                var rightPose = rightEyeGaze.Pose.ToOVRPose();
                leftPose = leftPose.ToHeadSpacePose();
                rightPose = rightPose.ToHeadSpacePose();

                // Step 1: both gaze vector in local space
                Vector3 leftOrigin = leftPose.position;
                Vector3 leftDir = (leftPose.orientation * Vector3.forward).normalized;

                Vector3 rightOrigin = rightPose.position;
                Vector3 rightDir = (rightPose.orientation * Vector3.forward).normalized;

                switch (eyeOption)
                {
                    case Eye_Options.Left:
                        return leftDir;
                        break;
                    case Eye_Options.Right:
                        return rightDir;
                        break;
                    case Eye_Options.Combined:
                        //intersection implementation
                        float tL, tR;
                        bool success = CalculateVectorVectorIntersection(
                            leftOrigin, rightOrigin,
                            leftDir, rightDir,
                            out tL, out tR);

                        Vector3 fixation = Vector3.zero;

                        if (success)
                        {
                            Vector3 pointA = leftOrigin + tL * leftDir;
                            Vector3 pointB = rightOrigin + tR * rightDir;
                            fixation = (pointA + pointB) * 0.5f;
                            Debug.Log("pointA value: " + pointA + " pointB value: " + pointB + "Fixation value: " + fixation);
                        }
                        else
                        {
                            // when tracking fails
                            Debug.LogWarning("Gaze fusion failed: gaze rays may be nearly parallel or unstable.");
                        }
                        Vector3 localDirection = (fixation - Vector3.zero).normalized;
                        return localDirection;
                        break;
                    default:
                        Debug.LogError("Unexpected eye option provided in DataSource_Eyes's DataStructure.");
                        return Vector3.zero;
                        break;

                }

            }
            public bool CalculateVectorVectorIntersection(
                Vector3 leftOrigin, Vector3 rightOrigin,
                Vector3 leftDir, Vector3 rightDir,
                out float tLeft, out float tRight)
            {
                Vector3 originDelta = leftOrigin - rightOrigin;

                tLeft = 0.0f;
                tRight = 0.0f;

                // dot products
                float r4dotr4 = Vector3.Dot(rightDir, rightDir);     // R4톀4
                float r2dotr2 = Vector3.Dot(leftDir, leftDir);       // R2톀2
                float r2dotr4 = Vector3.Dot(leftDir, rightDir);      // R2톀4

                // check denominator: (R2톀4)^2 - (R2톀2)(R4톀4)
                float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);

                if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
                    return false;

                tRight = ((Vector3.Dot(originDelta, leftDir) * r4dotr4 -
                        Vector3.Dot(originDelta, rightDir) * r2dotr4)) / denom;

                tLeft = (Vector3.Dot(originDelta, leftDir) + tRight * r2dotr2) / r2dotr4;

                return true;
            }














        }






        /*=========================================================================================================================*/
        /// <summary>
        /// Eye Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Eye Options")]
            [Tooltip("LeftEyeDir")]
            public bool leftEyeDir = true;
            [Tooltip("RightEyeDir")]
            public bool rightEyeDir = true;
            [Tooltip("CombinedEyeDir")]
            public bool combinedEyeDir = true;
        }






    }



}

