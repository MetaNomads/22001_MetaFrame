using Unity.Mathematics;
using Unity.XR.CoreUtils;
using UnityEngine;

public class Gaze: MonoBehaviour
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

    public Vector3 GetEyePosition(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return _leftEyeGaze.transform.GetWorldPose().position;
            case Eye.Right:
                return _rightEyeGaze.transform.GetWorldPose().position;
            case Eye.Combined:
                return GetCombinedEyePosition();
            default:
                return Vector3.zero;
        }
    }

    public Quaternion GetGazeRotation(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return _leftEyeGaze.transform.localRotation;
            case Eye.Right:
                return _rightEyeGaze.transform.localRotation;
            case Eye.Combined:
                // Average the gaze directions of both eyes for combined gaze
                return GetCombinedGazeRotation();
            default:
                return new Quaternion(0, 0, 0, 0);
        }
    }

    public Vector3 GetEyeForward(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return _leftEyeGaze.transform.forward;
            case Eye.Right:
                return _rightEyeGaze.transform.forward;
            case Eye.Combined:
                return GetCombinedGazeForward();
            default:
                return Vector3.zero;
        }
    }

    public Vector3 GetCombinedEyePosition()
    {
        ///// Get the midpoint between the two eyes
        Vector3 conbinedEyeOrigin = (_leftEyeGaze.transform.GetWorldPose().position + _rightEyeGaze.transform.GetWorldPose().position) * 0.5f;
        return conbinedEyeOrigin;
    }
    public Vector3 GetCombinedGazeForward()
    {
        Vector3 combinedEyeOrigin = GetCombinedEyePosition();

        //reconstruct gameobject from OVREye
        Pose leftEye = new Pose(
           _leftEyeGaze.transform.localPosition,
           _leftEyeGaze.transform.localRotation
       );
        Pose rightEye = new Pose(
            _rightEyeGaze.transform.localPosition,
            _rightEyeGaze.transform.localRotation
        );

        //return gazeDirection
        Vector3 combinedGazeForward = Vector3.zero;
        combinedGazeForward = (leftEye.forward + rightEye.forward) / 2;
        Debug.DrawRay(combinedEyeOrigin, combinedGazeForward * 50, Color.orange);
        return combinedGazeForward.normalized;
    }
    public Quaternion GetCombinedGazeRotation()
    {
        Quaternion CombinedGazeRoatation = Quaternion.Slerp(_leftEyeGaze.transform.localRotation, _rightEyeGaze.transform.localRotation, 0.5f);
        return CombinedGazeRoatation;
    }
}
