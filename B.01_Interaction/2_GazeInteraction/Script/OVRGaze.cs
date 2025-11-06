using Unity.XR.CoreUtils;
using UnityEngine;

public class OVRGaze : MonoBehaviour
{

    public enum Eye
    {
        Left,
        Right,
        Combined
    }

    [SerializeField] private OVREyeGaze _leftEyeGaze;

    [SerializeField] private OVREyeGaze _rightEyeGaze;

    [SerializeField] private OVRBody _body;
    [SerializeField] private OVRFaceExpressions _face;

    [SerializeField]
    private GameObject _cameraRig;


    public Transform GetEyeTransform(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                Debug.Log("Left eye transform position from camera " + _leftEyeGaze.transform.GetWorldPose().position);
                Debug.Log("Left eye transform rotation from camera " + _leftEyeGaze.transform.GetWorldPose().rotation.eulerAngles);
                Debug.Log("Left eye transform forward from camera " + _leftEyeGaze.transform.GetWorldPose().forward);


                return _leftEyeGaze.transform;
            case Eye.Right:
                Debug.Log("Right eye transform rotation from camera " + _rightEyeGaze.transform.GetWorldPose().position);
                Debug.Log("Right eye transform rotation from camera " + _rightEyeGaze.transform.GetWorldPose().rotation.eulerAngles);

                return _rightEyeGaze.transform;
            case Eye.Combined:
                Debug.LogWarning("Combined eye does not have a single transform.");
                return null; // Combined eye does not have a single transform
            default:
                return null;
        }
    }

    public Vector3 GetGazeVector(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return _leftEyeGaze.transform.forward;
            case Eye.Right:
                return _rightEyeGaze.transform.forward;
            case Eye.Combined:
                // Average the gaze directions of both eyes for combined gaze
                return CalculateCombinedGazeVector();
            default:
                return new Vector3(0, 0, 0);
        }
    }

    public Vector3 CalculateCombinedGazeVector()
    {
        /// Get the midpoint between the two eyes
        Vector3 eyeOrigin = (_leftEyeGaze.transform.GetWorldPose().position + _rightEyeGaze.transform.GetWorldPose().position) * 0.5f;
        // Get the combined fixation point
        Vector3 fixationPoint = CalculateCombinedEyePosition();
        // Calculate direction from eye origin to fixation point
        Vector3 gazeDirection = (fixationPoint - eyeOrigin).normalized;


        return gazeDirection; 


        // get head position rotation
        //Vector3 headPosition = _face.transform.position; ;
        //Quaternion headRotation = _face.transform.rotation;

        

        //return (headRotation * (CalculateCombinedEyePosition() - Vector3.zero).normalized).normalized;

        //// get head position rotation
        //Vector3 headPosition = Camera.main.transform.position;
        //Quaternion headRotation = Camera.main.transform.rotation;

        //return (headRotation * (CalculateCombinedEyePosition() - Vector3.zero).normalized).normalized;
    }

    public Vector3 CalculateCombinedEyePosition()
    {
        Debug.Log($"Calculate Rig Rotation: {_cameraRig.transform.rotation.eulerAngles}");
        Debug.Log($"Calculate Left Eye LOCAL rot: {_leftEyeGaze.transform.localRotation.eulerAngles}");
        Debug.Log($"Calculate Left Eye WORLD rot: {_leftEyeGaze.transform.rotation.eulerAngles}");
        Debug.Log($"Calculate Left Eye LOCAL forward: {_leftEyeGaze.transform.localRotation * Vector3.forward}");
        Debug.Log($"Calculate Left Eye WORLD forward: {_leftEyeGaze.transform.forward}");
        //Pose leftEye = _leftEyeGaze.transform.GetWorldPose();
        //Pose rightEye = _rightEyeGaze.transform.GetWorldPose();



        // Get local poses - these are NOT affected by rig rotation
        Pose leftEye = new Pose(
            _leftEyeGaze.transform.localPosition,
            _leftEyeGaze.transform.localRotation
        );

        Pose rightEye = new Pose(
            _rightEyeGaze.transform.localPosition,
            _rightEyeGaze.transform.localRotation
        );

        Vector3 leftEyePos = _leftEyeGaze.transform.GetWorldPose().position;
        Vector3 rightEyePos = _rightEyeGaze.transform.GetWorldPose().position;

        //leftEye.rotation = Quaternion.Euler(leftEye.rotation.eulerAngles + _cameraRig.transform.rotation.eulerAngles);
        //rightEye.rotation = Quaternion.Euler(rightEye.rotation.eulerAngles + _cameraRig.transform.rotation.eulerAngles);
        Debug.DrawRay(leftEyePos, leftEye.forward * 50, Color.red);
        Debug.DrawRay(rightEyePos, rightEye.forward * 50, Color.green);

        Vector3 combinedEyePosition = Vector3.zero;
        float tLeft;
        float tRight;

        Vector3 originDelta = leftEyePos - rightEyePos;
        tLeft = 0.0f;
        tRight = 0.0f;

        //Dot Products
        float r4dotr4 = Vector3.Dot(rightEye.forward, rightEye.forward);     // R4톀4
        float r2dotr2 = Vector3.Dot(leftEye.forward, leftEye.forward);       // R2톀2
        float r2dotr4 = Vector3.Dot(leftEye.forward, rightEye.forward);      // R2톀4

        // check denominator: (R2톀4)^2 - (R2톀2)(R4톀4)
        float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);
        if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
            return Vector3.zero;

        tRight = ((Vector3.Dot(originDelta, leftEye.forward) * r4dotr4 -
        Vector3.Dot(originDelta, rightEye.forward) * r2dotr4)) / denom;

        tLeft = (Vector3.Dot(originDelta, leftEye.forward) + tRight * r2dotr2) / r2dotr4;

        Vector3 pointA = leftEyePos + tLeft * leftEye.forward;
        Vector3 pointB = rightEyePos + tRight * rightEye.forward;
        combinedEyePosition = (pointA + pointB) * 0.5f;

        return _cameraRig.transform.TransformPoint(combinedEyePosition);



        //return combinedEyePosition;

    }
}
