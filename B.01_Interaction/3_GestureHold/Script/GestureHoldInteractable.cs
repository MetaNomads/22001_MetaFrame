using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.HandGrab;

namespace MetaFrame.Interaction
{
    /// <summary>
    /// Use a gesture to hold an interactorable, requires hand grab interactable
    /// issue#1 - multiple interactor in Gesture Hold Interactable - https://github.com/MetaFrame/22001_MetaFrame-Unity/issues/1
    /// </summary>
    public class GestureHoldInteractable : MonoBehaviour
    {
        [SerializeField]
        private HandGrabInteractor _HandGrabInteractor;
        [SerializeField]
        private HandGrabInteractable _HandGrabInteractable;

        // FIX (T2-5): null-guard the public UnityEvent entry points so a missing
        // Inspector reference produces a loud log instead of a silent NRE inside
        // a UnityEvent invocation (which Unity swallows and merely warns about
        // in the Console — easy to miss during a participant session).
        public void forceSelect()
        {
            if (_HandGrabInteractor == null || _HandGrabInteractable == null)
            {
                Debug.LogError(
                    $"[GestureHoldInteractable:{name}] forceSelect called but " +
                    $"_HandGrabInteractor={(_HandGrabInteractor == null ? "null" : "ok")}, " +
                    $"_HandGrabInteractable={(_HandGrabInteractable == null ? "null" : "ok")}. " +
                    "Wire both in the Inspector.", this);
                return;
            }
            _HandGrabInteractor.ForceSelect(_HandGrabInteractable);
        }

        public void forceRelease()
        {
            if (_HandGrabInteractor == null)
            {
                Debug.LogError(
                    $"[GestureHoldInteractable:{name}] forceRelease called but " +
                    "_HandGrabInteractor is null. Wire it in the Inspector.", this);
                return;
            }
            _HandGrabInteractor.ForceRelease();
        }

        // FIX (T2-5): surface missing Inspector refs at edit time.
        private void OnValidate()
        {
            if (_HandGrabInteractor == null)
                Debug.LogWarning($"[GestureHoldInteractable:{name}] _HandGrabInteractor unassigned.", this);
            if (_HandGrabInteractable == null)
                Debug.LogWarning($"[GestureHoldInteractable:{name}] _HandGrabInteractable unassigned.", this);
        }
    }
}
