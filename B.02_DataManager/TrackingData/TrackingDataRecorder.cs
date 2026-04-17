using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Sirenix.OdinInspector;
using Newtonsoft.Json;

namespace MetaFrame.Data
{
    public class TrackingDataRecorder : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;

        [BoxGroup("Output Settings")]
        [Tooltip("Optional — when assigned, the session folder will be prefixed with SubjectXX_. Falls back to FindObjectOfType if left empty.")]
        [SerializeField] private MetaFrame.State.ExperimentSequencer _experimentSequencer;

        [BoxGroup("Output Settings")]
        [FolderPath] public string _savePath;

        [BoxGroup("Output Settings")]
        [SerializeField]
        [Tooltip("Prefix for recording folder names (will be followed by timestamp)")]
        private string _folderPrefix = "Recording";

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(10, 1000)]
        [Tooltip("Recording interval in milliseconds")]
        private int _recordingIntervalMilliseconds = 10;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(0, 8)]
        [Tooltip("Decimal precision for all float data")]
        private int _decimalPrecision = 4;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Range(1, 40)]
        [Tooltip("Number of records to accumulate before flushing to the writer thread")]
        private int _batchSize = 10;

        // Runtime state
        public bool startRecord { get; private set; }
        private bool _isPaused = false;
        private string startTime;
        [HideInInspector] public string sessionPath;
        private float _recordingInterval;

        // Per-frame batch buffers (main thread only)
        private Dictionary<string, List<Dictionary<string, object>>> _dataBatches =
            new Dictionary<string, List<Dictionary<string, object>>>();

        // FIX: writer thread — all file I/O happens here, never on the main thread.
        // On Android/Quest, storage writes can stall the calling thread by 2–30ms
        // unpredictably due to the Android content provider layer. Moving writes off
        // the main thread eliminates these stalls from the frame budget entirely.
        private readonly ConcurrentQueue<(string fileName, string json)> _writeQueue =
            new ConcurrentQueue<(string, string)>();
        private Thread          _writerThread;
        private volatile bool   _writerRunning;

        // Writer-thread-only — never touched from main thread after StartRecording()
        private Dictionary<string, StreamWriter> _jsonWriters = new Dictionary<string, StreamWriter>();

        // Pre-allocated for performance
        private readonly StringBuilder _stringBuilder = new StringBuilder(4096);
        private JsonSerializerSettings _jsonSettings;

        // Performance monitoring
        private int   _totalFramesRecorded = 0;
        private int   _totalFramesSkipped  = 0;
        private float _nextRecordTime;

        // ── Events ─────────────────────────────────────────────────────────────────

        public event Action OnRecordingStarted;
        public event Action OnRecordingPaused;
        public event Action OnRecordingResumed;
        public event Action OnRecordingStopped;

        /*=========================================================================================================================*/
        /// <summary>
        /// Unity Lifecycle
        /// </summary>

        void Awake()
        {
            _recordingInterval = _recordingIntervalMilliseconds / 1000f;

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting           = Formatting.None,
                NullValueHandling    = NullValueHandling.Ignore,
                FloatFormatHandling  = FloatFormatHandling.String,
                FloatParseHandling   = FloatParseHandling.Double,
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
                FlushAllBatches();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && startRecord)
                FlushAllBatches();
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

                // FIX: start background writer thread before setting startRecord = true
                // so the first queued writes are guaranteed to have a running consumer.
                _writerRunning = true;
                _writerThread  = new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name         = "DataRecorder_Writer",
                };
                _writerThread.Start();

                startRecord     = true;
                _isPaused       = false;
                _nextRecordTime = Time.unscaledTime + _recordingInterval;

                Debug.Log($"[DataRecorder] Recording started. Interval: {_recordingIntervalMilliseconds}ms, " +
                          $"BatchSize: {_batchSize}. Session: {sessionPath}");
                OnRecordingStarted?.Invoke();
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
                _isPaused   = false;

                // Flush any remaining in-memory batches to the write queue
                FlushAllBatches();

                // Signal writer thread to drain the queue then exit
                _writerRunning = false;
                _writerThread?.Join(5000); // wait up to 5s for all queued writes to land
                _writerThread = null;

                // Now safe to close writers — the thread is done
                CloseAllWriters();

                Debug.Log($"[DataRecorder] Recording stopped. " +
                          $"Frames: {_totalFramesRecorded}, Skipped: {_totalFramesSkipped}");
                OnRecordingStopped?.Invoke();
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
            OnRecordingPaused?.Invoke();
        }

        [BoxGroup("Controls"), PropertyOrder(99)]
        [Button("Resume", ButtonSizes.Large), ShowIf("@startRecord && _isPaused")]
        public void Resume()
        {
            if (!startRecord || !_isPaused) return;
            _isPaused = false;
            Debug.Log("[DataRecorder] Recording resumed.");
            OnRecordingResumed?.Invoke();
        }

        private string GetCurrentStatusLabel() => "Status";

        private string GetCurrentStatus()
        {
            if (!startRecord) return "Not Recording";
            return _isPaused ? "Paused" : "Recording";
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Background Writer Thread
        /// </summary>

        private void WriterLoop()
        {
            // FIX: runs on a dedicated background thread so all file I/O is off the
            // main thread. Drains _writeQueue until signalled to stop AND queue is empty.
            while (_writerRunning || !_writeQueue.IsEmpty)
            {
                bool didWork = false;

                while (_writeQueue.TryDequeue(out var item))
                {
                    try
                    {
                        var writer = GetOrCreateWriter(item.fileName);
                        writer.WriteLine(item.json);
                        didWork = true;
                    }
                    catch (Exception e)
                    {
                        // Can't call Debug.Log from background thread on older Unity — log to console stderr instead
                        Console.Error.WriteLine($"[DataRecorder] Writer thread error for '{item.fileName}': {e.Message}");
                    }
                }

                // Flush after draining a burst to minimise open-file time
                if (didWork)
                {
                    foreach (var w in _jsonWriters.Values)
                    {
                        try { w.Flush(); }
                        catch { /* best-effort */ }
                    }
                }

                if (!didWork)
                    Thread.Sleep(1); // avoid busy-spin when queue is empty
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Session Setup
        /// </summary>

        private void CreateSessionDirectory()
        {
            startTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            if (_experimentSequencer == null)
#if UNITY_2023_1_OR_NEWER
                _experimentSequencer = UnityEngine.Object.FindFirstObjectByType<MetaFrame.State.ExperimentSequencer>();
#else
                _experimentSequencer = UnityEngine.Object.FindObjectOfType<MetaFrame.State.ExperimentSequencer>();
#endif

            string folderName;
            if (_experimentSequencer != null && _experimentSequencer.subjectID > 0)
            {
                string subjectPrefix = $"Subject{_experimentSequencer.subjectID:D2}";
                folderName = $"{subjectPrefix}_{_folderPrefix}_{startTime}";
                Debug.Log($"[DataRecorder] ExperimentSequencer found — using subject prefix '{subjectPrefix}'.");
            }
            else
            {
                folderName = $"{_folderPrefix}_{startTime}";
            }

            sessionPath = Path.Combine(_savePath, folderName);
            Directory.CreateDirectory(sessionPath);
            Debug.Log($"[DataRecorder] Session directory created: {sessionPath}");
        }

        private void InitializeBatchStructures()
        {
            _dataBatches.Clear();
            _totalFramesRecorded = 0;
            _totalFramesSkipped  = 0;
        }

        private void CloseAllWriters()
        {
            foreach (var writer in _jsonWriters.Values)
            {
                try { writer?.Flush(); writer?.Close(); writer?.Dispose(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataRecorder] Error closing writer: {e.Message}");
                }
            }
            _jsonWriters.Clear();
            _dataBatches.Clear();
            Debug.Log($"[DataRecorder] All writers closed. Total frames: {_totalFramesRecorded}");
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Data Collection
        /// </summary>

        private void RecordData()
        {
            if (_dataManager == null) return;

            var allData = CollectAllData();

            foreach (var sourceData in allData)
                AddToBatch(sourceData.Key, sourceData.Value);
        }

        private Dictionary<string, Dictionary<string, object>> CollectAllData()
        {
            var results  = new Dictionary<string, Dictionary<string, object>>();
            long epochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var dataSource in _dataManager._dataSources)
            {
                try
                {
                    var sourceData = dataSource.CollectData();
                    if (sourceData.Count == 0) continue;

                    var orderedData = new Dictionary<string, object>();
                    orderedData["timestamp"] = epochMs;

                    ApplyPrecisionToData(sourceData, _decimalPrecision);
                    foreach (var kvp in sourceData)
                        orderedData[kvp.Key] = kvp.Value;

                    // FIX: use pre-cached SourceNameLower instead of calling ToLower()
                    // (which allocates a new string) on every recording tick.
                    results[dataSource.SourceNameLower] = orderedData;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataRecorder] Failed to collect {dataSource.SourceName} data: {e.Message}");
                }
            }

            return results;
        }

        private void ApplyPrecisionToData(Dictionary<string, object> data, int precision)
        {
            var keys = new List<string>(data.Keys);
            foreach (var key in keys)
                data[key] = ApplyPrecisionToValue(data[key], precision);
        }

        private object ApplyPrecisionToValue(object value, int precision)
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
                    return value;
            }
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Batching — main thread accumulates, writer thread consumes
        /// </summary>

        private void AddToBatch(string sourceName, Dictionary<string, object> data)
        {
            if (!_dataBatches.ContainsKey(sourceName))
                _dataBatches[sourceName] = new List<Dictionary<string, object>>();

            _dataBatches[sourceName].Add(data);

            // FIX: simplified flush condition — previously had both _minBatchSize and
            // _maxBatchSize connected with || which meant _maxBatchSize never triggered
            // independently (any count >= min is always also the flush point). Replaced
            // with a single _batchSize threshold.
            if (_dataBatches[sourceName].Count >= _batchSize)
                FlushBatch(sourceName);
        }

        private void FlushBatch(string sourceName)
        {
            if (!_dataBatches.TryGetValue(sourceName, out var batch) || batch.Count == 0)
                return;

            string fileName = $"{sourceName}.json";

            // Serialize on main thread (fast), enqueue for file I/O on writer thread
            foreach (var record in batch)
            {
                string json = JsonConvert.SerializeObject(record, _jsonSettings);
                _writeQueue.Enqueue((fileName, json));
            }

            batch.Clear();
        }

        private void FlushAllBatches()
        {
            foreach (var sourceName in new List<string>(_dataBatches.Keys))
                FlushBatch(sourceName);
        }

        // Called only from the writer thread
        private StreamWriter GetOrCreateWriter(string fileName)
        {
            if (!_jsonWriters.TryGetValue(fileName, out StreamWriter writer))
            {
                string filePath  = Path.Combine(sessionPath, fileName);
                var fileStream   = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                writer           = new StreamWriter(fileStream, Encoding.UTF8);
                _jsonWriters[fileName] = writer;
            }
            return writer;
        }

        private void LogDataSourcesOnce()
        {
            if (_dataManager?._dataSources == null) return;
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
        private int WriteQueueDepth => _writeQueue.Count;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private string BatchStatus => startRecord
            ? $"Batches: {string.Join(", ", _dataBatches.Keys.Select(k => $"{k}({_dataBatches[k].Count})/{ _batchSize}"))}"
            : "Not recording";
    }
}
