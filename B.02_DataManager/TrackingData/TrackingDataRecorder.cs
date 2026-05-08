using System;
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
    // Recording is DEFERRED until ExperimentSequencer.OnSubjectIdConfirmed fires.
    // The historical behaviour was to call StartRecording() in Awake(), which created
    // the session folder synchronously at scene load. That doesn't work for the Quest
    // build because the subject ID arrives over LSL after scene load — we can't bake
    // it into the folder name unless we wait. So now:
    //   - Awake() caches config (no I/O).
    //   - OnEnable() subscribes to ExperimentSequencer.OnSubjectIdConfirmed.
    //   - On confirmation, StartRecording() runs and CreateSessionDirectory() reads
    //     the (now-known) subjectID and bakes it into the folder name.
    //   - Editor / PC dev path: ExperimentSequencer.Start() auto-confirms with the
    //     inspector-baked ID when _autoConfirmInEditorOnStart is true, so dev workflow
    //     is unchanged.
    // No execution-order attributes are used because OVR's components (OVRManager,
    // OVRFaceExpressions, OVRSkeleton, GazePose) are sensitive to being preempted.
    public class TrackingDataRecorder : MonoBehaviour
    {
        [SerializeField] private DataManager _dataManager;

        [BoxGroup("Output Settings")]
        [Tooltip("Optional — when assigned, the session folder will be prefixed with SubjectXX_. " +
                 "Falls back to FindObjectOfType if left empty.")]
        [SerializeField] private MetaFrame.State.ExperimentSequencer _experimentSequencer;

        [BoxGroup("Output Settings")]
        [Tooltip("Save path used in the Unity Editor and PC standalone builds. " +
                 "Absolute path. Example: C:\\TrackingData")]
        [FolderPath] public string _savePathPC = @"C:\TrackingData";

        [BoxGroup("Output Settings")]
        [Tooltip("Subfolder name used on Android headsets (Quest, Pico, etc). " +
                 "Resolves to Application.persistentDataPath/<this>, which on a Quest is:\n" +
                 "/sdcard/Android/data/<package>/files/<this>\n" +
                 "Retrieve over USB via MQDH, SideQuest, or `adb pull`.")]
        [SerializeField] private string _savePathAndroid = "TrackingData";

        [BoxGroup("Output Settings")]
        [SerializeField]
        [Tooltip("Prefix for recording folder names (will be followed by timestamp)")]
        private string _folderPrefix = "Recording";

        [BoxGroup("Recording Configuration")]
        [SerializeField, Range(10, 1000)]
        [Tooltip("Recording interval in milliseconds")]
        private int _recordingIntervalMilliseconds = 10;

        [BoxGroup("Recording Configuration")]
        [SerializeField, Range(0, 8)]
        [Tooltip("Decimal precision for all float data")]
        private int _decimalPrecision = 4;

        [BoxGroup("Recording Configuration")]
        [SerializeField, Range(1, 40)]
        [Tooltip("Number of records to accumulate before handing to the writer thread")]
        private int _batchSize = 10;

        [BoxGroup("Recording Configuration")]
        [SerializeField, Range(0.25f, 10f)]
        [Tooltip("How often the writer thread forces data to physical storage (seconds).\n" +
                 "Lower = less data lost on crash, higher = less I/O overhead.\n" +
                 "2s is a good default for Quest: bounds crash loss to ~200 records at 100Hz.")]
        private float _diskFlushIntervalSeconds = 2f;

        [BoxGroup("Recording Configuration")]
        [SerializeField]
        [Tooltip("Log when a tracking source transitions between present/absent (helps diagnose dropouts).")]
        private bool _logTrackingPresenceChanges = true;

        // ── Runtime state ──────────────────────────────────────────────────────────
        public bool startRecord { get; private set; }
        private bool _isPaused;
        private string _startTime;
        [HideInInspector] public string sessionPath;
        private float _recordingInterval;
        private float _nextRecordTime;

        // Per-frame batch buffers (MAIN THREAD ONLY)
        private readonly Dictionary<string, List<Dictionary<string, object>>> _dataBatches = new();

        // ── Writer thread plumbing ─────────────────────────────────────────────────
        // All file I/O happens on a background thread. On Android/Quest, storage writes
        // can stall the calling thread unpredictably (2–30ms) due to the content provider
        // layer. Moving writes off the main thread keeps them out of the frame budget.
        //
        // FIX: queue now carries the raw Dictionary — precision rounding AND JSON
        // serialization happen on the writer thread, not the main thread. On the main
        // thread, FlushBatch becomes allocation-free (just references passed around),
        // which eliminates the 2–8ms serialization spike that used to happen every
        // _batchSize ticks.
        private readonly ConcurrentQueue<(string fileName, Dictionary<string, object> record)> _writeQueue = new();
        private Thread _writerThread;
        private volatile bool _writerRunning;
        private volatile bool _writerAlive;              // set false if the loop dies unexpectedly
        private ManualResetEventSlim _writerWakeup;      // signal: queue has items
        private ManualResetEventSlim _writerIdle;        // signal: queue drained, writers flushed

        // Writer-thread-only state — do NOT touch from main thread
        private readonly Dictionary<string, StreamWriter> _jsonWriters = new();
        private readonly Dictionary<string, FileStream> _fileStreams = new();
        private readonly HashSet<string> _dirtyWriters = new();
        private DateTime _lastDiskFlush = DateTime.MinValue;

        // JSON
        private JsonSerializerSettings _jsonSettings;

        // Diagnostics
        private int _totalFramesRecorded;
        private int _totalFramesSkipped;
        private int _totalWriteFailures;
        private int _totalDiskFlushes;

        // Source presence tracking (for logging reconnects)
        private readonly Dictionary<string, bool> _sourcePresence = new();

        // ── Events ─────────────────────────────────────────────────────────────────
        public event Action OnRecordingStarted;
        public event Action OnRecordingPaused;
        public event Action OnRecordingResumed;
        public event Action OnRecordingStopped;

        /*=========================================================================================================================*/
        /// <summary>Unity Lifecycle</summary>

        void Awake()
        {
            _recordingInterval = _recordingIntervalMilliseconds / 1000f;

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                FloatFormatHandling = FloatFormatHandling.String,
                FloatParseHandling = FloatParseHandling.Double,
            };

            // Recording start is DEFERRED until ExperimentSequencer.OnSubjectIdConfirmed
            // fires (see OnEnable). This is the change for the Quest build: the subject
            // ID is set at runtime via the LSL handshake, not baked into the scene, so
            // we cannot create the session folder until we know it.
        }

        void OnEnable()
        {
            // Subscribe to the sequencer's subject-confirmation event. This is the
            // signal that subjectID is now valid and we can create the session folder
            // with the correct SubjectXX_ prefix.
            MetaFrame.State.ExperimentSequencer.OnSubjectIdConfirmed += OnSubjectIdConfirmed;

            // Catch-up case: the sequencer may have already fired the event before
            // our OnEnable ran (script-execution-order race). If so, start now.
            // This mirrors the same defensive pattern VideoRecorder uses for our
            // OnRecordingStarted event.
            var seq = MetaFrame.State.ExperimentSequencer.instance;
            if (seq != null && seq.IsSubjectIdConfirmed && !startRecord)
            {
                Debug.Log("[DataRecorder] OnEnable — sequencer already confirmed; catching up.");
                StartRecording();
            }
        }

        void OnDisable()
        {
            // Always unsubscribe in OnDisable — Unity calls OnDisable before OnDestroy
            // for component teardown, and also during domain reload in the editor.
            // Subscribing in OnEnable + unsubscribing in OnDisable is the standard
            // safe pair for Unity's static-event lifecycle.
            MetaFrame.State.ExperimentSequencer.OnSubjectIdConfirmed -= OnSubjectIdConfirmed;
        }

        private void OnSubjectIdConfirmed(int subjectID)
        {
            if (startRecord)
            {
                // Idempotent — sequencer fired confirmation again (override path).
                Debug.Log($"[DataRecorder] OnSubjectIdConfirmed({subjectID}) — already recording, ignoring.");
                return;
            }
            Debug.Log($"[DataRecorder] OnSubjectIdConfirmed({subjectID}) — starting recording.");
            StartRecording();
        }

        void Update()
        {
            if (!startRecord) return;

            float now = Time.unscaledTime;
            if (now < _nextRecordTime) return;

            if (_isPaused)
            {
                _totalFramesSkipped++;
                // FIX: keep advancing _nextRecordTime while paused so we don't build up
                // a debt that replays as a tick storm on resume.
                _nextRecordTime = now + _recordingInterval;
                return;
            }

            try
            {
                RecordData();
                _totalFramesRecorded++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataRecorder] Frame recording failed: {e.Message}");
            }

            _nextRecordTime += _recordingInterval;

            // FIX: if we've fallen behind by more than 2 intervals (GC spike, hitch,
            // editor breakpoint), don't try to catch up — re-anchor to now. Prevents
            // a burst of back-to-back records with artificial timestamps.
            if (now - _nextRecordTime > _recordingInterval * 2f)
                _nextRecordTime = now + _recordingInterval;
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (!startRecord) return;
            // On Quest, this fires when the headset comes off. If the OS kills us while
            // paused (battery, thermal, background timeout) anything in our buffers is
            // lost. Flush SYNCHRONOUSLY all the way to physical storage.
            if (pauseStatus)
            {
                Debug.Log("[DataRecorder] App paused — forcing sync flush to disk.");
                ForceFlushToDisk(timeoutMs: 3000);
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!startRecord) return;
            if (!hasFocus)
            {
                // Less urgent than pause (app is still running) but still worth flushing.
                FlushAllBatches();
            }
        }

        void OnApplicationQuit()
        {
            // FIX: hooked in addition to OnDestroy. OnDestroy normally runs on quit,
            // but abrupt termination paths on Android can skip it. OnApplicationQuit
            // is the more reliable last-chance hook.
            StopRecording();
        }

        void OnDestroy()
        {
            StopRecording();
        }

        /*=========================================================================================================================*/
        /// <summary>Recording Control</summary>

        public void StartRecording()
        {
            if (startRecord) return;

            // Hard guard: never create a session folder without a confirmed subject ID.
            // The previous design silently fell back to "Recording_<timestamp>" if the
            // sequencer wasn't found — that produced anonymous data folders nobody
            // could match back to a subject. Refuse instead.
            var seq = MetaFrame.State.ExperimentSequencer.instance;
            if (seq == null || !seq.IsSubjectIdConfirmed || seq.subjectID < 1)
            {
                Debug.LogError(
                    "[DataRecorder] StartRecording refused — subject ID is not confirmed. " +
                    "Recording must be triggered by ExperimentSequencer.OnSubjectIdConfirmed " +
                    "(via LSL handshake on Quest, or the editor confirm button in dev). " +
                    "If you're hitting this in the editor, set _autoConfirmInEditorOnStart " +
                    "= true on ExperimentSequencer or click Confirm in Play Mode.");
                return;
            }

            try
            {
                CreateSessionDirectory();
                _dataBatches.Clear();
                _sourcePresence.Clear();
                _totalFramesRecorded = 0;
                _totalFramesSkipped = 0;
                _totalWriteFailures = 0;
                _totalDiskFlushes = 0;
                LogDataSourcesOnce();

                // Start writer thread BEFORE setting startRecord = true, so any
                // queued writes are guaranteed to have a running consumer.
                _writerWakeup = new ManualResetEventSlim(false);
                _writerIdle = new ManualResetEventSlim(true);
                _writerRunning = true;
                _writerAlive = true;
                _writerThread = new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name = "DataRecorder_Writer",
                };
                _writerThread.Start();

                startRecord = true;
                _isPaused = false;
                _nextRecordTime = Time.unscaledTime + _recordingInterval;

                Debug.Log($"[DataRecorder] Recording started. Interval: {_recordingIntervalMilliseconds}ms, " +
                          $"BatchSize: {_batchSize}, DiskFlush: {_diskFlushIntervalSeconds}s. Session: {sessionPath}");
                OnRecordingStarted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataRecorder] Failed to start recording: {e.Message}\n{e.StackTrace}");
                // Best-effort cleanup
                _writerRunning = false;
                _writerWakeup?.Set();
            }
        }

        public void StopRecording()
        {
            if (!startRecord) return;

            try
            {
                startRecord = false;
                _isPaused = false;

                // Push any remaining in-memory batches to the write queue
                FlushAllBatches();

                // Signal the writer to drain + exit
                _writerRunning = false;
                _writerWakeup?.Set();
                _writerThread?.Join(5000);   // up to 5s for the queue to drain
                _writerThread = null;

                // Safe to close writers — thread is done
                CloseAllWriters();

                _writerWakeup?.Dispose();
                _writerIdle?.Dispose();
                _writerWakeup = null;
                _writerIdle = null;

                Debug.Log($"[DataRecorder] Recording stopped. " +
                          $"Frames: {_totalFramesRecorded}, Skipped: {_totalFramesSkipped}, " +
                          $"WriteFailures: {_totalWriteFailures}, DiskFlushes: {_totalDiskFlushes}");
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
            // Also flush batches on pause so a subsequent crash doesn't lose the
            // in-memory window.
            FlushAllBatches();
            Debug.Log("[DataRecorder] Recording paused.");
            OnRecordingPaused?.Invoke();
        }

        [BoxGroup("Controls"), PropertyOrder(99)]
        [Button("Resume", ButtonSizes.Large), ShowIf("@startRecord && _isPaused")]
        public void Resume()
        {
            if (!startRecord || !_isPaused) return;
            _isPaused = false;
            // FIX: re-anchor the tick schedule so we don't fire a burst of catch-up
            // records. The next record happens one full interval from now.
            _nextRecordTime = Time.unscaledTime + _recordingInterval;
            Debug.Log("[DataRecorder] Recording resumed.");
            OnRecordingResumed?.Invoke();
        }

        /// <summary>
        /// Synchronously pushes everything to physical storage:
        ///   1. In-memory batches → write queue (serialize on main thread)
        ///   2. Wait for writer thread to drain the queue
        ///   3. StreamWriter.Flush() on all dirty writers
        ///   4. FileStream.Flush(true) — force OS to commit to flash
        /// Use this when you need to guarantee no data loss (app pause, trial boundary, etc).
        /// </summary>
        public void ForceFlushToDisk(int timeoutMs = 3000)
        {
            if (!startRecord) return;

            // Signal that we want to know when the queue is drained
            _writerIdle?.Reset();
            FlushAllBatches();
            _writerWakeup?.Set();

            // Wait for writer to drain + commit. If it times out we still return —
            // better to continue with some risk than to hang the main thread.
            bool drained = _writerIdle?.Wait(timeoutMs) ?? false;
            if (!drained)
                Debug.LogWarning($"[DataRecorder] ForceFlushToDisk timed out after {timeoutMs}ms. " +
                                 $"Queue depth: {_writeQueue.Count}");
        }

        private string GetCurrentStatusLabel() => "Status";

        private string GetCurrentStatus()
        {
            if (!startRecord)
            {
                var seq = MetaFrame.State.ExperimentSequencer.instance;
                if (seq == null) return "Waiting — no ExperimentSequencer in scene";
                if (!seq.IsSubjectIdConfirmed) return "Waiting — subject ID not confirmed (LSL handshake pending)";
                return "Idle (recording stopped)";
            }
            if (!_writerAlive) return "ERROR — Writer thread died";
            return _isPaused ? "Paused" : "Recording";
        }

        /*=========================================================================================================================*/
        /// <summary>Background Writer Thread</summary>

        private void WriterLoop()
        {
            // FIX: top-level catch. If anything escapes the inner handling, the thread
            // was silently dying and the queue would fill forever with no indication.
            try
            {
                WriterLoopInner();
            }
            catch (Exception fatal)
            {
                _writerAlive = false;
                Console.Error.WriteLine($"[DataRecorder] WRITER THREAD DIED: {fatal}");
                // Can't call Debug.Log from here on older Unity — stderr is the safe bet.
            }
        }

        private void WriterLoopInner()
        {
            _lastDiskFlush = DateTime.UtcNow;

            while (_writerRunning || !_writeQueue.IsEmpty)
            {
                // FIX: block on an event instead of Thread.Sleep(1). Wakes instantly when
                // data arrives, avoids 1ms latency and 1000Hz scheduler churn while idle.
                _writerWakeup.Wait(100);  // 100ms so periodic-flush check still runs when idle
                _writerWakeup.Reset();

                bool didWork = DrainQueue();

                // FIX: only flush writers we actually wrote to in this burst, not every
                // open writer. With 4 sources that was 4× unnecessary flush per burst.
                if (didWork)
                    FlushDirtyStreamWriters();

                // FIX: periodic force-to-disk. Bounds the worst-case data loss from a
                // crash to _diskFlushIntervalSeconds worth of records. StreamWriter.Flush
                // alone does NOT commit to flash — FileStream.Flush(true) does.
                if ((DateTime.UtcNow - _lastDiskFlush).TotalSeconds >= _diskFlushIntervalSeconds)
                {
                    ForceAllFileStreamsToDisk();
                    _lastDiskFlush = DateTime.UtcNow;
                }

                // Signal idle if the queue is empty and we've committed everything.
                // ForceFlushToDisk waits on this.
                if (_writeQueue.IsEmpty && _dirtyWriters.Count == 0)
                    _writerIdle.Set();
            }

            // Final drain + commit before the thread exits
            DrainQueue();
            FlushDirtyStreamWriters();
            ForceAllFileStreamsToDisk();
            _writerIdle.Set();
        }

        private bool DrainQueue()
        {
            bool didWork = false;
            while (_writeQueue.TryDequeue(out var item))
            {
                try
                {
                    // FIX: precision rounding and JSON serialization moved here from
                    // the main thread. Both are pure CPU work on data the main thread
                    // no longer owns. This is the single biggest frame-rate win:
                    // eliminates the 2–8ms serialization spike that used to hit the
                    // main thread every _batchSize ticks.
                    ApplyPrecisionToData(item.record, _decimalPrecision);
                    string json = JsonConvert.SerializeObject(item.record, _jsonSettings);

                    var writer = GetOrCreateWriter(item.fileName);
                    writer.WriteLine(json);
                    _dirtyWriters.Add(item.fileName);
                    didWork = true;
                }
                catch (Exception e)
                {
                    _totalWriteFailures++;
                    Console.Error.WriteLine($"[DataRecorder] Writer error for '{item.fileName}': {e.Message}");
                }
            }
            return didWork;
        }

        private void FlushDirtyStreamWriters()
        {
            foreach (var fileName in _dirtyWriters)
            {
                if (_jsonWriters.TryGetValue(fileName, out var w))
                {
                    try { w.Flush(); }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"[DataRecorder] Flush failed for '{fileName}': {e.Message}");
                    }
                }
            }
            _dirtyWriters.Clear();
        }

        private void ForceAllFileStreamsToDisk()
        {
            // FileStream.Flush(true) is the only call that guarantees data reaches
            // physical storage on Android. Without this, files on disk can be up to
            // a few seconds stale when the OS kills the app.
            foreach (var fs in _fileStreams.Values)
            {
                try { fs.Flush(true); }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[DataRecorder] Disk flush failed: {e.Message}");
                }
            }
            _totalDiskFlushes++;
        }

        /*=========================================================================================================================*/
        /// <summary>Session Setup</summary>

        private void CreateSessionDirectory()
        {
            _startTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            if (_experimentSequencer == null)
#if UNITY_2023_1_OR_NEWER
                _experimentSequencer = UnityEngine.Object.FindFirstObjectByType<MetaFrame.State.ExperimentSequencer>();
#else
                _experimentSequencer = UnityEngine.Object.FindObjectOfType<MetaFrame.State.ExperimentSequencer>();
#endif

            // The guard in StartRecording() guarantees a confirmed subject ID exists
            // before we get here, so we can fail hard if it's not. The old "fall back
            // to Recording_<timestamp>" branch was a footgun — it produced unlabelled
            // recordings that couldn't be matched to a subject post hoc.
            if (_experimentSequencer == null || !_experimentSequencer.IsSubjectIdConfirmed || _experimentSequencer.subjectID < 1)
            {
                throw new System.InvalidOperationException(
                    "[DataRecorder] CreateSessionDirectory called without a confirmed subject ID. " +
                    "This should be unreachable — the StartRecording guard runs first.");
            }

            string subjectPrefix = $"Subject{_experimentSequencer.subjectID:D2}";
            string folderName = $"{subjectPrefix}_{_folderPrefix}_{_startTime}";
            Debug.Log($"[DataRecorder] Using subject prefix '{subjectPrefix}'.");

            // Resolve the platform-specific base save path.
            // - Editor / PC standalone → absolute path on disk (_savePathPC)
            // - Android headsets       → Application.persistentDataPath/<_savePathAndroid>
            //   which on Quest is /sdcard/Android/data/<package>/files/<_savePathAndroid>
            string basePath;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            basePath = _savePathPC;
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(Application.persistentDataPath, "Recordings");
                Debug.LogWarning($"[DataRecorder] _savePathPC not set — falling back to '{basePath}'.");
            }
