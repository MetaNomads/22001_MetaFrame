using UnityEngine;

public class OVRCombinedEye : MonoBehaviour
{

    [SerializeField]
    private Gaze ovrGaze;

    // Update is called once per frame
    void Update()
    {

        //TODO: Modify the transform for the combined eye to be an actual middle rather than just the left eye
        transform.position = DetermineCombinedPositionFromOVRGaze();
        transform.rotation = DetermineCombinedRotationFromOVRGaze();

        //For testing purposes
        Debug.DrawRay(transform.position,  (transform.rotation.eulerAngles) * 50, Color.purple);
    }

    private Vector3 DetermineCombinedPositionFromOVRGaze()
    {
        //Temporarily using the left eye position
        return ovrGaze.GetEyePosition(Gaze.Eye.Left);
    }

    private Quaternion DetermineCombinedRotationFromOVRGaze()
    {
        //Temporarily using the right eye position
        return new Quaternion(0,0,0,0);
    }
}
