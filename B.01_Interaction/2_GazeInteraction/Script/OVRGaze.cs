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
    private OVRCameraRig _cameraRig;

    public Transform GetEyeTransform(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                Debug.Log("Left eye transform rotation from camera " + _cameraRig.leftEyeAnchor.rotation);
                return _leftEyeGaze.transform;
            case Eye.Right:
                Debug.Log("Right eye transform rotation from camera " + _cameraRig.rightEyeAnchor.rotation);
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
        Vector3 eyeOrigin = (_leftEyeGaze.transform.position + _rightEyeGaze.transform.position) * 0.5f;
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

        Transform leftEye = _leftEyeGaze.transform;
        Transform rightEye = _rightEyeGaze.transform;

        Vector3 combinedEyePosition = Vector3.zero;
        float tLeft;
        float tRight;

        Vector3 originDelta = leftEye.transform.position - rightEye.transform.position;
        tLeft = 0.0f;
        tRight = 0.0f;

        //Dot Products
        float r4dotr4 = Vector3.Dot(rightEye.transform.forward, rightEye.transform.forward);     // R4톀4
        float r2dotr2 = Vector3.Dot(leftEye.transform.forward, leftEye.transform.forward);       // R2톀2
        float r2dotr4 = Vector3.Dot(leftEye.transform.forward, rightEye.transform.forward);      // R2톀4

        // check denominator: (R2톀4)^2 - (R2톀2)(R4톀4)
        float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);
        if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
            return Vector3.zero;

        tRight = ((Vector3.Dot(originDelta, leftEye.transform.forward) * r4dotr4 -
        Vector3.Dot(originDelta, rightEye.transform.forward) * r2dotr4)) / denom;

        tLeft = (Vector3.Dot(originDelta, leftEye.transform.forward) + tRight * r2dotr2) / r2dotr4;

        Vector3 pointA = leftEye.transform.position + tLeft * leftEye.transform.forward;
        Vector3 pointB = rightEye.transform.position + tRight * rightEye.transform.forward;
        combinedEyePosition = (pointA + pointB) * 0.5f;



        return combinedEyePosition;

    }

}
