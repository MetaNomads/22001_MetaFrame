using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System.Reflection;


namespace MetaFrame.Data
{
    /// <summary>
    /// Interface for all data sources to enable plugin architecture
    /// </summary>
    public interface IDataSource
    {
        string SourceName { get; }
        void Initialize(DataManager manager);
        Dictionary<string, object> CollectData();
    }

    /// <summary>
    /// Base class for data sources, providing common functionality and structure
    /// </summary>
    public abstract class DataSourceBase<TDataStructure, TRecordingConfig> : MonoBehaviour, IDataSource
        where TDataStructure : class
        where TRecordingConfig : class, new()
    {

        [FoldoutGroup("RecordConfig"), PropertyOrder(99)]
        [InlineProperty, HideLabel]
        [SerializeField]
        public TRecordingConfig RecordConfig = new TRecordingConfig();

        [SerializeField] public DataManager dataManager;

        public abstract string SourceName { get; }
        public TDataStructure Data { get; protected set; }

        public virtual void Initialize(DataManager manager)
        {
            dataManager = manager;
            manager.RegisterDataSource(this);
        }

        void Start()
        {
            Data = CreateData();
            OnDataInitialized();
        }
        
        /// <summary>
        /// Override for post-initialization setup
        /// </summary>
        protected virtual void OnDataInitialized() { }
        
        protected abstract TDataStructure CreateData();
        public abstract Dictionary<string, object> CollectData();



        /// <summary>
        /// Utility for extracting transform data
        /// </summary>
        protected object GetTransformData(Transform transform)
        {
            if (transform == null) return null;
            return new
            {
                Position = new float[] { transform.position.x, transform.position.y, transform.position.z },
                Rotation = new float[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w }
            };
        }

        /// <summary>
        // Utility for extracting position data
        /// <summary>
        protected object GetPositionData(Transform transform)
        {
            if (transform == null) return null;
            return new float[] { transform.position.x, transform.position.y, transform.position.z };
        }
        protected object GetPositionData(Vector3 position)
        {
            if (position == null) return null;
            return new float[] { position.x, position.y, position.z };
        }

        /// <summary>
        // Utility for extracting rotation data
        /// <summary>
        protected object GetRotationData(Transform transform)
        {
            if (transform == null) return null;
            return new float[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w };
        }
        /// <summary>
        // Utility for extracting object data
        /// <summary>
        protected Dictionary<string, object> GetObjectData(object obj)
        {
            if (obj == null) return null;
            var dict = new Dictionary<string, object>();
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                dict[prop.Name] = prop.GetValue(obj, null);
            }
            return dict;
        }
        /// <summary>
        // Utility for calculating turn, pitch, and tilt angles
        /// <summary>
        public float? CalculateTurn(Transform tartgetVector, Transform refVector, float direction)
        {
            float angle = Vector3.SignedAngle(
                tartgetVector.up,
                Vector3.ProjectOnPlane(refVector.up, tartgetVector.right),
                tartgetVector.right * direction);

            angle = math.remap(0f, 90f, 0f, 1f, angle);
            return angle < 0 ? null : angle;
        }

        public float? CalculatePitch(Transform tartgetVector, Transform refVector, float direction)
        {
            float angle = Vector3.SignedAngle(
                tartgetVector.up,
                Vector3.ProjectOnPlane(refVector.up, tartgetVector.forward),
                tartgetVector.forward * direction);

            angle = math.remap(0f, 90f, 0f, 1f, angle);
            return angle < 0 ? null : angle;
        }

        public float? CalculateTilt(Transform tartgetVector, Transform refVector, float direction)
        {
            float angle = Vector3.SignedAngle(
                tartgetVector.right,
                Vector3.ProjectOnPlane(refVector.right, tartgetVector.up),
                tartgetVector.up * direction);

            angle = math.remap(0f, 90f, 0f, 1f, angle);
            return angle < 0 ? null : angle;
        }
        /// <summary>
        /// Safe utility for extracting values with error handling
        /// </summary>
        // protected float? GetFloatValue(Func<float> valueGetter)
        // {
        //     try
        //     {
        //         return valueGetter?.Invoke();
        //     }
        //     catch
        //     {
        //         return null;
        //     }
        // }

        // protected T SafeGetValue<T>(Func<T> valueGetter, T defaultValue = null) where T : class
        // {
        //     try
        //     {
        //         return valueGetter?.Invoke() ?? defaultValue;
        //     }
        //     catch
        //     {
        //         return defaultValue;
        //     }
        // }


    }
}