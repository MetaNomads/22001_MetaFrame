using MetaFrame.Data;
using MetaFrame.Interaction.GazeInteraction;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MetaFrame.Interaction
{
    public class GazePose : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;
        // Simple eye transforms
        [SerializeField] private Transform _leftEye;
        [SerializeField] private Transform _rightEye;
        
        // Gaze pose data objects
        [SerializeField] private Transform _centerGazeTransform;
        [SerializeField] private GazeInteractor _centerGazeInteractor;
        
        [SerializeField] private Transform _headGazeTransform;
        [SerializeField] private GazeInteractor _headGazeInteractor;
        
        [SerializeField] private Transform _chestGazeTransform;
        [SerializeField] private GazeInteractor _chestGazeInteractor;

        
        // Public accessors
        public Transform LeftEye => _leftEye;
        public Transform RightEye => _rightEye;
        public GazePoseData CenterGaze { get; private set; }
        public GazePoseData HeadGaze { get; private set; }
        public GazePoseData ChestGaze { get; private set; }

        void Awake()
        {
            // Initialize GazePoseData objects
            CenterGaze = new GazePoseData(_centerGazeTransform, _centerGazeInteractor, UpdateCenterGaze);
            HeadGaze = new GazePoseData(_headGazeTransform, _headGazeInteractor, UpdateHeadGaze);
            ChestGaze = new GazePoseData(_chestGazeTransform, _chestGazeInteractor, UpdateChestGaze);
        }

        void LateUpdate()
        {
            CenterGaze?.UpdatePose();
            HeadGaze?.UpdatePose();
            ChestGaze?.UpdatePose();
        }

         /*=========================================================================================================================*/
        /// <summary>
        /// Nested GazePoseData Class - Encapsulates single gaze pose with transform and raycast
        /// </summary>

        public class GazePoseData
        {
            private readonly Transform _gazeTransform;
            private readonly GazeInteractor _gazeInteractor;
            private readonly System.Action _updateAction;

            public GazePoseData(Transform gazeTransform, GazeInteractor gazeInteractor, System.Action updateAction)
            {
                _gazeTransform = gazeTransform;
                _gazeInteractor = gazeInteractor;
                _updateAction = updateAction;
            }

            /// <summary>
            /// Get the transform for this gaze pose
            /// </summary>
            public Transform GetTransform() => _gazeTransform;

            /// <summary>
            /// Get the gaze collision point via raycast (null if no collision or interactor unavailable)
            /// </summary>
            public Vector3? GetGazePoint()
            {
                if (_gazeInteractor == null) return null;

                try
                {
                    return _gazeInteractor.GetCollisionPoint();
                }
                catch
                {
                    return null;
                }
            }

            /// <summary>
            /// Update this gaze pose (calls the delegate update function)
            /// </summary>
            public void UpdatePose()
            {
                _updateAction?.Invoke();
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Update Functions - Passed as delegates to GazePoseData
        /// </summary>

        private void UpdateCenterGaze()
        {
            if (_centerGazeTransform == null || _leftEye == null || _rightEye == null) return;

            try
            {
                var leftPose = _leftEye.GetWorldPose();
                var rightPose = _rightEye.GetWorldPose();
                _centerGazeTransform.position = (leftPose.position + rightPose.position) / 2f;
                _centerGazeTransform.rotation = Quaternion.Slerp(_leftEye.localRotation, _rightEye.localRotation, 0.5f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update center gaze: {e.Message}");
            }
        }

        private void UpdateHeadGaze()
        {
            if (_headGazeTransform == null || _dataManager?.Body?.Data?.Head == null) return;

            try
            {
                var headTransform = _dataManager.Body.Data.Head;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _headGazeTransform.position = headTransform.position;
                _headGazeTransform.rotation = headTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update head gaze: {e.Message}");
            }
        }

        private void UpdateChestGaze()
        {
            if (_chestGazeTransform == null || _dataManager?.Body?.Data?.Chest == null) return;

            try
            {
                var chestTransform = _dataManager.Body.Data.Chest;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _chestGazeTransform.position = chestTransform.position;
                _chestGazeTransform.rotation = chestTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazePose] Failed to update chest gaze: {e.Message}");
            }
        }
    }
}
