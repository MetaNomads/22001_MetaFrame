using UnityEngine;

namespace MetaNomads.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class UnlockGrabZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var grab = other.GetComponentInParent<LockableHandGrab>();
            grab?.AllowRelease();
        }

        private void OnTriggerExit(Collider other)
        {
            var grab = other.GetComponentInParent<LockableHandGrab>();
            grab?.RevokeRelease();
        }
    }
}