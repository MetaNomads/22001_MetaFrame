#region Includes
using System.Collections;

using UnityEngine;
using UnityEngine.Events;
#endregion

namespace MetaFrame.Interaction.GazeInteraction
{
    public class GazeInteractable : MonoBehaviour
    {
        #region Variables

        private const string WAIT_TO_EXIT_COROUTINE = "WaitToExit_Coroutine";
        private GazeInteractor _GazeInteractor;
        
        [Tooltip("0 to use global default time")]
        [SerializeField] private float _timeToActivate = 0.0f;
        [SerializeField] private bool _isActivable = true;
        [SerializeField] private float _exitDelay;      

        [Header("Events")]  
        public UnityEvent OnGazeActivated; //the moment when gaze is finished
        public UnityEvent<bool> OnGazeToggle; //On when raycast hits the object, Off when raycast leaves the object

        public bool IsEnabled
        {
            get { return _collider.enabled; }
            set { _collider.enabled = value; }
        }

        public bool IsActivable
        {
            get { return _isActivable; }
        }

        public bool IsActivated { get; private set; }

        private Collider _collider;

        #endregion

        private void Awake()
        {
            _collider = gameObject.GetComponent<Collider>();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(_collider == null) { throw new System.Exception("Missing Collider"); }
            #endif
        }

        private void Start()
        {
            enabled = false;
        }


        private void update()
        {

        }

        // private void scale(GazeInteractor interactor) //scale trigger collider based on distance
        // {
        //     var distance = Vector3.Distance(viewer.transform.position, this.transform.position);
        //     var scale = distance * _scale;
        //     scale = Mathf.Clamp(scale, _scale, scale);
        //     _collider.transform.localScale = Vector3.one * scale;
        // }

        public void SetGazeInteractor(GazeInteractor a){_GazeInteractor = a;}
        public GazeInteractor GetGazeInteractor(){return _GazeInteractor;}

        public float getTimeToActivate()
        {
            return _timeToActivate;
        }

//Events
        public void Enable(bool enable)
        {
            gameObject.SetActive(enable);
        }

        public void Activate()
        {
            IsActivated = true;
            OnGazeActivated?.Invoke();
        }

        public void GazeEnter(GazeInteractor interactor, Vector3 point)
        {
            StopCoroutine(WAIT_TO_EXIT_COROUTINE);
            OnGazeToggle?.Invoke(true);
        }

        public void GazeExit(GazeInteractor interactor)
        {
            if(gameObject.activeInHierarchy)
            {
                StartCoroutine(WAIT_TO_EXIT_COROUTINE, interactor);
            }
            else
            {
                InvokeExit(interactor);
            }
        }

        private IEnumerator WaitToExit_Coroutine(GazeInteractor interactor)
        {
            yield return new WaitForSeconds(_exitDelay);

            InvokeExit(interactor);
        }

        private void InvokeExit(GazeInteractor interactor)
        {
            OnGazeToggle?.Invoke(false);

            IsActivated = false;
        }
    }
}
