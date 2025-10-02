using UnityEngine;

public class OVREyes : MonoBehaviour
{

    public enum Eye
    {
        Left,
        Right,
        Combined
    }

    [SerializeField] private OVREyeGaze _leftEyeGaze;

    [SerializeField] private OVREyeGaze _rightEyeGaze;

    public Transform GetEyeTransform(Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return _leftEyeGaze.transform;
            case Eye.Right:
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

        // get head position rotation
        Vector3 headPosition = Camera.main.transform.position;
        Quaternion headRotation = Camera.main.transform.rotation;

        return (headRotation * (CalculateCombinedEyePosition() - Vector3.zero).normalized).normalized;
    }

    public Vector3 CalculateCombinedEyePosition()
    {

        Vector3 combinedEyePosition = Vector3.zero;
        float tLeft;
        float tRight;

        Vector3 originDelta = _leftEyeGaze.transform.position - _rightEyeGaze.transform.position;
        tLeft = 0.0f;
        tRight = 0.0f;

        //Dot Products
        float r4dotr4 = Vector3.Dot(_rightEyeGaze.transform.forward, _rightEyeGaze.transform.forward);     // R4톀4
        float r2dotr2 = Vector3.Dot(_leftEyeGaze.transform.forward, _leftEyeGaze.transform.forward);       // R2톀2
        float r2dotr4 = Vector3.Dot(_leftEyeGaze.transform.forward, _rightEyeGaze.transform.forward);      // R2톀4

        // check denominator: (R2톀4)^2 - (R2톀2)(R4톀4)
        float denom = Mathf.Pow(r2dotr4, 2f) - (r2dotr2 * r4dotr4);
        if (r2dotr4 < Mathf.Epsilon || Mathf.Abs(denom) < Mathf.Epsilon)
            return Vector3.zero;

        tRight = ((Vector3.Dot(originDelta, _leftEyeGaze.transform.forward) * r4dotr4 -
        Vector3.Dot(originDelta, _rightEyeGaze.transform.forward) * r2dotr4)) / denom;

        tLeft = (Vector3.Dot(originDelta, _leftEyeGaze.transform.forward) + tRight * r2dotr2) / r2dotr4;

        Vector3 pointA = _leftEyeGaze.transform.position + tLeft * _leftEyeGaze.transform.forward;
        Vector3 pointB = _rightEyeGaze.transform.position + tRight * _rightEyeGaze.transform.forward;
        combinedEyePosition = (pointA + pointB) * 0.5f;



        return combinedEyePosition;

    }

}
