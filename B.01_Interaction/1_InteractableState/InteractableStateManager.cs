using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using Oculus.Interaction;
using Sirenix.OdinInspector;

namespace MetaNomads.Interaction
{
    /// <summary>
    /// copied from Oculus.Interaction.InteractableUnityEventWrapper
    /// Exposes Unity events that broadcast state changes from an <see cref="IInteractableView"/> (an Interactable).
    /// 
    /// disabled multiple interactor states
    /// added trigger events from MetaFrame
    /// </summary>

    public class InteractableStateManager: MonoBehaviour
    {
        /// <summary>
        /// The <see cref="IInteractableView"/> (Interactable) component to wrap.
        /// </summary>
        [Tooltip("The IInteractableView (Interactable) component to wrap.")]
        [SerializeField, Interface(typeof(IInteractableView))]
        private UnityEngine.Object _interactableView;
        private IInteractableView InteractableView;

        /*=========================================================================================================================*/
        /// <summary>
        /// events setup
        /// </summary>
        [BoxGroup("Universal States")][Tooltip("Raised when an Interactor hovers over the Interactable")][SerializeField] private UnityEvent _whenHover;
        [BoxGroup("Universal States")][Tooltip("Raised when the Interactable was being hovered but now it isn't")][SerializeField] private UnityEvent _whenUnhover;
        [BoxGroup("Universal States")][Tooltip("Raised when an Interactor selects the Interactable")][SerializeField] private UnityEvent _whenSelect;
        [BoxGroup("Universal States")][Tooltip("Raised when the Interactable was being selected but now it isn't")][SerializeField] private UnityEvent _whenUnselect;
        
        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// Trigger Confirm events
        /// </summary>
        // [BoxGroup("First Trigger")][SerializeField] private GameObject[] firstTriggerConfirmer = null;
        // [BoxGroup("First Trigger")][SerializeField] private UnityEvent _firstTrigger;
        // [BoxGroup("First Trigger")][SerializeField] private UnityEvent _firstUntrigger;
        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// multiple interactors
        /// </summary>

        // [Tooltip("Raised each time an Interactor hovers over the Interactable, even if the Interactable is already being hovered by a different Interactor.")]
        // [SerializeField]
        // private UnityEvent _whenInteractorViewAdded;

        // [Tooltip("Raised each time an Interactor stops hovering over the Interactable, even if the Interactable is still being hovered by a different Interactor.")]
        // [SerializeField]
        // private UnityEvent _whenInteractorViewRemoved;

        // [Tooltip("Raised each time an Interactor selects the Interactable, even if the Interactable is already being selected by a different Interactor.")]
        // [SerializeField]
        // private UnityEvent _whenSelectingInteractorViewAdded;

        // [Tooltip("Raised each time an Interactor stops selecting the Interactable, even if the Interactable is still being selected by a different Interactor.")]
        // [SerializeField]
        // private UnityEvent _whenSelectingInteractorViewRemoved;
        /*=========================================================================================================================*/

        #region Properties

        public UnityEvent WhenHover => _whenHover;
        public UnityEvent WhenUnhover => _whenUnhover;
        public UnityEvent WhenSelect => _whenSelect;
        public UnityEvent WhenUnselect => _whenUnselect;
        // public UnityEvent firstTrigger => _firstTrigger;
        // public UnityEvent secondTrigger => _secondTrigger;
        // public UnityEvent thirdTrigger => _thirdTrigger;
        
        // public UnityEvent WhenInteractorViewAdded => _whenInteractorViewAdded;
        // public UnityEvent WhenInteractorViewRemoved => _whenInteractorViewRemoved;
        // public UnityEvent WhenSelectingInteractorViewAdded => _whenSelectingInteractorViewAdded;
        // public UnityEvent WhenSelectingInteractorViewRemoved => _whenSelectingInteractorViewRemoved;

        #endregion
        /*=========================================================================================================================*/
        /// <summary>
        /// evoke state events
        /// </summary>

        protected bool _started = false;

        protected virtual void Awake()
        {
            InteractableView = _interactableView as IInteractableView;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            this.AssertField(InteractableView, nameof(InteractableView));
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started)
            {
                InteractableView.WhenStateChanged += HandleStateChanged;
                // InteractableView.WhenInteractorViewAdded += HandleInteractorViewAdded;
                // InteractableView.WhenInteractorViewRemoved += HandleInteractorViewRemoved;
                // InteractableView.WhenSelectingInteractorViewAdded += HandleSelectingInteractorViewAdded;
                // InteractableView.WhenSelectingInteractorViewRemoved += HandleSelectingInteractorViewRemoved;
            }
        }

        protected virtual void OnDisable()
        {
            if (_started)
            {
                InteractableView.WhenStateChanged -= HandleStateChanged;
                // InteractableView.WhenInteractorViewAdded -= HandleInteractorViewAdded;
                // InteractableView.WhenInteractorViewRemoved -= HandleInteractorViewRemoved;
                // InteractableView.WhenSelectingInteractorViewAdded -= HandleSelectingInteractorViewAdded;
                // InteractableView.WhenSelectingInteractorViewRemoved -= HandleSelectingInteractorViewRemoved;
            }
        }


        #region Inject
        public void InjectAllInteractableUnityEventWrapper(IInteractableView interactableView)
        {
            InjectInteractableView(interactableView);
        }

        public void InjectInteractableView(IInteractableView interactableView)
        {
            _interactableView = interactableView as UnityEngine.Object;
            InteractableView = interactableView;
        }
        #endregion

        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// evoke universal states
        /// </summary>
        private void HandleStateChanged(InteractableStateChangeArgs args)
        {
            switch (args.NewState)
            {
                case InteractableState.Normal:
                    if (args.PreviousState == InteractableState.Hover)
                    {
                        _whenUnhover.Invoke();
                    }

                    break;
                case InteractableState.Hover:
                    if (args.PreviousState == InteractableState.Normal)
                    {
                        _whenHover.Invoke();
                    }
                    else if (args.PreviousState == InteractableState.Select)
                    {
                        _whenUnselect.Invoke();
                    }

                    break;
                case InteractableState.Select:
                    if (args.PreviousState == InteractableState.Hover)
                    {
                        _whenSelect.Invoke();
                    }

                    break;
            }
        }

        /*-------------------------------------------------------------------------------------------------------------------------*/
        /// <summary>
        /// evoke universal states
        /// </summary>
        /// 







        // private void HandleInteractorViewAdded(IInteractorView interactorView)
        // {
        //     WhenInteractorViewAdded.Invoke();
        // }

        // private void HandleInteractorViewRemoved(IInteractorView interactorView)
        // {
        //     WhenInteractorViewRemoved.Invoke();
        // }

        // private void HandleSelectingInteractorViewAdded(IInteractorView interactorView)
        // {
        //     WhenSelectingInteractorViewAdded.Invoke();
        // }

        // private void HandleSelectingInteractorViewRemoved(IInteractorView interactorView)
        // {
        //     WhenSelectingInteractorViewRemoved.Invoke();
        // }
        /*=========================================================================================================================*/




    }

}
