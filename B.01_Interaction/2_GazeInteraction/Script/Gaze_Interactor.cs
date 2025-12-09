using UnityEngine;
using System.Collections.Generic;
using MetaNomads.Interaction;
using UnityEngine.Rendering;

public class Gaze_Interactor : MonoBehaviour
{


    //For Logging and Testing
    [SerializeField]
    private List<GameObject> raycastHits = new List<GameObject>();

    [SerializeField]
    private bool verboseLogging = false;


    //For managing the Raycast Layer Mask
    [SerializeField]
    private List<LayerMask> layerMasksToIgnore = new List<LayerMask>();
    private LayerMask layerMask;

    //For getting the references to shoot the raycasts from
    [SerializeField]
    private Transform eyeTransform;
    [SerializeField]
    private Transform headTransform;
    [SerializeField]
    private Transform chestTransform;

    //For keeping track of data
    private GameObject eyeCurrentlyGazedObject;
    private Vector3 eyecollisionPoint = Vector3.zero;
    private bool eyeCurrentlyColliding = false;

    private GameObject headCurrentlyGazedObject;
    private Vector3 headcollisionPoint = Vector3.zero;
    private bool headCurrentlyColliding = false;

    private GameObject chestCurrentlyGazedObject;
    private Vector3 chestcollisionPoint = Vector3.zero;
    private bool chestCurrentlyColliding = false;
    
    //For retrieving data
    public enum Interactors
    {
        HEAD,
        CHEST,
        EYE,
        NULL
    }

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
        //Shoot the eye raycast
        (bool, GameObject, Vector3) eyeData = ShootRaycast(eyeTransform);
        eyeCurrentlyColliding = eyeData.Item1;
        eyeCurrentlyGazedObject = eyeData.Item2;
        eyecollisionPoint = eyeData.Item3;

        //Shoot the head raycast
        (bool, GameObject, Vector3) headData = ShootRaycast(headTransform);
        headCurrentlyColliding = headData.Item1;
        headCurrentlyGazedObject = headData.Item2;
        headcollisionPoint = headData.Item3;

        //Shoot the chest raycast
        (bool, GameObject, Vector3) chestData = ShootRaycast(chestTransform);
        chestCurrentlyColliding = chestData.Item1;
        chestCurrentlyGazedObject = chestData.Item2;
        chestcollisionPoint = chestData.Item3;

        //Draw raycast lines for testing
        if (verboseLogging)
        {
            Debug.DrawRay(eyeTransform.position, eyeTransform.forward * 50, Color.magenta);
            Debug.DrawRay(headTransform.position, headTransform.forward * 50, Color.aquamarine);
            Debug.DrawRay(chestTransform.position, chestTransform.forward * 50, Color.beige);

        }
    }

    private (bool, GameObject, Vector3) ShootRaycast(Transform originTransform)
    {
        //Shoot Raycast
        RaycastHit hit;
        if (originTransform != null && Physics.Raycast(originTransform.position, originTransform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, ~layerMask))
        {
            //For logging. It's inefficient so make sure to uncheck "Verbose Logging" in the inspector when not testing the script.
            Log("Raycast has hit an object named: " + hit.transform.gameObject.name);
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

            return (true, hit.transform.gameObject, hit.point);
        }
        else
        {
            return (false, null, Vector3.zero);
        }

    }

    //Get functions for the currently Gazed Game Object
    public GameObject GetCurrentlyGazedObject(Interactors interactor)
    {
        return GetCurrentlyGazedObjectInternal(interactor);
    }
    public GameObject GetCurrentlyGazedObject(Transform interactorTransform)
    {

        Interactors interactor = GetInteractorType(interactorTransform);
        return GetCurrentlyGazedObjectInternal(interactor);
        
    }

    //Utility function which actually gets the interactor object from one of the two get calls
    private GameObject GetCurrentlyGazedObjectInternal(Interactors interactor)
    {
        switch (interactor)
        {
            case Interactors.EYE:
                if (eyeCurrentlyColliding)
                {
                    return eyeCurrentlyGazedObject;
                }
                else
                {
                    return null;
                }
            case Interactors.HEAD:
                if (headCurrentlyColliding)
                {
                    return headCurrentlyGazedObject;
                }
                else
                {
                    return null;
                }
            case Interactors.CHEST:
                if (chestCurrentlyColliding)
                {
                    return chestCurrentlyGazedObject;
                }
                else
                {
                    return null;
                }
            default:
                Debug.LogError("Invalid interactor" + interactor + " request given to GetCurrentlyGazedObject()");
                return null;

        }
    }
    

    public Vector3? GetCollisionPoint(Interactors interactor)
    {
        return GetCollisionPointInternal(interactor);
    }
    public Vector3? GetCollisionPoint(Transform interactorTransform)
    {
        Interactors interactor = GetInteractorType(interactorTransform);
        return GetCollisionPointInternal(interactor);
    }


    public Vector3? GetCollisionPointInternal(Interactors interactor)
    {
        switch (interactor)
        {
            case Interactors.EYE:
                if (eyeCurrentlyColliding)
                {
                    return eyecollisionPoint;
                }
                else
                {
                    return null;
                }
            case Interactors.HEAD:
                if (headCurrentlyColliding)
                {
                    return headcollisionPoint;
                }
                else
                {
                    return null;
                }
            case Interactors.CHEST:
                if (chestCurrentlyColliding)
                {
                    return chestcollisionPoint;
                }
                else
                {
                    return null;
                }
            default:
                Debug.LogError("Invalid interactor" + interactor + " request given to GetCollisionPoint()");
                return null;

        }
    }


    //Helper function to convert a given transform to the correct interactor
    private Interactors GetInteractorType(Transform interactorTransform)
    {

        if (interactorTransform == eyeTransform)
        {
            return Interactors.EYE;
        }
        else if (interactorTransform == headTransform)
        {
            return Interactors.HEAD;
        }
        else if (interactorTransform == chestTransform)
        {
            return Interactors.CHEST;
        }
        else
        {
            Debug.LogError("Transform for object " + transform.name + " is not equal to any transforms such as " + eyeTransform.name + " , " + headTransform.name + " , or " + chestTransform.name);
            return Interactors.NULL;
        }


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