#elif UNITY_ANDROID
            string androidSubfolder = string.IsNullOrEmpty(_savePathAndroid) ? "Recordings" : _savePathAndroid;
            basePath = Path.Combine(Application.persistentDataPath, androidSubfolder);
#else
            basePath = Path.Combine(Application.persistentDataPath, "Recordings");
            Debug.LogWarning($"[DataRecorder] Unrecognised platform — falling back to '{basePath}'.");
#endif

            sessionPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(sessionPath);
            Debug.Log($"[DataRecorder] Session directory: {sessionPath}");
        }

        private void CloseAllWriters()
        {
            // Called after the writer thread has joined — safe to touch these dicts.
            foreach (var writer in _jsonWriters.Values)
            {
                try { writer?.Flush(); writer?.Close(); writer?.Dispose(); }
                catch (Exception e) { Debug.LogWarning($"[DataRecorder] Error closing writer: {e.Message}"); }
            }
            foreach (var fs in _fileStreams.Values)
            {
                try { fs?.Flush(true); fs?.Close(); fs?.Dispose(); }
                catch (Exception e) { Debug.LogWarning($"[DataRecorder] Error closing stream: {e.Message}"); }
            }
            _jsonWriters.Clear();
            _fileStreams.Clear();
            _dataBatches.Clear();
            _dirtyWriters.Clear();
            Debug.Log($"[DataRecorder] All writers closed. Total frames: {_totalFramesRecorded}");
        }

        /*=========================================================================================================================*/
        /// <summary>Data Collection</summary>

        private void RecordData()
        {
            if (_dataManager == null) return;

            var allData = CollectAllData();
            foreach (var sourceData in allData)
                AddToBatch(sourceData.Key, sourceData.Value);
        }

        private Dictionary<string, Dictionary<string, object>> CollectAllData()
        {
            var results = new Dictionary<string, Dictionary<string, object>>();
            long epochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var dataSource in _dataManager._dataSources)
            {
                Dictionary<string, object> sourceData = null;
                try
                {
                    sourceData = dataSource.CollectData();
                }
                catch (Exception e)
                {
                    // Defensive — a source going permanently bad should NOT break
                    // the others. Log the failure, continue to next source.
                    Debug.LogWarning($"[DataRecorder] {dataSource.SourceName} threw in CollectData: {e.Message}");
                    TrackPresence(dataSource.SourceNameLower, present: false);
                    continue;
                }

                bool present = sourceData != null && sourceData.Count > 0;
                TrackPresence(dataSource.SourceNameLower, present);
                if (!present) continue;

                // FIX: no longer apply precision rounding here — it runs on the writer
                // thread before serialization. Main thread's job is just to collect.
                var orderedData = new Dictionary<string, object>(sourceData.Count + 1)
                {
                    ["timestamp"] = epochMs
                };
                foreach (var kvp in sourceData)
                    orderedData[kvp.Key] = kvp.Value;

                results[dataSource.SourceNameLower] = orderedData;
            }

            return results;
        }

        /// <summary>
        /// Logs when a source transitions between present and absent. Helps diagnose
        /// silent dropouts ("why is hand data missing from trial 7?") and slow-to-start
        /// trackers ("face tracking didn't seem to work") after the fact.
        /// </summary>
        private void TrackPresence(string sourceKey, bool present)
        {
            if (!_logTrackingPresenceChanges) return;
            if (_sourcePresence.TryGetValue(sourceKey, out bool wasPresent))
            {
                if (wasPresent != present)
                {
                    if (present)
                        Debug.Log($"[DataRecorder] ✓ Source '{sourceKey}' RECOVERED — " +
                                  $"first record at frame {_totalFramesRecorded} " +
                                  $"(~{(_totalFramesRecorded * _recordingInterval):F1}s after recording start).");
                    else
                        Debug.LogWarning($"[DataRecorder] ⚠ Source '{sourceKey}' LOST at frame {_totalFramesRecorded}.");
                }
            }
            _sourcePresence[sourceKey] = present;
        }

        private void ApplyPrecisionToData(Dictionary<string, object> data, int precision)
        {
            // FIX: iterate via a snapshot key list because we mutate values (some paths
            // create new objects like float[]). Can't enumerate and assign in one pass.
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
        /// <summary>Batching — main thread accumulates, writer thread consumes</summary>

        private void AddToBatch(string sourceName, Dictionary<string, object> data)
        {
            if (!_dataBatches.TryGetValue(sourceName, out var list))
            {
                list = new List<Dictionary<string, object>>(_batchSize);
                _dataBatches[sourceName] = list;
            }

            list.Add(data);

            if (list.Count >= _batchSize)
                FlushBatch(sourceName);
        }

        private void FlushBatch(string sourceName)
        {
            if (!_dataBatches.TryGetValue(sourceName, out var batch) || batch.Count == 0)
                return;

            string fileName = $"{sourceName}.json";

            // FIX: no serialization on main thread. Hand the Dictionary references
            // directly to the writer thread. Ownership transfers — main thread must
            // never read or write these dicts after this point. The writer thread
            // rounds floats + serializes + writes in one pass.
            //
            // This is safe because each source's CollectData produces fresh arrays
            // every tick (GazeData caches the wrapper but GetGazeDataDictionary copies
            // into fresh float[], Hand/FACS/Body allocate fresh). So no aliasing.
            foreach (var record in batch)
                _writeQueue.Enqueue((fileName, record));

            batch.Clear();
            _writerWakeup?.Set();
            _writerIdle?.Reset();
        }

        private void FlushAllBatches()
        {
            // Snapshot keys — FlushBatch doesn't remove entries but being defensive
            foreach (var sourceName in new List<string>(_dataBatches.Keys))
                FlushBatch(sourceName);
        }

        // Called only from the writer thread
        private StreamWriter GetOrCreateWriter(string fileName)
        {
            if (!_jsonWriters.TryGetValue(fileName, out StreamWriter writer))
            {
                string filePath = Path.Combine(sessionPath, fileName);

                // Larger buffer (8KB) since we write small JSON lines at 100Hz.
                // FileShare.Read so an external tool can tail the file while recording.
                // No FileOptions — we manage flush-to-disk explicitly via periodic
                // fs.Flush(true) which is more predictable cross-platform.
                var fileStream = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 8192);

                writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                _jsonWriters[fileName] = writer;
                _fileStreams[fileName] = fileStream;
            }
            return writer;
        }

        private void LogDataSourcesOnce()
        {
            if (_dataManager?._dataSources == null || _dataManager._dataSources.Count == 0)
            {
                Debug.LogWarning("[DataRecorder] No data sources registered yet. " +
                                 "Check DataManager.InitializeDataSources() is being called.");
                return;
            }

            // FIX: explicit startup diagnostic per source. Distinguishes:
            //   - source registered with 0 fields  → tracker not ready yet (normal — will log RECOVERED later)
            //   - source throws on first probe     → dependency missing (e.g. _faceExpressions not assigned)
            //   - source has N fields              → ready immediately
            //
            // This is the log you want when face/eye tracking "seems not to start" —
            // tells you up front whether we're waiting on the tracker (fine) or whether
            // something is genuinely broken (needs fixing).
            foreach (var dataSource in _dataManager._dataSources)
            {
                string key = dataSource.SourceNameLower ?? dataSource.SourceName.ToLower();
                try
                {
                    var sourceData = dataSource.CollectData();
                    int count = sourceData?.Count ?? 0;

                    // Seed the presence table so the first transition is logged correctly.
                    _sourcePresence[key] = count > 0;

                    if (count > 0)
                    {
                        Debug.Log($"[DataRecorder] ✓ {dataSource.SourceName} ready ({count} fields) at startup.");
                    }
                    else
                    {
                        // Most common case for FACS/Gaze on Quest — tracker not yet initialised.
                        // Not an error; will start recording as soon as it comes online.
                        Debug.Log($"[DataRecorder] ⧗ {dataSource.SourceName} not ready at startup " +
                                  $"(tracker initialising, permissions pending, or hardware absent). " +
                                  $"Will log RECOVERED if/when it comes online.");
                    }
                }
                catch (Exception e)
                {
                    // An exception here usually means a SerializeField reference is null
                    // (e.g. _faceExpressions, _gazePose not assigned in Inspector) or a
                    // downstream null chain. Record as absent so the first recovery gets logged.
                    _sourcePresence[key] = false;
                    Debug.LogError($"[DataRecorder] ✕ {dataSource.SourceName} threw on startup probe: " +
                                   $"{e.Message}. Check Inspector references and OVR component setup.");
                }
            }
        }

        /*=========================================================================================================================*/
        /// <summary>Inspector Diagnostics</summary>

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
        private int WriteFailures => _totalWriteFailures;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private int DiskFlushes => _totalDiskFlushes;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private bool WriterThreadAlive => _writerAlive;

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private string BatchStatus => startRecord
            ? $"Batches: {string.Join(", ", _dataBatches.Keys.Select(k => $"{k}({_dataBatches[k].Count})/{_batchSize}"))}"
            : "Not recording";

        [BoxGroup("Runtime Info")]
        [ShowInInspector, ReadOnly]
        private string SourcePresence => startRecord
            ? string.Join(", ", _sourcePresence.Select(kv => $"{kv.Key}:{(kv.Value ? "✓" : "✕")}"))
            : "Not recording";
    }
}