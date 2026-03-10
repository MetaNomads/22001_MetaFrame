using UnityEngine;
using MetaFrame.State;

public class PlayerFacingMirrorCondition : MonoBehaviour, IAnomalyTrigger
{
    public Transform player;
    public float angleThreshold = 25f;

    public bool Evaluate()
    {
        float angle = Vector3.Angle(transform.forward, player.position - transform.position);
        return angle < angleThreshold;
    }
}