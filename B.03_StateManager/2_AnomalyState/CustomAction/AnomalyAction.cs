using UnityEngine;

namespace MetaFrame.State
{
    // ── AnomalyAction — subclass this to create custom anomaly actions ─────────────
    // Place on the same GameObject as AnomalyStateManager. Wire RunAnomalyAction() to a binding's Actions list.
    //
    // 1. ONE-SHOT  — do the thing, return. No extra calls needed.
    //    class FadeObject : AnomalyAction {
    //        public Renderer r;
    //        protected override void Execute() => r.enabled = false;
    //    }
    //
    // 2. ASYNC / TIMED  — set IsAsync, run a coroutine, call CompleteAnomalyAction() at the end.
    //    class FlickerLight : AnomalyAction {
    //        public Light l; public float duration = 3f;
    //        protected override bool IsAsync => true;
    //        protected override void Execute() => StartCoroutine(Flicker());
    //        public override void CancelAnomalyAction() { StopAllCoroutines(); l.enabled = true; CompleteAnomalyAction(); }
    //        IEnumerator Flicker() { for (float t=0; t<duration; t+=0.1f) { l.enabled=!l.enabled; yield return new WaitForSeconds(0.1f); } l.enabled=true; CompleteAnomalyAction(); }
    //    }
    //
    // 3. ASYNC / CONDITION  — complete when external state changes, not on a timer.
    //    class WaitForAudio : AnomalyAction {
    //        public AudioSource src; public AudioClip clip;
    //        protected override bool IsAsync => true;
    //        protected override void Execute() { src.PlayOneShot(clip); StartCoroutine(Wait()); }
    //        public override void CancelAnomalyAction() { StopAllCoroutines(); src.Stop(); CompleteAnomalyAction(); }
    //        IEnumerator Wait() { yield return new WaitWhile(() => src.isPlaying); CompleteAnomalyAction(); }
    //    }
    //
    // 4. ASYNC / MANAGER-DRIVEN  — loop until the anomaly state changes, then self-stop.
    //    class SpinObject : AnomalyAction {
    //        public Transform t; public float speed = 90f;
    //        protected override bool IsAsync => true;
    //        protected override void Execute() => StartCoroutine(Spin());
    //        IEnumerator Spin() { while (Manager.CurrentAnomalyState == AnomalyState.Triggered) { t.Rotate(Vector3.up, speed * Time.deltaTime); yield return null; } CompleteAnomalyAction(); }
    //    }

    public abstract class AnomalyAction : UnityEngine.MonoBehaviour
    {
        private AnomalyStateManager _manager;
        protected AnomalyStateManager Manager => _manager;

        // ── Lifecycle ──────────────────────────────────────────────

        protected virtual void Awake()
        {
            _manager = GetComponent<AnomalyStateManager>();

            if (_manager == null)
                UnityEngine.Debug.LogError(
                    $"[AnomalyAction] No AnomalyStateManager found on '{gameObject.name}'. " +
                    "Add one to the same GameObject.", this);
        }

        // ── Entry point — wire this to the binding's Actions UnityEvent ───────────

        public void RunAnomalyAction()
        {
            if (_manager == null)
            {
                UnityEngine.Debug.LogError(
                    $"[AnomalyAction] Cannot run '{GetType().Name}' — no AnomalyStateManager.", this);
                return;
            }

            _manager.RegisterActiveAction(this);

            if (IsAsync)
                _manager.RegisterPendingAction();

            Execute();

            if (!IsAsync)
                _manager.UnregisterActiveAction(this);
        }

        // ── Overrideable API ──────────────────────────────────────

        /// <summary>Your action logic goes here.</summary>
        protected abstract void Execute();

        /// <summary>
        /// Return true if Execute() starts a coroutine, animation, or anything
        /// that outlives the current frame. The manager waits for CompleteAnomalyAction()
        /// before advancing to Completed state. Defaults to false (one-shot).
        /// </summary>
        protected virtual bool IsAsync => false;

        /// <summary>
        /// Called automatically if the anomaly is cancelled mid-run.
        /// Override to stop coroutines, reset visuals, etc.
        /// </summary>
        public virtual void CancelAnomalyAction() { }

        // ── Helpers for subclasses ────────────────────────────────

        /// <summary>
        /// Call this at the end of your async work to signal completion.
        /// Has no effect for one-shot actions (IsAsync == false).
        /// </summary>
        protected void CompleteAnomalyAction()
        {
            if (!IsAsync) return;
            _manager?.UnregisterActiveAction(this);
            _manager?.SignalActionComplete();
        }
    }
}