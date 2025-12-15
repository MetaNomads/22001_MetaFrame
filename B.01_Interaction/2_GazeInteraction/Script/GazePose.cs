using MetaFrame.Data;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MetaFrame.Interaction
{
    public class GazePose : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;
        [SerializeField] private Transform _leftEye;
        [SerializeField] private Transform _rightEye;
        [SerializeField] private Transform _centerEye;
        [SerializeField] private Transform _headGaze;
        [SerializeField] private Transform _chestGaze;

        public Transform LeftEye => _leftEye != null ? _leftEye : null;
        public Transform RightEye => _rightEye != null ? _rightEye : null;
        public Transform CenterEye => _centerEye != null ? _centerEye : null;
        public Transform HeadGaze => _headGaze != null ? _headGaze : null;
        public Transform ChestGaze => _chestGaze != null ? _chestGaze : null;

        /*=========================================================================================================================*/
        /// <summary>
        /// Gaze GameObject updater
        /// </summary>

        void LateUpdate()
        {
            UpdateCenterEye();
            UpdateHeadGaze();
            UpdateChestGaze();
        }

        private void UpdateCenterEye()
        {
            if (_centerEye == null || _leftEye == null || _rightEye == null) return;

            try
            {
                var leftPose = _leftEye.GetWorldPose();
                var rightPose = _rightEye.GetWorldPose();
                _centerEye.position = (leftPose.position + rightPose.position) / 2f;
                _centerEye.rotation = Quaternion.Slerp(_leftEye.localRotation, _rightEye.localRotation, 0.5f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazeUpdater] Failed to update center eye: {e.Message}");
            }
        }

        private void UpdateHeadGaze()
        {
            if (_headGaze == null || _dataManager?.Body?.Data?.Head == null) return;

            try
            {
                var headTransform = _dataManager.Body.Data.Head;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _headGaze.position = headTransform.position;
                _headGaze.rotation = headTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazeUpdater] Failed to update head gaze: {e.Message}");
            }
        }

        private void UpdateChestGaze()
        {
            if (_chestGaze == null || _dataManager?.Body?.Data?.Chest == null) return;

            try
            {
                var chestTransform = _dataManager.Body.Data.Chest;
                Quaternion correction = Quaternion.Euler(-90, 0, 90);
                _chestGaze.position = chestTransform.position;
                _chestGaze.rotation = chestTransform.rotation * correction;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GazeUpdater] Failed to update chest gaze: {e.Message}");
            }
        }

    }
}
