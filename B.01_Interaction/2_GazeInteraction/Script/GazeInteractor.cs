using UnityEngine;
using System.Collections.Generic;
using MetaNomads.Interaction;
using UnityEngine.Rendering;
using MetaFrame.Utilities.Editor;

namespace MetaFrame.Interaction.GazeInteraction
{
    public class GazeInteractor : MonoBehaviour
    {

        //For managing the Raycast Layer Mask
        [Header("set layer masks to ignore for raycast")]
        [SerializeField]
        private List<LayerMask> layerMasksToIgnore = new List<LayerMask>();
        private LayerMask layerMask;

        //For Logging and Testing
        [Header("realtime debugging")]
        [SerializeField]
        private bool showRayAndHitList = false;

        [Tooltip("When enabled, the raycast will hit trigger colliders.\n" +
                 "When disabled, trigger colliders are ignored regardless of the global Physics setting.")]
        [SerializeField]
        private bool hitTriggerColliders = false;
        [Header("display raycast hits list, DO NOT edit")]
        public List<GameObject> raycastHits = new List<GameObject>();


        //For keeping track of data
        private GameObject currentGazeInteractable;
        private Vector3? collisionPoint = null;
        private bool isColliding = false;

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
            //Shoot the raycast from this GameObject's transform
            (bool, GameObject, Vector3) raycastData = ShootRaycast();
            isColliding = raycastData.Item1;
            currentGazeInteractable = raycastData.Item2;
            collisionPoint = raycastData.Item3;

            //Draw raycast line for testing
            if (showRayAndHitList)
            {
                Debug.DrawRay(transform.position, transform.forward * 50, Color.magenta);
            }
        }

        private (bool, GameObject, Vector3) ShootRaycast()
        {
            //Shoot Raycast from this GameObject's transform
            RaycastHit hit;
            QueryTriggerInteraction triggerInteraction = hitTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

            if (Physics.Raycast(transform.position, transform.forward, out hit, Mathf.Infinity, ~layerMask, triggerInteraction))
            {
                //For logging. It's inefficient so make sure to uncheck "Verbose Logging" in the inspector when not testing the script.
                Log("Raycast has hit an object named: " + hit.transform.gameObject.name);
                
                if (showRayAndHitList)
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

        //Get function for the currently Gazed Game Object
        public GameObject GetGazeInteractable()
        {
            if (isColliding)
            {
                return currentGazeInteractable;
            }
            else
            {
                return null;
            }
        }

        //Get function for the collision point
        public Vector3? GetCollisionPoint()
        {
            if (isColliding)
            {
                return collisionPoint;
            }
            else
            {
                return null;
            }
        }

        //Check if currently colliding with any object
        public bool IsColliding()
        {
            return isColliding;
        }

        //Used for logging without the clutter
        private void Log(string message)
        {
            if (showRayAndHitList)
            {
                Debug.Log(message);
            }
        }
    }
}