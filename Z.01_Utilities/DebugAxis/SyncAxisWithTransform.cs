using UnityEngine;

namespace MetaFrame.Utilities
{
    public class SyncAxisWithTransform : MonoBehaviour
    {
        [SerializeField] private Transform _objectToFollow;

        void LateUpdate()
        {
            if (_objectToFollow == null)
                return;

            // Update the position and rotation of the target object to follow the palm
            this.transform.position = _objectToFollow.position;
            this.transform.rotation = _objectToFollow.rotation;
        }
    }
}
