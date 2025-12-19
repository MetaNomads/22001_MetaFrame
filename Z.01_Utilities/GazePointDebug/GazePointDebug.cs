using UnityEngine;
using MetaFrame.Interaction.GazeInteraction;

namespace MetaFrame.Testing
{
    /// <summary>
    /// Test script to visualize gaze hit points by moving a GameObject to the collision point
    /// Useful for debugging and verifying gaze raycast functionality
    /// Now works directly with GazeInteractor for simplified dependency management
    /// </summary>
    public class GazeHitPointVisualizer : MonoBehaviour
    {
        [Header("Gaze Source")]
        [SerializeField] private GazeInteractor _gazeInteractor;

        [Header("Visualization Settings")]
        [SerializeField] private GameObject _visualizerObject;
        [SerializeField] private bool _hideWhenNoHit = true;
        [SerializeField] private float _smoothing = 0.1f;

        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawDebugGizmos = true;
        [SerializeField] private float _gizmoSphereRadius = 0.05f;

        void Start()
        {
            if (_visualizerObject == null)
            {
                Debug.LogWarning("[GazeHitPointVisualizer] No visualizer object assigned!");
                enabled = false;
                return;
            }

            if (_gazeInteractor == null)
            {
                Debug.LogWarning("[GazeHitPointVisualizer] No GazeInteractor assigned!");
                enabled = false;
                return;
            }
        }

        void Update()
        {
            if (_gazeInteractor == null || _visualizerObject == null) return;

            // Get the gaze hit point directly from the interactor
            Vector3? hitPoint = _gazeInteractor.GetCollisionPoint();

            if (hitPoint.HasValue)
            {
                // Show and move visualizer to hit point
                if (!_visualizerObject.activeSelf)
                {
                    _visualizerObject.SetActive(true);
                }

                // Apply smoothing for more stable visualization
                if (_smoothing > 0)
                {
                    _visualizerObject.transform.position = Vector3.Lerp(
                        _visualizerObject.transform.position,
                        hitPoint.Value,
                        Time.deltaTime / _smoothing
                    );
                }
                else
                {
                    _visualizerObject.transform.position = hitPoint.Value;
                }
            }
            else
            {
                // No hit - hide visualizer if configured
                if (_hideWhenNoHit && _visualizerObject.activeSelf)
                {
                    _visualizerObject.SetActive(false);
                }
            }
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying || _gazeInteractor == null || !_drawDebugGizmos) return;

            // Get the interactor's transform (raycast origin)
            Transform interactorTransform = _gazeInteractor.transform;

            // Get the collision point
            Vector3? hitPoint = _gazeInteractor.GetCollisionPoint();

            if (hitPoint.HasValue)
            {
                // Draw green line from raycast origin to hit point
                Gizmos.color = Color.green;
                Gizmos.DrawLine(interactorTransform.position, hitPoint.Value);
                Gizmos.DrawWireSphere(hitPoint.Value, _gizmoSphereRadius);
            }
            else
            {
                // Draw red ray when no hit (showing raycast direction)
                Gizmos.color = Color.red;
                Gizmos.DrawRay(interactorTransform.position, interactorTransform.forward * 10f);
            }
        }

        /// <summary>
        /// Get the currently gazed GameObject (if any)
        /// </summary>
        public GameObject GetGazedObject()
        {
            return _gazeInteractor?.GetGazeInteractable();
        }

        /// <summary>
        /// Check if the gaze is currently hitting anything
        /// </summary>
        public bool IsGazeHitting()
        {
            return _gazeInteractor != null && _gazeInteractor.IsColliding();
        }
    }
}