using UnityEngine;
using UnityEngine.XR;
using System;
using System.IO;


    public class GazeRayCombined : MonoBehaviour
    {
        [Header("Gaze Ray Settings")]
        [Tooltip("Check to enable gaze tracking and visualization")]
        public float gazeRayLength = 500f;
        public bool isEnabled = true;
        public Transform gazeDebugSphere;

        private LineRenderer lineRenderer;
        private OVRPlugin.EyeGazesState gazeState = new OVRPlugin.EyeGazesState();
        private OVRPlugin.EyeGazesState _currentEyeGazesState;
        
        //Referenced for recording the data
        public Vector3 leftEyeDirectionGazeForRecording = Vector3.zero;
        public Vector3 rightEyeDirectionGazeForRecording = Vector3.zero;
        public Vector3 combinedEyeDirectionGazeForRecording = Vector3.zero;

    void Start()
        {
            if (!isEnabled)
            {
                // Optional: Reset visuals (e.g., hide debug sphere or clear line)
                if (lineRenderer != null)
                {
                    lineRenderer.SetPosition(0, Vector3.zero);
                    lineRenderer.SetPosition(1, Vector3.zero);
                }

                if (gazeDebugSphere != null)
                {
                    gazeDebugSphere.gameObject.SetActive(false);
                }

                // Optional: Log message once (can be removed for performance)
                // Debug.Log("Gaze tracking is disabled.");

                return; // Skip gaze tracking logic
            }
            
            // create LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                GameObject lineObj = new GameObject("GazeRayLine");
                lineObj.transform.SetParent(this.transform);
                lineRenderer = lineObj.AddComponent<LineRenderer>();
            }

            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;

            Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lineMat.color = Color.cyan;
            lineRenderer.material = lineMat;

            // start eye tracking
            if (!OVRPlugin.StartEyeTracking())
            {
                Debug.LogError("Failed to start eye tracking!");
            }
        }

        void Update()
        {
            if (!isEnabled) return;

            // get head position rotation
            Vector3 headPosition = Camera.main.transform.position;
            Quaternion headRotation = Camera.main.transform.rotation;

            //get gaze data
            if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _currentEyeGazesState))
                return;

            var leftEyeGaze = _currentEyeGazesState.EyeGazes[(int)0];
            var rightEyeGaze = _currentEyeGazesState.EyeGazes[(int)1];

            if (!leftEyeGaze.IsValid || !rightEyeGaze.IsValid)
            {
                Debug.LogWarning("Gaze not valid this frame.");
                return;
            }

            var leftPose = leftEyeGaze.Pose.ToOVRPose();
            var rightPose = rightEyeGaze.Pose.ToOVRPose();

            leftPose = leftPose.ToHeadSpacePose();
            rightPose = rightPose.ToHeadSpacePose();

            
            // Step 1: both gaze vector in local space
            Vector3 leftOrigin = leftPose.position;
            Vector3 leftDir = (leftPose.orientation * Vector3.forward).normalized;

        leftEyeDirectionGazeForRecording = leftDir;


            Vector3 rightOrigin = rightPose.position;
            Vector3 rightDir = (rightPose.orientation * Vector3.forward).normalized;

        rightEyeDirectionGazeForRecording = rightDir;

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
            combinedEyeDirectionGazeForRecording = fixation;
                Debug.Log("pointA value: " + pointA + " pointB value: " + pointB + "Fixation value: " + fixation);
            }
            else
            {
                // when tracking fails
                Debug.LogWarning("Gaze fusion failed: gaze rays may be nearly parallel or unstable.");
            }
            
            Vector3 localDirection = (fixation - Vector3.zero).normalized; // Vector3.zero is head local space origin
            Quaternion localRotation = Quaternion.LookRotation(localDirection);
            Vector3 localOrigin = Vector3.zero;

            // change to world space
            Vector3 worldOrigin = headPosition + headRotation * localOrigin;
            Vector3 worldDirection = (headRotation * localDirection).normalized;


            Vector3 endPoint = worldOrigin + worldDirection * gazeRayLength;
            lineRenderer.SetPosition(0, worldOrigin);
            lineRenderer.SetPosition(1, endPoint);
            //Debug.Log("World origin: " + worldOrigin + " world direction: " + worldDirection + "end point: " + endPoint);
            

            if (gazeDebugSphere != null)
            {
                gazeDebugSphere.position = endPoint;
                gazeDebugSphere.rotation = Quaternion.LookRotation(worldDirection);
            }


            //SaveGazeRotationToCsv(localRotation,  @"C:\Users\zhoua\OneDrive\Desktop\gaze_rotation.csv");
        }

        private void SaveGazeRotationToCsv(Quaternion localRotation, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(fs))
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                // timestamp + Quaternion coordinate xyzw 
                writer.WriteLine($"{timestamp},{localRotation.x},{localRotation.y},{localRotation.z},{localRotation.w}");
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
            float r4dotr4 = Vector3.Dot(rightDir, rightDir);     // R4·R4
            float r2dotr2 = Vector3.Dot(leftDir, leftDir);       // R2·R2
            float r2dotr4 = Vector3.Dot(leftDir, rightDir);      // R2·R4

            // check denominator: (R2·R4)^2 - (R2·R2)(R4·R4)
            float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);

            if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
                return false;
                
            tRight = ((Vector3.Dot(originDelta, leftDir) * r4dotr4 - 
                    Vector3.Dot(originDelta, rightDir) * r2dotr4)) / denom;

            tLeft = (Vector3.Dot(originDelta, leftDir) + tRight * r2dotr2) / r2dotr4;

            return true;
        }
    }