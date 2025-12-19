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
            var raycastData = ShootRaycast();
            isColliding = raycastData.hit;
            currentGazeInteractable = raycastData.obj;
            collisionPoint = raycastData.point;

            //Draw raycast line for testing
            if (showRayAndHitList)
            {
                Debug.DrawRay(transform.position, transform.forward * 50, Color.magenta);
            }
        }

        private (bool hit, GameObject obj, Vector3? point) ShootRaycast()
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, Mathf.Infinity, ~layerMask))
            {
                return (true, hitInfo.transform.gameObject, hitInfo.point);
            }

            return (false, null, null);
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