using UnityEngine;
using System.Collections;

namespace MetaFrame.State
{

   class FlickerLight : AnomalyAction {
       public Light l; public float duration = 3f;
       protected override bool IsAsync => true;
       protected override void Execute() => StartCoroutine(Flicker());
       public override void CancelAnomalyAction() { StopAllCoroutines(); l.enabled = true; CompleteAnomalyAction(); }
       IEnumerator Flicker() { for (float t=0; t<duration; t+=0.1f) { l.enabled=!l.enabled; yield return new WaitForSeconds(0.1f); } l.enabled=true; CompleteAnomalyAction(); }
   }
}