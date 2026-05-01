using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

namespace MetaNomads.Interaction
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

        private void InitiateLeft()
        {
            // hand poke
            if (HandPoke_L_toggle && HandPoke_L != null) {HandPoke_Left = new Interactor(HandPoke_L_toggle, HandPoke_L); HandPoke_Left.Set();}
            // touch grab
            if (TouchGrab_L_toggle && TouchGrab_L != null) {TouchGrab_Left = new Interactor(TouchGrab_L_toggle, TouchGrab_L); TouchGrab_Left.Set();}
            // hand ray
            if (HandRay_L_toggle && HandRay_L != null) {HandRay_Left = new Interactor(HandRay_L_toggle, HandRay_L); HandRay_Left.Set();}
            // hand grab
            if (HandGrab_L_toggle && HandGrab_L != null) {HandGrab_Left = new Interactor(HandGrab_L_toggle, HandGrab_L); HandGrab_Left.Set();}
            // hand grab use
            // FIX (T2-1): was `!&&` — invalid C# syntax that should never have compiled.
            // Pattern matches every other line in this method: X_toggle && Y != null.
            if (HandGrabUse_L_toggle && HandGrabUse_L != null) {HandGrabUse_Left = new Interactor(HandGrabUse_L_toggle, HandGrabUse_L); HandGrabUse_Left.Set();}
            // hand ray
            if (DistanceHandGrab_L_toggle && DistanceHandGrab_L != null) {DistanceHandGrab_Left = new Interactor(DistanceHandGrab_L_toggle, DistanceHandGrab_L); DistanceHandGrab_Left.Set();}
            // hand ray
            if (HandRay_L_toggle && HandRay_L != null) {HandRay_Left = new Interactor(HandRay_L_toggle, HandRay_L); HandRay_Left.Set();}
        }
        /*-------------------------------------------------------------------------------------------------------------------------*/        
        /// <summary>
        /// editor inspector setup
        /// </summary>
        // hand poke
        [FoldoutGroup("Left Hand Interactor")][ToggleLeft] [SerializeField] protected bool HandPoke_L_toggle;
        [FoldoutGroup("Left Hand Interactor")][EnableIf("HandPoke_L_toggle")] [SerializeField] protected GameObject HandPoke_L;
            // touch grab
            [FoldoutGroup("Left Hand Interactor")][EnableIf("HandPoke_L_toggle")][Indent(2)][ToggleLeft] [SerializeField] protected bool TouchGrab_L_toggle;
            [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(2)] [SerializeField] protected GameObject TouchGrab_L;
                //hand ray
                [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandRay_L_toggle;
                [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandRay_L_toggle")][Indent(4)] [SerializeField] protected GameObject HandRay_L;
                //hand grab
                [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandGrab_L_toggle;
                [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(4)] [SerializeField] protected GameObject HandGrab_L;
                    //hand grab use
                    [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool HandGrabUse_L_toggle;
                    [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle && HandGrabUse_L_toggle")][Indent(6)] [SerializeField] protected GameObject HandGrabUse_L;
                    //distance grab use
                    [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool DistanceHandGrab_L_toggle;
                    [FoldoutGroup("Left Hand Interactor")][EnableIf("@HandPoke_L_toggle && TouchGrab_L_toggle && HandGrab_L_toggle && DistanceHandGrab_L_toggle")][Indent(6)] [SerializeField] protected GameObject DistanceHandGrab_L;
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

        private void InitiateRight()
        {
            // hand poke
            if (HandPoke_R_toggle && HandPoke_R != null) {HandPoke_Right = new Interactor(HandPoke_R_toggle, HandPoke_R);}
            // touch grab
            if (TouchGrab_R_toggle && TouchGrab_R != null) {TouchGrab_Right = new Interactor(TouchGrab_R_toggle, TouchGrab_R);}
            // hand ray
            if (HandRay_R_toggle && HandRay_R != null) {HandRay_Right = new Interactor(HandRay_R_toggle, HandRay_R);}
            // hand grab
            if (HandGrab_R_toggle && HandGrab_R != null) {HandGrab_Right = new Interactor(HandGrab_R_toggle, HandGrab_R);}
            // hand grab use
            if (HandGrabUse_R_toggle && HandGrabUse_R != null) {HandGrabUse_Right = new Interactor(HandGrabUse_R_toggle, HandGrabUse_R);}
            // hand ray
            if (DistanceHandGrab_R_toggle && DistanceHandGrab_R != null) {DistanceHandGrab_Right = new Interactor(DistanceHandGrab_R_toggle, DistanceHandGrab_R);}
            // hand ray
            if (HandRay_R_toggle && HandRay_R != null) {HandRay_Right = new Interactor(HandRay_R_toggle, HandRay_R);}
        }
        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// editor inspector setup
        /// </summary>
        // hand poke
        [FoldoutGroup("Right Hand Interactor")][ToggleLeft] [SerializeField] protected bool HandPoke_R_toggle;
        [FoldoutGroup("Right Hand Interactor")][EnableIf("HandPoke_R_toggle")] [SerializeField] protected GameObject HandPoke_R;
            // touch grab
            [FoldoutGroup("Right Hand Interactor")][EnableIf("HandPoke_R_toggle")][Indent(2)][ToggleLeft] [SerializeField] protected bool TouchGrab_R_toggle;
            [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(2)] [SerializeField] protected GameObject TouchGrab_R;
                //hand ray
                [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandRay_R_toggle;
                [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandRay_R_toggle")][Indent(4)] [SerializeField] protected GameObject HandRay_R;
                //hand grab
                [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle")][Indent(4)][ToggleLeft] [SerializeField] protected bool HandGrab_R_toggle;
                [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(4)] [SerializeField] protected GameObject HandGrab_R;
                    //hand grab use
                    [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool HandGrabUse_R_toggle;
                    [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle && HandGrabUse_R_toggle")][Indent(6)] [SerializeField] protected GameObject HandGrabUse_R;
                    //distance grab use
                    [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle")][Indent(6)][ToggleLeft] [SerializeField] protected bool DistanceHandGrab_R_toggle;
                    [FoldoutGroup("Right Hand Interactor")][EnableIf("@HandPoke_R_toggle && TouchGrab_R_toggle && HandGrab_R_toggle && DistanceHandGrab_R_toggle")][Indent(6)] [SerializeField] protected GameObject DistanceHandGrab_R;
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        /// <summary>
        /// locomotion x 2
        /// </summary>
        public Interactor Locomotion_Left;
        public Interactor Locomotion_Right;

        private void InitiateLocomotion()
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
        [FoldoutGroup("Locomotion")][ToggleLeft] [SerializeField] protected bool Locomotion_L_toggle;
        [FoldoutGroup("Locomotion")][EnableIf("Locomotion_L_toggle")] [SerializeField] protected GameObject Locomotion_L;
        [FoldoutGroup("Locomotion")][ToggleLeft] [SerializeField] protected bool Locomotion_R_toggle;
        [FoldoutGroup("Locomotion")][EnableIf("Locomotion_R_toggle")] [SerializeField] protected GameObject Locomotion_R;
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
            InitiateInteractors();

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
            public void Set()
            {
                if (activation){this.Enable();}
                else{this.Disable();}
            }
            public void Enable()
            {
                activation = true;
                interactor.SetActive(true);
            }
            public void Disable()
            {
                activation = false;
                interactor.SetActive(false);
            }
        }

        private void InitiateInteractors()
        {
            InitiateLeft();
            InitiateRight();
            InitiateLocomotion();
        }
       


    }
}