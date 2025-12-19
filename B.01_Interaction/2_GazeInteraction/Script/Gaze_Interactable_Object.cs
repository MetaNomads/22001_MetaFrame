using UnityEngine;
using MetaFrame.Data;

namespace MetaFrame.Interaction
{   
    public class Gaze_Interactable_Object : MonoBehaviour
    {

        //Keeps track of if the object is currently hovered upon
        private bool currentlyHovered = false;

        //Keeps track of if the obj is currently gazed upon
        private bool gazedUpon = false;

        //Timer to go from just hovered on to hovered and gazed upon
        private const float GAZE_TIME = 3;
        private float gazeTimer = 0;
        private bool gazeTimerActive = false;
        private Coroutine GazeCheck = null;

        //TODO: add ref to datasource_interactable

        //In the case that the object is transparent
        [SerializeField]
        [Header("Object Transparency")]
        [Tooltip("Check this box if the object is transparent!")]
        private bool PassthroughObject = false;

        //To ensure the correct collider is hit
        [SerializeField]
        [Header("Object Gaze/Hover Collider")]
        [Tooltip("Make sure to add a reference to a collider here or this script won't work!")]
        private Collider GazeCollider;

        private void Start()
        {
            //Check if the reference to the Gaze Collider is present
            if (GazeCollider == null)
            {
                Debug.LogError($"No gaze collider reference is provided. {gameObject.name} will not be hoverable or gazed upon.");
                this.enabled = false;
            }
        }

        /// <summary>
        // Get function for the object's transparency variable PassthroughObject
        /// <summary>
        public bool IsPassthrough()
        {
            return PassthroughObject;
        }

        /// <summary>
        //  Function to handling being hovered upon. Called by eye raycasts. 
        /// <summary>
        public void OnHoverEnter()
        {
            currentlyHovered = true;
            OnHoverEnterActions();
            //Add other universal methods here if needed
        }
        /// <summary>
        //  Function used for unique actions when object is hovered upon.
        /// <summary>
        virtual protected void OnHoverEnterActions()
        {
            //Implement unique hover actions for objects in CHILD scripts!
        }

        private void OnTriggerExit(Collider other)
        {

        }



    }
}