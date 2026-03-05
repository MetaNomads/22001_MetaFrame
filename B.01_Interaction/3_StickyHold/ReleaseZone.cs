using UnityEngine;

namespace MetaNomads.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class ReleaseZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var sticky = StickyGrab.GetStickyGrabForCollider(other);
            sticky?.AllowRelease();
        }

        private void OnTriggerExit(Collider other)
        {
            var sticky = StickyGrab.GetStickyGrabForCollider(other);
            sticky?.RevokeRelease();
        }
    }
}