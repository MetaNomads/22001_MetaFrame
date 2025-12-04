using Unity.XR.CoreUtils;
using UnityEngine;

namespace MetaFrame.Interaction
{
    public class CenterEye : MonoBehaviour
    {
        [SerializeField] private Transform _leftEye;
        [SerializeField] private Transform _rightEye;

        void LateUpdate()
        {
            if (_leftEye == null || _rightEye == null) return;

            // Average position
            transform.position = (_leftEye.transform.GetWorldPose().position + _rightEye.transform.GetWorldPose().position) / 2f;

            // Average rotation (spherical interpolation halfway)
            transform.rotation = Quaternion.Slerp(_leftEye.localRotation, _rightEye.localRotation, 0.5f);
        }

    }
}
