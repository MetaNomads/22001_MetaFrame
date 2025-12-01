using MetaNomads.Data;
using Unity.Mathematics;
using Unity.XR.CoreUtils;
using UnityEngine;
using static OVRPlugin;

namespace MetaNomads.Data
{   
    public class Gaze: MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;
        public enum GazeData
        {
            Left,
            Right,
            Combined,
            Chest,
            Head
        }

        [SerializeField] private OVREyeGaze _leftEyeGaze;

        [SerializeField] private OVREyeGaze _rightEyeGaze;

        [SerializeField] private DataSource_Body _body;
        [SerializeField] private OVRFaceExpressions _face;

        [SerializeField] private GameObject _cameraRig;

        public Vector3 GetEyePosition(GazeData eye)
        {
            switch (eye)
            {
                case GazeData.Left:
                    return _leftEyeGaze.transform.GetWorldPose().position;
                case GazeData.Right:
                    return _rightEyeGaze.transform.GetWorldPose().position;
                case GazeData.Combined:
                    return GetCombinedEyePosition();
                case GazeData.Head:
                    return GetHeadPosition();
                case GazeData.Chest:
                    return GetChestPosition();
                default:
                    return Vector3.zero;
            }
        }

        public Quaternion GetGazeRotation(GazeData eye)
        {
            switch (eye)
            {
                case GazeData.Left:
                    return _leftEyeGaze.transform.localRotation;
                case GazeData.Right:
                    return _rightEyeGaze.transform.localRotation;
                case GazeData.Combined:
                    // Average the gaze directions of both eyes for combined gaze
                    return GetCombinedGazeRotation();
                case GazeData.Head:
                    return GetHeadRotation();
                case GazeData.Chest:
                    return GetChestRotation();
                default:
                    return new Quaternion(0, 0, 0, 0);
            }
        }

        public Vector3 GetGazeForward(GazeData eye)
        {
            switch (eye)
            {
                case GazeData.Left:
                    return _leftEyeGaze.transform.forward;
                case GazeData.Right:
                    return _rightEyeGaze.transform.forward;
                case GazeData.Combined:
                    return GetCombinedGazeForward();
                case GazeData.Head:
                    return GetHeadForward();
                case GazeData.Chest:
                    return GetChestForward();
                default:
                    return Vector3.zero;
            }
        }

        public Vector3 GetCombinedEyePosition()
        {
            ///// Get the midpoint between the two eyes
            Vector3 conbinedEyeOrigin = (_leftEyeGaze.transform.GetWorldPose().position + _rightEyeGaze.transform.GetWorldPose().position) * 0.5f;
            return conbinedEyeOrigin;
        }
        public Vector3 GetCombinedGazeForward()
        {
            Vector3 combinedEyeOrigin = GetCombinedEyePosition();

            //reconstruct gameobject from OVREye
            Pose leftEye = new Pose(
            _leftEyeGaze.transform.localPosition,
            _leftEyeGaze.transform.localRotation
        );
            Pose rightEye = new Pose(
                _rightEyeGaze.transform.localPosition,
                _rightEyeGaze.transform.localRotation
            );

            //return gazeDirection
            Vector3 combinedGazeForward = Vector3.zero;
            combinedGazeForward = (leftEye.forward + rightEye.forward) / 2;
            Debug.DrawRay(combinedEyeOrigin, combinedGazeForward * 50, Color.orange);
            return combinedGazeForward.normalized;
        }
        public Quaternion GetCombinedGazeRotation()
        {
            Quaternion CombinedGazeRoatation = Quaternion.Slerp(_leftEyeGaze.transform.localRotation, _rightEyeGaze.transform.localRotation, 0.5f);
            return CombinedGazeRoatation;
        }

        public Vector3 GetHeadPosition()
        {
            if (_dataManager?.Body?.Data?.Head != null)
                return _dataManager.Body.Data.Head.position;
            return default;
        }
        public Quaternion GetHeadRotation()
        {
            if (_dataManager?.Body?.Data?.Head != null)
                return _dataManager.Body.Data.Head.rotation;
            return Quaternion.identity;
        }
        public Vector3 GetHeadForward()
        {
            if (_dataManager?.Body?.Data?.Head != null)
                return _dataManager.Body.Data.Head.forward;
            return default;
        }
        public Vector3 GetChestPosition()
        {
            if (_dataManager?.Body?.Data?.Chest != null)
                return _dataManager.Body.Data.Chest.position;
            return default;
        }
        public Quaternion GetChestRotation()
        {
            if (_dataManager?.Body?.Data?.Chest != null)
                return _dataManager.Body.Data.Chest.rotation;
            return Quaternion.identity;
        }
        public Vector3 GetChestForward()
        {
            if (_dataManager?.Body?.Data?.Chest != null)
                return _dataManager.Body.Data.Chest.forward;
            return default;
        }
    }
}