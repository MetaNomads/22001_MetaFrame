using MetaFrame.Data;
using Unity.Mathematics;
using Unity.XR.CoreUtils;
using UnityEngine;
using static OVRPlugin;

namespace MetaFrame.Data
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
        [SerializeField] private GameObject _centerEyeGaze;
        
        // public Transform? GetGazeTransform(GazeData eye)
        // {
        //     switch (eye)
        //     {
        //         case GazeData.Left:
        //             return _leftEyeGaze.transform;
        //         case GazeData.Right:
        //             return _rightEyeGaze.transform;
        //         case GazeData.Combined:
        //             return _centerEyeGaze.transform;
        //         case GazeData.Head:
        //             return _dataManager?.Body?.Data?.Head.transform;
        //         case GazeData.Chest:
        //             return _dataManager?.Body?.Data?.Chest.transform;
        //         default:
        //             return null;
        //     }
        // }
        


        //public Vector3? GetEyePosition(GazeData eye)
        //{
        //    switch (eye)
        //    {
        //        case GazeData.Left:
        //            return _leftEyeGaze.transform.GetWorldPose().position;
        //        case GazeData.Right:
        //            return _rightEyeGaze.transform.GetWorldPose().position;
        //        case GazeData.Combined:
        //            return GetCombinedEyePosition();
        //        case GazeData.Head:
        //            return GetHeadPosition();
        //        case GazeData.Chest:
        //            return GetChestPosition();
        //        default:
        //            return null;
        //    }
        //}

        //public Quaternion? GetGazeRotation(GazeData eye)
        //{
        //    switch (eye)
        //    {
        //        case GazeData.Left:
        //            return _leftEyeGaze.transform.localRotation;
        //        case GazeData.Right:
        //            return _rightEyeGaze.transform.localRotation;
        //        case GazeData.Combined:
        //            // Average the gaze directions of both eyes for combined gaze
        //            return GetCombinedGazeRotation();
        //        case GazeData.Head:
        //            return GetHeadRotation();
        //        case GazeData.Chest:
        //            return GetChestRotation();
        //        default:
        //            return null;
        //    }
        //}

        //public Vector3? GetGazeForward(GazeData eye)
        //{
        //    switch (eye)
        //    {
        //        case GazeData.Left:
        //            return _leftEyeGaze.transform.forward;
        //        case GazeData.Right:
        //            return _rightEyeGaze.transform.forward;
        //        case GazeData.Combined:
        //            return GetCombinedGazeForward();
        //        case GazeData.Head:
        //            return GetHeadForward();
        //        case GazeData.Chest:
        //            return GetChestForward();
        //        default:
        //            return null;
        //    }
        //}

        //public Vector3? GetCombinedEyePosition()
        //{
        //    if (_leftEyeGaze != null && _rightEyeGaze != null)
        //    {
        //        // Get the midpoint between the two eyes
        //        Vector3 conbinedEyeOrigin = (_leftEyeGaze.transform.GetWorldPose().position + _rightEyeGaze.transform.GetWorldPose().position) * 0.5f;
        //        return conbinedEyeOrigin;
        //    }
        //    else return null;
        //}
        //public Vector3? GetCombinedGazeForward()
        //{
        //    if (_leftEyeGaze != null && _rightEyeGaze != null)
        //    {
        //        Vector3? combinedEyeOrigin = GetCombinedEyePosition();

        //        // Check if we got a valid eye position
        //        if (!combinedEyeOrigin.HasValue)
        //        {
        //            return null;
        //        }

        //        // Reconstruct gameobject from OVREye
        //        Pose leftEye = new Pose(
        //            _leftEyeGaze.transform.localPosition,
        //            _leftEyeGaze.transform.localRotation
        //        );
        //        Pose rightEye = new Pose(
        //            _rightEyeGaze.transform.localPosition,
        //            _rightEyeGaze.transform.localRotation
        //        );

        //        // Return gazeDirection
        //        Vector3 combinedGazeForward = (leftEye.forward + rightEye.forward) / 2;
        //        Debug.DrawRay(combinedEyeOrigin.Value, combinedGazeForward * 50, Color.orange);
        //        return combinedGazeForward.normalized;
        //    }

        //    return null;
        //}
        //public Quaternion? GetCombinedGazeRotation()
        //{
        //    if (_leftEyeGaze != null && _rightEyeGaze != null)
        //    {
        //        Quaternion CombinedGazeRoatation = Quaternion.Slerp(_leftEyeGaze.transform.localRotation, _rightEyeGaze.transform.localRotation, 0.5f);
        //        return CombinedGazeRoatation;
        //    }
        //    else return null;
        //}

        //public Vector3? GetHeadPosition() => GetPositionData(_dataManager?.Body?.Data?.Head);
        //{
        //    return _dataManager?.Body?.Data?.Head?.position;
        //}
        //public Quaternion? GetHeadRotation()
        //{
        //    if (_dataManager?.Body?.Data?.Head != null)
        //        return _dataManager.Body.Data.Head.rotation;
        //    return null;
        //}
        //public Vector3? GetHeadForward()
        //{
        //    if (_dataManager?.Body?.Data?.Head != null)
        //        return _dataManager.Body.Data.Head.forward;
        //    return null;
        //}
        //public Vector3? GetChestPosition()
        //{
        //    if (_dataManager?.Body?.Data?.Chest != null)
        //        return _dataManager.Body.Data.Chest.position;
        //    return null;
        //}
        //public Quaternion? GetChestRotation()
        //{
        //    if (_dataManager?.Body?.Data?.Chest != null)
        //        return _dataManager.Body.Data.Chest.rotation;
        //    return null;
        //}
        //public Vector3? GetChestForward()
        //{
        //    if (_dataManager?.Body?.Data?.Chest != null)
        //        return _dataManager.Body.Data.Chest.forward;
        //    return null;
        //}
    }
}