// ── ITrackableAction ──────────────────────────────────────────────────────────
// Implement this on any MonoBehaviour that performs async work (animation,
// audio, coroutines, etc.) so CollisionTrigger can track its completion
// via a single RunTracked() call.
//
// Example:
//   public class FadeOut : MonoBehaviour, ITrackableAction
//   {
//       public void Run(System.Action onComplete) => StartCoroutine(Run(onComplete));
//       private IEnumerator Fade(System.Action onComplete) { ... yield ...; onComplete(); }
//   }

public interface ITrackableAction
{
    /// <summary>
    /// Start the action. Call onComplete exactly once when finished.
    /// </summary>
    void Run(System.Action onComplete);
}