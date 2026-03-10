using System.Collections.Generic;
using MetaFrame.Data;
using UnityEngine;

public class SpawnMechanism : MonoBehaviour
{
    [SerializeField] private GameObject cupPrefab;
    [SerializeField] private Vector3 spawnPoint;

    private List<GameObject> spawnedCups = new List<GameObject>();

    public void SpawnCup()
    {
        Debug.Log("Cup Spawned");
        GameObject newCup = Instantiate(
            cupPrefab,
            spawnPoint,
            Quaternion.identity
        );
        spawnedCups.Add(newCup);
    }

    public void DeactivateCup()
    {
        Debug.Log("Cup deactivated");
        spawnedCups[-1].GetComponent<Rigidbody>().isKinematic = true;
    }

    public void DestroyCup()
    {
        foreach (GameObject cup in spawnedCups)
        {
            if (cup != null)
            {
                Destroy(cup);
            }
        }
        spawnedCups.Clear();
    }
}

