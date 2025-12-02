using UnityEngine;
using System.Collections.Generic;
using MetaNomads.Interaction;

public class Gaze_Interactor : MonoBehaviour
{

    [SerializeField]
    private List<GameObject> raycastHits = new List<GameObject>();


    [SerializeField]
    private bool verboseLogging = false;

    [SerializeField]
    private List<LayerMask> layerMasksToIgnore = new List<LayerMask>();

    private LayerMask layerMask;

    private GameObject currentlyGazedObject;

    void Awake()
    {
        for (int i = 0; i < layerMasksToIgnore.Count; i++)
        {
            layerMask = layerMask | layerMasksToIgnore[i];
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, ~layerMask))
        {
            Log("Raycast has hit an object named: " + hit.transform.gameObject.name);
            currentlyGazedObject = hit.transform.gameObject;
            if (verboseLogging)
            {
                bool newObj = true;
                for (int i = 0; i < raycastHits.Count; i++)
                {
                    if (raycastHits[i] == hit.transform.gameObject)
                    {
                        newObj = false;
                        break;
                    }
                }
                if (newObj)
                {
                    raycastHits.Add(hit.transform.gameObject);
                }
            }
        }
        else
        {
            currentlyGazedObject = null;
        }

        if (verboseLogging)
        {
            Debug.DrawRay(transform.position, transform.forward * 50, Color.magenta);

        }
    }

    public GameObject GetCurrentlyGazedObject()
    {
        return currentlyGazedObject;
    }



    //Used for logging without the clutter
    private void Log(string message)
    {
        if (verboseLogging)
        {
            Debug.Log(message);
        }
    }
}
