using Oculus.Interaction;
using UnityEngine;

public class CollisionSettingSpawn : MonoBehaviour
{
    private bool CupCollision;
    private string Source_T;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Cup"))
        {
            CupCollision = true;
            Debug.Log("Functions Activation: True");
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Cup"))
        {
            CupCollision = false;
            Debug.Log("Functions Activation: False");
        }
    }

    public bool GetCupPresence()
    { 
        return CupCollision; 
    }
}

