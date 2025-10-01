using System;
using System.Collections.Generic;
using UnityEngine;




namespace MetaFrame.Data
{
    public class DataSource_Eyes : DataSourceBase<DataSource_Eyes.DataStructure, DataSource_Eyes.RecordingConfig>
    {

        public override string SourceName => "Eyes";
        private bool _eyeTrackingFunctional = false;

        [SerializeField] internal OVREyeGaze _leftEyeGaze;
        [SerializeField] internal OVREyeGaze _rightEyeGaze;



        public void Update()
        {
            Debug.Log("Left eye transform: " + _leftEyeGaze.gameObject.name + " - " + _leftEyeGaze.transform.position + " Rotation: " + _leftEyeGaze.transform.rotation + " orientation: " + _leftEyeGaze.transform.forward);
        }

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _leftEyeGaze, _rightEyeGaze);
        }



        public override Dictionary<string, object> CollectData()
        {

            Debug.Log("Eye Referenced");
            var data = new Dictionary<string, object>();

            //In case there is a way to ensure the eye tracker is functional, add it here.
            if (true)
            {
                //Left Eye Vector
                if (RecordConfig.LeftEye)
                {
                    data["LeftEye"] = GetTransformDataWithGaze(Data.LeftEyeTransform, Data.LeftEyeGazeVector);
                }
                if (RecordConfig.RightEye)
                {
                    data["RightEye"] = GetTransformDataWithGaze(Data.RightEyeTransform, Data.RightEyeGazeVector);
                }
                if (RecordConfig.CombinedEye)
                {
                    data["CombinedEye"] = GetPositionDataWithGaze(Data.CombinedEyePosition, Data.CombinedEyeGazeVector);
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
            private readonly OVREyeGaze _rightEye;
            private readonly OVREyeGaze _leftEye;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Eyes source, OVREyeGaze leftEye, OVREyeGaze rightEye)
            {
                _source = source;
                _leftEye = leftEye;
                _rightEye = rightEye;
            }

            //Left Eye
            
            public Transform LeftEyeTransform => _leftEye.transform;
            public Vector3 LeftEyeGazeVector => _leftEye.transform.forward;


            //Right Eye
            public Transform RightEyeTransform => _rightEye.transform;
            public Vector3 RightEyeGazeVector => _rightEye.transform.forward;


            //Combined Eyes
            public Vector3 CombinedEyePosition => CalculateCombinedEyePosition();
            public Vector3 CombinedEyeGazeVector => Vector3.zero;

            public Vector3 CalculateCombinedEyePosition()
            {

                Vector3 combinedEyePosition = Vector3.zero;
                float tLeft;
                float tRight;

                Vector3 originDelta = _leftEye.transform.position - _rightEye.transform.position;
                tLeft = 0.0f;
                tRight = 0.0f;

                //Dot Products
                float r4dotr4 = Vector3.Dot(_rightEye.transform.forward, _rightEye.transform.forward);     // R4톀4
                float r2dotr2 = Vector3.Dot(_leftEye.transform.forward, _leftEye.transform.forward);       // R2톀2
                float r2dotr4 = Vector3.Dot(_leftEye.transform.forward, _rightEye.transform.forward);      // R2톀4

                // check denominator: (R2톀4)^2 - (R2톀2)(R4톀4)
                float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);
                if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
                    return Vector3.zero;

                tRight = ((Vector3.Dot(originDelta, _leftEye.transform.forward) * r4dotr4 -
                Vector3.Dot(originDelta, _rightEye.transform.forward) * r2dotr4)) / denom;

                tLeft = (Vector3.Dot(originDelta, _leftEye.transform.forward) + tRight * r2dotr2) / r2dotr4;

                Vector3 pointA = _leftEye.transform.position + tLeft * _leftEye.transform.forward;
                Vector3 pointB = _rightEye.transform.position + tRight * _rightEye.transform.forward;
                combinedEyePosition = (pointA + pointB) * 0.5f;



                return combinedEyePosition;

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
            [Tooltip("Left Eye")]
            public bool LeftEye = true;
            [Tooltip("Right Eye")]
            public bool RightEye = true;
            [Tooltip("Combined Eyes")]
            public bool CombinedEye = true;
        }






    }



}

