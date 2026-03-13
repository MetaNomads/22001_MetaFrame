namespace MetaFrame.State
{
    /// <summary>
    /// Implement this on any MonoBehaviour to use it as a custom condition.
    /// Drag the component into a trigger's Conditions list.
    ///
    /// Example:
    ///   public class PlayerNearMirrorCondition : MonoBehaviour, IAnomalyCondition
    ///   {
    ///       public Transform player;
    ///       public float radius = 2f;
    ///
    ///       public bool Evaluate() =>
    ///           Vector3.Distance(transform.position, player.position) <= radius;
    ///   }
    /// </summary>
    public interface IAnomalyCondition
    {
        bool Evaluate();
    }
}