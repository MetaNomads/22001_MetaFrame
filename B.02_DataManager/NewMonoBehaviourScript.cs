using UnityEngine;
using MetaFrame.Data;

namespace MetaFrame.Data
{
    public class PalmSync : MonoBehaviour
    {
        [Header("Target GameObjects")]
        [SerializeField] private Transform leftPalmObject;
        [SerializeField] private Transform rightPalmObject;
        [SerializeField] private DataManager _dataManager;

        void Update()
        {
            if (_dataManager.Hand == null || _dataManager.Hand.Data == null)
                return;

            // Left palm
            if (leftPalmObject != null)
            {
                leftPalmObject.position = _dataManager.Hand.Data.LeftPalm.position;
                leftPalmObject.rotation = _dataManager.Hand.Data.LeftPalm.rotation;
            }

            // Right palm
            if (rightPalmObject != null)
            {
                rightPalmObject.position = _dataManager.Hand.Data.RightPalm.position;
                rightPalmObject.rotation = _dataManager.Hand.Data.RightPalm.rotation;
            }
        }
    }
}
