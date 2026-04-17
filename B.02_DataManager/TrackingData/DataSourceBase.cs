using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
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

        // FIX: pre-lowercased name cached at Initialize() so CollectAllData()
        // never calls ToLower() (and allocates a new string) every recording tick.
        string SourceNameLower { get; }

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

        // FIX: cached in Initialize() — no allocation on hot recording path.
        private string _sourceNameLower;
        public string SourceNameLower => _sourceNameLower;

        public TDataStructure Data { get; protected set; }

        public virtual void Initialize(DataManager manager)
        {
            dataManager       = manager;
            _sourceNameLower  = SourceName.ToLower(); // cache once, never allocate again
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

        // ── Data Utilities ────────────────────────────────────────────────────────

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
        /// Utility for extracting position data
        /// </summary>
        protected object GetPositionData(Transform transform)
        {
            if (transform == null) return null;
            return new float[] { transform.position.x, transform.position.y, transform.position.z };
        }

        protected object GetPositionData(Vector3 position)
        {
            return new float[] { position.x, position.y, position.z };
        }

        /// <summary>
        /// Utility for extracting rotation data
        /// </summary>
        protected object GetRotationData(Transform transform)
        {
            if (transform == null) return null;
            return new float[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w };
        }

        // ── Precision ─────────────────────────────────────────────────────────────

        protected void ApplyPrecisionToData(Dictionary<string, object> data, int precision)
        {
            var keys = new List<string>(data.Keys);
            foreach (var key in keys)
                data[key] = ApplyPrecisionToValue(data[key], precision);
        }

        protected object ApplyPrecisionToValue(object value, int precision)
        {
            if (value == null) return null;

            switch (value)
            {
                case float f:
                    return (float)Math.Round(f, precision, MidpointRounding.AwayFromZero);

                case double d:
                    return Math.Round(d, precision, MidpointRounding.AwayFromZero);

                case float[] floatArray:
                    for (int i = 0; i < floatArray.Length; i++)
                        floatArray[i] = (float)Math.Round(floatArray[i], precision, MidpointRounding.AwayFromZero);
                    return floatArray;

                case double[] doubleArray:
                    for (int i = 0; i < doubleArray.Length; i++)
                        doubleArray[i] = Math.Round(doubleArray[i], precision, MidpointRounding.AwayFromZero);
                    return doubleArray;

                case Dictionary<string, object> dict:
                    ApplyPrecisionToData(dict, precision);
                    return dict;

                default:
                    // FIX: removed the reflection fallback (ApplyPrecisionToObject) that
                    // was called for anonymous objects. Anonymous objects no longer appear
                    // in the recording pipeline — FACS and Hand data sources now use
                    // float[] directly. If a new data source mistakenly produces an
                    // anonymous object it will pass through unrounded, which is preferable
                    // to paying reflection costs on every recording tick.
                    return value;
            }
        }

        // ── Angle Utilities ───────────────────────────────────────────────────────

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
    }
}
