using UnityEngine;
using System.Collections;

namespace MetaFrame.State
{
    // FIX (S-7): file was internal-access (no `public`) and had no null guards on
    // the Inspector-assigned Light reference. Promoted to public for consistency
    // with sibling AnomalyAction subclasses, added defensive null-check, and
    // expanded the formatting so the body matches the rest of the codebase
    // rather than the one-liner doc-example style.

    public class FlickerLight : AnomalyAction
    {
        [SerializeField] private Light l;
        [SerializeField] private float duration = 3f;

        protected override bool IsAsync => true;

        protected override void Execute()
        {
            if (l == null)
            {
                Debug.LogError(
                    $"[FlickerLight:{name}] Light reference is not assigned. " +
                    "Aborting flicker; calling CompleteAnomalyAction() so the " +
                    "AnomalyStateManager isn't left waiting.", this);
                CompleteAnomalyAction();
                return;
            }
            StartCoroutine(Flicker());
        }

        public override void CancelAnomalyAction()
        {
            StopAllCoroutines();
            if (l != null) l.enabled = true;
            CompleteAnomalyAction();
        }

        private IEnumerator Flicker()
        {
            for (float t = 0; t < duration; t += 0.1f)
            {
                l.enabled = !l.enabled;
                yield return new WaitForSeconds(0.1f);
            }
            l.enabled = true;
            CompleteAnomalyAction();
        }
    }
}