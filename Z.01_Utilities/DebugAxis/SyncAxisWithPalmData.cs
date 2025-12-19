using UnityEngine;
using MetaFrame.Data;

namespace MetaFrame.Utilities
{
    public class SyncAxisWithPalmData : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;
        [SerializeField] private bool _leftPalm;

        void Update()
        {
            if (_dataManager?.Hand?.Data == null)
                return;

            Transform palmTransform = _leftPalm 
                ? _dataManager.Hand.Data.LeftPalm 
                : _dataManager.Hand.Data.RightPalm;

            if (palmTransform == null)
                return;

            this.transform.position = palmTransform.position;
            this.transform.rotation = palmTransform.rotation;
        }
    }
}
