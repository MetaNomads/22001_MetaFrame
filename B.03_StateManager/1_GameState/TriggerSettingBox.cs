using UnityEngine;

public class TriggerSetting : MonoBehaviour
{
    [SerializeField] private SpawnMechanism spawnMechanism;
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Cup")) return;
        spawnMechanism.DeactivateCup();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Cup")) return;

     
    }
}

