using UnityEngine;

namespace MetaNomads.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class UnlockZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
    Debug.Log($"[UnlockZone] OnTriggerEnter — {other.gameObject.name}");
    var grab = other.GetComponentInParent<LockableHandGrab>();
    Debug.Log($"[UnlockZone] grab found: {grab != null}");
    grab?.AllowRelease();
        }

        private void OnTriggerExit(Collider other)
        {
            var grab = other.GetComponentInParent<LockableHandGrab>();
            grab?.RevokeRelease();
        }
    }
}