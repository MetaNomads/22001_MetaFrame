using UnityEngine;
using MetaFrame.Interaction;

namespace MetaFrame.Testing
{
    /// <summary>
    /// Test script to visualize gaze hit points by moving a GameObject to the collision point
    /// Useful for debugging and verifying gaze raycast functionality
    /// </summary>
    public class GazeHitPointVisualizer : MonoBehaviour
    {
        [Header("Gaze Source")]
        [SerializeField] private GazePose _gazePose;
        
        [Header("Gaze Type to Visualize")]
        [SerializeField] private GazeType _gazeType = GazeType.CenterGaze;
        
        [Header("Visualization Settings")]
        [SerializeField] private GameObject _visualizerObject;
        [SerializeField] private bool _hideWhenNoHit = true;
        [SerializeField] private float _smoothing = 0.1f;
        
        private GazePose.GazePoseData _selectedGazePose;

        public enum GazeType
        {
            CenterGaze,
            HeadGaze,
            ChestGaze
        }

        void Start()
        {
            if (_visualizerObject == null)
            {
                Debug.LogWarning("[GazeHitPointVisualizer] No visualizer object assigned!");
                enabled = false;
                return;
            }

            if (_gazePose == null)
            {
                Debug.LogWarning("[GazeHitPointVisualizer] No GazePose assigned!");
                enabled = false;
                return;
            }

            // Select the appropriate gaze pose
            _selectedGazePose = _gazeType switch
            {
                GazeType.CenterGaze => _gazePose.CenterGaze,
                GazeType.HeadGaze => _gazePose.HeadGaze,
                GazeType.ChestGaze => _gazePose.ChestGaze,
                _ => null
            };

            if (_selectedGazePose == null)
            {
                Debug.LogWarning($"[GazeHitPointVisualizer] Failed to get {_gazeType}!");
                enabled = false;
            }
        }

        void Update()
        {
            if (_selectedGazePose == null || _visualizerObject == null) return;

            // Get the gaze hit point
            Vector3? hitPoint = _selectedGazePose.GetGazePoint();

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
            if (!Application.isPlaying || _selectedGazePose == null) return;

            // Draw debug ray from gaze origin to hit point
            var transform = _selectedGazePose.GetTransform();
            if (transform == null) return;

            Vector3? hitPoint = _selectedGazePose.GetGazePoint();
            
            if (hitPoint.HasValue)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, hitPoint.Value);
                Gizmos.DrawWireSphere(hitPoint.Value, 0.05f);
            }
            else
            {
                // Draw forward ray when no hit
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, transform.forward * 10f);
            }
        }
    }
}