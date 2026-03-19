using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Sirenix.OdinInspector;
using Newtonsoft.Json;

namespace MetaFrame.Data
{
    public class TrackingDataRecorder : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;

        [BoxGroup("Output Settings")]
        [FolderPath] public string _savePath;

        [BoxGroup("Output Settings")]
        [SerializeField]
        [Tooltip("Prefix for recording folder names (will be followed by timestamp)")]
        private string _folderPrefix = "Recording";

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(1, 20)]
        [Tooltip("Recording interval in milliseconds")]
        private int _recordingIntervalMilliseconds = 10;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(0, 8)]
        [Tooltip("Decimal precision for all float data")]
        private int _decimalPrecision = 4;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(1, 20)]
        [Tooltip("Minimum records to batch before writing (higher = better performance, higher latency)")]
        private int _minBatchSize = 10;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(20, 40)]
        [Tooltip("Maximum records before forcing write (prevents excessive memory usage)")]
        private int _maxBatchSize = 20;

        // Runtime state
        public bool startRecord { get; private set; }
        private bool _isPaused = false;
        private string startTime;
        public string sessionPath;
        private float _recordingInterval;

        // Optimized data structures
        private Dictionary<string, StreamWriter> _jsonWriters = new Dictionary<string, StreamWriter>();
        private Dictionary<string, List<Dictionary<string, object>>> _dataBatches = new Dictionary<string, List<Dictionary<string, object>>>();
        private Dictionary<string, float> _lastBatchTimes = new Dictionary<string, float>();
        
        // Pre-allocated structures for performance
        private readonly StringBuilder _stringBuilder = new StringBuilder(4096);
        private JsonSerializerSettings _jsonSettings;

        // Performance monitoring
        private int _totalFramesRecorded = 0;
        private int _totalFramesSkipped = 0;
        private float _nextRecordTime;

        /*=========================================================================================================================*/
        /// <summary>
        /// Unity Lifecycle
        /// </summary>

        void Awake()
        {
            _recordingInterval = _recordingIntervalMilliseconds / 1000f;
            
            // Configure JSON settings once
            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                FloatFormatHandling = FloatFormatHandling.String,
                FloatParseHandling = FloatParseHandling.Double
            };

            StartRecording();
        }

        void Update()
        {
            if (startRecord && Time.unscaledTime >= _nextRecordTime)
            {
                if (_isPaused)
                {
                    _totalFramesSkipped++;
                }
                else
                {
                    try
                    {
                        RecordData();
                        _totalFramesRecorded++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[DataRecorder] Frame recording failed: {e.Message}");
                    }
                }
                _nextRecordTime += _recordingInterval;
            }
        }

        void OnDestroy()
        {
            StopRecording();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && startRecord)
            {
                FlushAllBatches();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && startRecord)
            {
                FlushAllBatches();
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Recording Control
        /// </summary>

        public void StartRecording()
        {
            if (startRecord) return;

            try
            {
                CreateSessionDirectory();
                InitializeBatchStructures();
                LogDataSourcesOnce();
                startRecord = true;
                _isPaused = false;
                _nextRecordTime = Time.unscaledTime + _recordingInterval;
                
                Debug.Log($"[DataRecorder] Recording started at {_recordingInterval} milliseconds with {_minBatchSize}-{_maxBatchSize} record batching. Session: {sessionPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataRecorder] Failed to start recording: {e.Message}");
            }
        }

        public void StopRecording()
        {
            if (!startRecord) return;

            try
            {
                startRecord = false;
                _isPaused = false;
                FlushAllBatches();
                CloseAllWriters();
                Debug.Log($"[DataRecorder] Recording stopped. Total frames: {_totalFramesRecorded}, Skipped: {_totalFramesSkipped}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataRecorder] Error stopping recording: {e.Message}");
            }
        }

        [BoxGroup("Controls"), PropertyOrder(99)]
        [ShowInInspector, ReadOnly]
        [LabelText("@GetCurrentStatusLabel()")]
        [DisplayAsString(false)]
        private string CurrentStatus => GetCurrentStatus();

        [BoxGroup("Controls"), PropertyOrder(99)]
        [Button("Pause", ButtonSizes.Large), ShowIf("@startRecord && !_isPaused")]
        public void Pause()
        {
            if (!startRecord || _isPaused) return;
            _isPaused = true;
            Debug.Log("[DataRecorder] Recording paused.");
        }

        [BoxGroup("Controls"), PropertyOrder(99)]
        [Button("Resume", ButtonSizes.Large), ShowIf("@startRecord && _isPaused")]
        public void Resume()
        {
            if (!startRecord || !_isPaused) return;
            _isPaused = false;
            Debug.Log("[DataRecorder] Recording resumed.");
        }

        private string GetCurrentStatusLabel() => "Status";

        private string GetCurrentStatus()
        {
            if (!startRecord) return "Not Recording";
            return _isPaused ? "Paused" : "Recording";
        }

        private void CreateSessionDirectory()
        {
            startTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            sessionPath = Path.Combine(_savePath, $"{_folderPrefix}_{startTime}");
            Directory.CreateDirectory(sessionPath);

            Debug.Log($"[DataRecorder] Session directory created: {sessionPath}");
        }

        private void InitializeBatchStructures()
        {
            _dataBatches.Clear();
            _lastBatchTimes.Clear();
            _totalFramesRecorded = 0;
            _totalFramesSkipped = 0;
        }

        private void CloseAllWriters()
        {
            foreach (var writer in _jsonWriters.Values)
            {
                try
                {
                    writer?.Close();
                    writer?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataRecorder] Error closing writer: {e.Message}");
                }
            }
            _jsonWriters.Clear();
            _dataBatches.Clear();
            _lastBatchTimes.Clear();

            Debug.Log($"[DataRecorder] All writers closed. Total frames: {_totalFramesRecorded}");
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Data Collection with Integrated Precision Control
        /// </summary>

        private Dictionary<string, Dictionary<string, object>> CollectAllData()
        {
            var results = new Dictionary<string, Dictionary<string, object>>();
            string timestamp = GetRealWorldTimeString();

            foreach (var dataSource in _dataManager._dataSources)
            {
                try
                {
                    // Debug.Log("The source being tested is: " +  dataSource.SourceName);
                    var sourceData = dataSource.CollectData();
                    // Debug.Log("The source: " + dataSource.SourceName + " has " + sourceData.Count + " options");
                    if (sourceData.Count > 0)
                    {
                        // Create new dictionary with timestamp first
                        var orderedData = new Dictionary<string, object>();
                        orderedData["timestamp"] = timestamp;
                        
                        // Apply precision and add remaining data
                        ApplyPrecisionToData(sourceData);
                        foreach (var kvp in sourceData)
                        {
                            orderedData[kvp.Key] = kvp.Value;
                        }
                        
                        results[dataSource.SourceName.ToLower()] = orderedData;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataRecorder] Failed to collect {dataSource.SourceName} data: {e.Message}");
                    // Continue with other data sources
                }
            }

            return results;
        }

        private void ApplyPrecisionToData(Dictionary<string, object> data)
        {
            var keys = new List<string>(data.Keys);
            foreach (var key in keys)
            {
                data[key] = ApplyPrecisionToValue(data[key]);
            }
        }

        private object ApplyPrecisionToValue(object value)
        {
            if (value == null) return null;

            switch (value)
            {
                case float f:
                    return (float)Math.Round(f, _decimalPrecision, MidpointRounding.AwayFromZero);
                
                case double d:
                    return Math.Round(d, _decimalPrecision, MidpointRounding.AwayFromZero);
                
                case float[] floatArray:
                    for (int i = 0; i < floatArray.Length; i++)
                    {
                        floatArray[i] = (float)Math.Round(floatArray[i], _decimalPrecision, MidpointRounding.AwayFromZero);
                    }
                    return floatArray;
                
                case double[] doubleArray:
                    for (int i = 0; i < doubleArray.Length; i++)
                    {
                        doubleArray[i] = Math.Round(doubleArray[i], _decimalPrecision, MidpointRounding.AwayFromZero);
                    }
                    return doubleArray;
                
                case Dictionary<string, object> dict:
                    ApplyPrecisionToData(dict);
                    return dict;
                
                default:
                    // Handle anonymous objects and other complex types via reflection
                    if (value.GetType().IsClass && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                    {
                        return ApplyPrecisionToObject(value);
                    }
                    return value;
            }
        }

        private object ApplyPrecisionToObject(object obj)
        {
            var type = obj.GetType();
            var properties = type.GetProperties();
            
            // Create new anonymous object with rounded values
            var dict = new Dictionary<string, object>();
            foreach (var prop in properties)
            {
                try
                {
                    var propValue = prop.GetValue(obj);
                    dict[prop.Name] = ApplyPrecisionToValue(propValue);
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }
            return dict;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Optimized Recording with Batching
        /// </summary>

        private void RecordData()
        {
            if (_dataManager == null) return;

            var allData = CollectAllData();

            // Batch each data source separately
            foreach (var sourceData in allData)
            {
                string sourceName = sourceData.Key;
                var data = sourceData.Value;
                AddToBatch(sourceName, data);
            }
        }

        private void AddToBatch(string sourceName, Dictionary<string, object> data)
        {
            // Initialize batch if needed
            if (!_dataBatches.ContainsKey(sourceName))
            {
                _dataBatches[sourceName] = new List<Dictionary<string, object>>();
                _lastBatchTimes[sourceName] = Time.unscaledTime;
            }

            _dataBatches[sourceName].Add(data);

            // Check if batch should be flushed
            bool shouldFlush = ShouldFlushBatch(sourceName);
            if (shouldFlush)
            {
                FlushBatch(sourceName);
            }
        }

        private bool ShouldFlushBatch(string sourceName)
        {
            var batch = _dataBatches[sourceName];
            
            // Flush if EITHER condition is met:
            return batch.Count >= _minBatchSize ||     // Normal batching threshold
                   batch.Count >= _maxBatchSize;       // Hard limit (safety)
        }

        private void FlushBatch(string sourceName)
        {
            if (!_dataBatches.ContainsKey(sourceName) || _dataBatches[sourceName].Count == 0)
                return;

            try
            {
                string fileName = $"{sourceName}.json";
                var writer = GetOrCreateWriter(fileName);
                var batch = _dataBatches[sourceName];

                // Write each record as a separate line (NDJSON format)
                foreach (var record in batch)
                {
                    string jsonString = JsonConvert.SerializeObject(record, _jsonSettings);
                    writer.WriteLine(jsonString);
                }
                writer.Flush();

                // Clear batch and update time
                batch.Clear();
                _lastBatchTimes[sourceName] = Time.unscaledTime;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataRecorder] Failed to flush batch for {sourceName}: {e.Message}");
            }
        }

        private void FlushAllBatches()
        {
            foreach (var sourceName in new List<string>(_dataBatches.Keys))
            {
                FlushBatch(sourceName);
            }
        }

        private StreamWriter GetOrCreateWriter(string fileName)
        {
            if (!_jsonWriters.TryGetValue(fileName, out StreamWriter writer))
            {
                string filePath = Path.Combine(sessionPath, fileName);
                var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                writer = new StreamWriter(fileStream, Encoding.UTF8);
                _jsonWriters[fileName] = writer;
            }
            return writer;
        }

        private string GetRealWorldTimeString()
        {
            var time = DateTime.Now.TimeOfDay;
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";
        }

        private void LogDataSourcesOnce()
        {
            if (_dataManager == null || _dataManager._dataSources == null) return;
            foreach (var dataSource in _dataManager._dataSources)
            {
                try
                {
                    var sourceData = dataSource.CollectData();
                    Debug.Log($"[DataRecorder] Source: {dataSource.SourceName}, initial options: {sourceData?.Count ?? 0}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataRecorder] Failed to probe {dataSource.SourceName}: {e.Message}");
                }
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Inspector Information
        /// </summary>

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private string CurrentSessionPath => sessionPath ?? "Not recording";



        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private int FramesRecorded => _totalFramesRecorded;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private int FramesSkipped => _totalFramesSkipped;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private float RecordingDuration => startRecord && !string.IsNullOrEmpty(startTime) ? Time.unscaledTime - _nextRecordTime + _recordingInterval : 0f;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private string BatchStatus => startRecord ? 
            $"Batches: {string.Join(", ", _dataBatches.Keys.Select(k => $"{k}({_dataBatches[k].Count})"))}" : 
            "Not recording";
    }
}