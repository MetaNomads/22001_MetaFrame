using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

namespace MetaFrame.Interaction
{      
    // a manager to enable interactors from code
    // any new interactor assigned to MetaFrame_Controller should be assigned in this manager
    public class InteractorManager : MonoBehaviour

    {
        /*=========================================================================================================================*/
        /// <summary>
        /// Left Hand Interactor x 7
        /// </summary>
        public Interactor HandPoke_Left;
        public Interactor TouchGrab_Left;
        public Interactor HandRay_Left;
        public Interactor HandGrab_Left;
        public Interactor HandGrabUse_Left;
        public Interactor DistanceHandGrab_Left;

        private void initiateLeft()
        {
            // hand poke
            if (HandPoke_L_toggle != null && HandPoke_L != null) {HandPoke_Left = new Interactor(HandPoke_L_toggle, HandPoke_L); HandPoke_Left.set();}
            // touch grab
            if (TouchGrab_L_toggle != null && TouchGrab_L != null) {TouchGrab_Left = new Interactor(TouchGrab_L_toggle, TouchGrab_L); TouchGrab_Left.set();}
            // hand ray
            if (HandRay_L_toggle != null && HandRay_L != null) {HandRay_Left = new Interactor(HandRay_L_toggle, HandRay_L); HandRay_Left.set();}
            // hand grab
            if (HandGrab_L_toggle != null && HandGrab_L != null) {HandGrab_Left = new Interactor(HandGrab_L_toggle, HandGrab_L); HandGrab_Left.set();}
            // hand grab use
            if (HandGrabUse_L_toggle != null && HandGrabUse_L != null) {HandGrabUse_Left = new Interactor(HandGrabUse_L_toggle, HandGrabUse_L); HandGrabUse_Left.set();}
            // hand ray
            if (DistanceHandGrab_L_toggle != null && DistanceHandGrab_L != null) {DistanceHandGrab_Left = new Interactor(DistanceHandGrab_L_toggle, DistanceHandGrab_L); DistanceHandGrab_Left.set();}
            // hand ray
            if (HandRay_L_toggle != null && HandRay_L != null) {HandRay_Left = new Interactor(HandRay_L_toggle, HandRay_L); HandRay_Left.set();}
        }
        /*-------------------------------------------------------------------------------------------------------------------------*/        
        /// <summary>
        /// editor inspector setup
        /// </summary>
        // hand poke
        [BoxGroup("Left Hand Interactor")][ToggleLeft] [SerializeField] protected bool HandPoke_L_toggle;
        [BoxGroup("Left Hand Interactor")][EnableIf("HandPoke_L_toggle")] [SerializeField] protected GameObject HandPoke_L;
            // touch grab
            [BoxGroup("Left Hand Interactor")][EnableIf("HandPoke_L_toggle")][Indent(2)][ToggleLeft] [SerializeField] protected bool TouchGrab_L_toggle;
            [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(2)] [SerializeField] protected GameObject TouchGrab_L;
                //hand ray
                [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandRay_L_toggle;
                [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandRay_L_toggle")][Indent(4)] [SerializeField] protected GameObject HandRay_L;
                //hand grab
                [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandGrab_L_toggle;
                [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(4)] [SerializeField] protected GameObject HandGrab_L;
                    //hand grab use
                    [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool HandGrabUse_L_toggle;
                    [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle && HandGrabUse_L_toggle")][Indent(6)] [SerializeField] protected GameObject HandGrabUse_L;
                    //distance grab use
                    [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool DistanceHandGrab_L_toggle;
                    [BoxGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle && DistanceHandGrab_L_toggle")][Indent(6)] [SerializeField] protected GameObject DistanceHandGrab_L;
        /*=========================================================================================================================*/
        

        /*=========================================================================================================================*/
        /// <summary>
        /// Right Hand Interactor x 7
        /// </summary>
        public Interactor HandPoke_Right;
        public Interactor TouchGrab_Right;
        public Interactor HandRay_Right;
        public Interactor HandGrab_Right;
        public Interactor HandGrabUse_Right;
        public Interactor DistanceHandGrab_Right;

        private void initiateRight()
        {
            // hand poke
            if (HandPoke_R_toggle != null && HandPoke_R != null) {HandPoke_Right = new Interactor(HandPoke_R_toggle, HandPoke_R);}
            // touch grab
            if (TouchGrab_R_toggle != null && TouchGrab_R != null) {TouchGrab_Right = new Interactor(TouchGrab_R_toggle, TouchGrab_R);}
            // hand ray
            if (HandRay_R_toggle != null && HandRay_R != null) {HandRay_Right = new Interactor(HandRay_R_toggle, HandRay_R);}
            // hand grab
            if (HandGrab_R_toggle != null && HandGrab_R != null) {HandGrab_Right = new Interactor(HandGrab_R_toggle, HandGrab_R);}
            // hand grab use
            if (HandGrabUse_R_toggle != null && HandGrabUse_R != null) {HandGrabUse_Right = new Interactor(HandGrabUse_R_toggle, HandGrabUse_R);}
            // hand ray
            if (DistanceHandGrab_R_toggle != null && DistanceHandGrab_R != null) {DistanceHandGrab_Right = new Interactor(DistanceHandGrab_R_toggle, DistanceHandGrab_R);}
            // hand ray
            if (HandRay_R_toggle != null && HandRay_R != null) {HandRay_Right = new Interactor(HandRay_R_toggle, HandRay_R);}
        }
        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// editor inspector setup
        /// </summary>
        // hand poke
        [BoxGroup("Right Hand Interactor")][ToggleLeft] [SerializeField] protected bool HandPoke_R_toggle;
        [BoxGroup("Right Hand Interactor")][EnableIf("HandPoke_R_toggle")] [SerializeField] protected GameObject HandPoke_R;
            // touch grab
            [BoxGroup("Right Hand Interactor")][EnableIf("HandPoke_R_toggle")][Indent(2)][ToggleLeft] [SerializeField] protected bool TouchGrab_R_toggle;
            [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(2)] [SerializeField] protected GameObject TouchGrab_R;
                //hand ray
                [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandRay_R_toggle;
                [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandRay_R_toggle")][Indent(4)] [SerializeField] protected GameObject HandRay_R;
                //hand grab
                [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandGrab_R_toggle;
                [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(4)] [SerializeField] protected GameObject HandGrab_R;
                    //hand grab use
                    [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool HandGrabUse_R_toggle;
                    [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle && HandGrabUse_R_toggle")][Indent(6)] [SerializeField] protected GameObject HandGrabUse_R;
                    //distance grab use
                    [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool DistanceHandGrab_R_toggle;
                    [BoxGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle && DistanceHandGrab_R_toggle")][Indent(6)] [SerializeField] protected GameObject DistanceHandGrab_R;
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        /// <summary>
        /// locomotion x 2
        /// </summary>
        public Interactor Locomotion_Left;
        public Interactor Locomotion_Right;

        private void initiateLocomotion()
        {
            // Locomotion Left
            if (Locomotion_L_toggle && Locomotion_L != null) {Locomotion_Left = new Interactor(Locomotion_L_toggle, Locomotion_L);}
            // Locomotion Right
            if (Locomotion_R_toggle && Locomotion_R != null) {Locomotion_Right = new Interactor(Locomotion_R_toggle, Locomotion_R);}
        }
        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// editor inspector setup
        /// </summary>
        [BoxGroup("Locomotion")][ToggleLeft] [SerializeField] protected bool Locomotion_L_toggle;
        [BoxGroup("Locomotion")][EnableIf("Locomotion_L_toggle")] [SerializeField] protected GameObject Locomotion_L;
        [BoxGroup("Locomotion")][ToggleLeft] [SerializeField] protected bool Locomotion_R_toggle;
        [BoxGroup("Locomotion")][EnableIf("Locomotion_R_toggle")] [SerializeField] protected GameObject Locomotion_R;
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        /// <summary>
        /// chest gaze
        /// </summary>
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        /// <summary>
        /// head gaze
        /// </summary>
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        /// <summary>
        /// eye gaze
        /// </summary>
        /*=========================================================================================================================*/


        // Start is called before the first frame update
        void Start()
        {
            initiateInteractors();

        }



        /*=========================================================================================================================*/
        /// <summary>
        /// interactor class functions
        /// </summary>
        public class Interactor
        {
            private bool activation;
            private GameObject interactor;

            public Interactor(bool b, GameObject i)
            {
                activation = b;
                interactor = i;
            }
            public void set()
            {
                if (activation){this.enable();}
                else{this.disable();}
            }
            public void enable()
            {
                activation = true;
                interactor.SetActive(true);
            }
            public void disable()
            {
                activation = false;
                interactor.SetActive(false);
            }
        }

        private void initiateInteractors()
        {
            initiateLeft();
            initiateRight();
            initiateLocomotion();
        }
       


    }
}