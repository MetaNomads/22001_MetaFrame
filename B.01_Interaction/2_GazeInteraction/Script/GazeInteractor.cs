#region Includes
using UnityEngine;
using UnityEngine.Events;
using MetaFrame.Tags;
#endregion

namespace MetaFrame.Interaction.GazeInteraction
{
    public class GazeInteractor : MonoBehaviour
    {
        #region Variables

        [Header("Configuration")]
        [SerializeField] private GameObject reticle;
        private LineRenderer lineRenderer;
        [SerializeField] private float _rayShiftDown = 0.025f;
        [SerializeField] private float _maxDetectionDistance;
        [SerializeField] private float _minDetectionDistance;
        [SerializeField] private float _timeToActivate = 1.0f;

        [Tooltip("Raycast will only hit selected layer")]
        [SerializeField] private LayerMask _layerMask;
        [Tooltip("Raycast will only hit selected tags")]
        [SerializeField] private Tag _tagMask;

        public Material defaultTargetedMaterial;
        [Tooltip("The highlight material to use when pulling")]
        public Material defaultSelectedMaterial;

        [Header("Events")]  
        public UnityEvent OnGazeActivated; //the moment when gaze is finished
        public UnityEvent<bool> OnGazeToggle; //On when raycast hits the object, Off when raycast leaves the object
        public UnityEvent OnGazeEnter; //the moment when gaze is finished
        public UnityEvent OnGazeExit; //the moment when gaze is finished

        private Ray _ray;
        private RaycastHit _hit;
        private Vector3 GazePoint;

        private GazeReticle _reticle;
        private GazeInteractable _interactable;

        private float _enterStartTime;

        #endregion

        private void Awake()
        {
            
        }


        private void Start()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(reticle == null) { throw new System.Exception("Missing GazeReticle"); }
            #endif

            _reticle = Instantiate(reticle.GetComponent<GazeReticle>());
            _reticle.SetInteractor(this);

            lineRenderer = gameObject.GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
        }
        private void Update()
        {
            _ray = new Ray(transform.position - transform.up * _rayShiftDown, transform.forward);
            LineRender(_ray);

            if (Physics.Raycast(_ray, out _hit, _maxDetectionDistance, _layerMask))
            {
                //raycast will stop when hitting objects outside tagMask
                if (!_hit.collider.transform.HasTag(_tagMask))
                {
                    return;
                }

                var distance = Vector3.Distance(transform.position, _hit.transform.position);
                if (distance < _minDetectionDistance)
                {
                    _reticle.Enable(false);
                    Reset();
                    return;
                }

                _reticle.SetTarget(_hit);
                _reticle.Enable(true);

                var interactable = _hit.collider.transform.GetComponent<GazeInteractable>();
                if(interactable == null)
                {
                    Reset();
                    return;
                }

                if (interactable != _interactable)
                {
                    Reset();

                    _enterStartTime = Time.time;

                    _interactable = interactable;
                    _interactable.SetGazeInteractor(this);
                    _interactable.GazeEnter(this, _hit.point);
                    OnGazeToggle?.Invoke(true);
                    OnGazeEnter?.Invoke();
                }

                // _interactable.GazeStay(this, _hit.point);
                GazePoint = _hit.point;

                if (_interactable.IsActivable && !_interactable.IsActivated)
                {
                    if (!(_interactable.getTimeToActivate() == 0f)) {_timeToActivate = _interactable.getTimeToActivate(); }

                    var timeToActivate = (_enterStartTime + _timeToActivate) - Time.time;
                    var progress = 1 - (timeToActivate / _timeToActivate);
                    progress = Mathf.Clamp(progress, 0, 1);
                    _reticle.SetProgress(progress);

                    if (progress == 1)
                    {
                        _reticle.Enable(false);
                        _interactable.Activate();
                        OnGazeActivated?.Invoke();
                    }
                }

                return;
            }

            _reticle.Enable(false);
            Reset();
        }

        private void Reset()
        {
            _reticle.SetProgress(0);

            if(_interactable == null) { return; }
            _interactable.GazeExit(this);
            _interactable.SetGazeInteractor(null);
            _interactable = null;
            OnGazeToggle?.Invoke(false);
            OnGazeExit.Invoke();
        }

        private void LineRender(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, _maxDetectionDistance) && hit.transform.root != transform.root)
            {
                lineRenderer.SetPositions(new Vector3[] { ray.origin + ray.direction * _minDetectionDistance - transform.up * _rayShiftDown, hit.point});
            }
            else
            {
                lineRenderer.SetPositions(new Vector3[] { ray.origin + ray.direction * _minDetectionDistance - transform.up * _rayShiftDown, ray.origin + ray.direction * _maxDetectionDistance});
            }
        }

        public Vector3 getGazePoint()
        {
          return GazePoint;
        }

        public void InteractionEnabler(bool enable)
        {
            gameObject.SetActive(enable);
            if (_reticle != null) {_reticle.Enable(enable);}
            if (_reticle != null && enable == false) {Reset();}
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * _maxDetectionDistance);
        }
#endif
    }
}
