namespace MetaFrame.State
{
    /// <summary>
    /// Implement this on any MonoBehaviour to use it as a custom trigger.
    /// Drag the component into a binding's Custom Trigger list.
    ///
    /// Example:
    ///   public class PlayerNearMirrorTrigger : MonoBehaviour, IAnomalyTrigger
    ///   {
    ///       public Transform player;
    ///       public float radius = 2f;
    ///
    ///       public bool Evaluate() =>
    ///           Vector3.Distance(transform.position, player.position) <= radius;
    ///   }
    /// </summary>
    public interface IAnomalyTrigger
    {
        bool Evaluate();
    }
}