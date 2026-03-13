using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MetaFrame.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MetaFrame.Data
{
    // ── JSON Data Models ──────────────────────────────────────────────────────────

    public class TransitionEvent
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string source;     // GameObject name — populated for anomaly transitions
        public string from;
        public string to;
        public string timestamp;

        public TransitionEvent(string from, string to, DateTime time, string source = null)
        {
            this.source = string.IsNullOrEmpty(source) ? null : source;
            this.from   = from;
            this.to     = to;
            timestamp   = time.ToString("yyyy-MM-dd_HH-mm-ss.fff");
        }
    }

    public class SurveyEntry
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string detection;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string confidence;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string plausibility;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string reportStart;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string reportEnd;
    }

    public class TrialRecord
    {
        public int    trialNumber;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string anomaly;

        public List<TransitionEvent> stateTransitions   = new();
        public List<TransitionEvent> anomalyTransitions = new();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SurveyEntry survey;
    }

    public class SessionRecord
    {
        public string sessionLabel;
        public string sessionStart;
        public List<TrialRecord> trials = new();
    }

    public class ExperimentRecord
    {
        public int    subjectID;
        public string experimentStart;
        public List<SessionRecord> sessions = new();
    }

    // ── Survey Input ──────────────────────────────────────────────────────────────

    public class SurveyData
    {
        public string detection;
        public string confidence;
        public string plausibility;
        public string reportStart;
    }

    // ── SurveyDataRecorder ────────────────────────────────────────────────────────

    public class SurveyDataRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameStateManager      gsm;
        [SerializeField] private TrackingDataRecorder  trackingdataRecorder;

        // ── File path ──────────────────────────────────────────────────────────────

        private string OutputPath => trackingdataRecorder != null && !string.IsNullOrEmpty(trackingdataRecorder.sessionPath)
            ? Path.Combine(trackingdataRecorder.sessionPath, "ExperimentData.json")
            : null;

        // ── In-memory experiment data ──────────────────────────────────────────────

        private ExperimentRecord _experiment;

        // ── Current context pointers ──────────────────────────────────────────────

        private SessionRecord _currentSession;
        private TrialRecord   _currentTrial;
        private int           _trialNumber;

        // ── Dynamic ASM tracking ──────────────────────────────────────────────────

        private readonly List<AnomalyStateManager> _trackedAsms = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (gsm == null) gsm = GameStateManager.instance;
        }

        private void OnEnable()
        {
            ExperimentSequencer.OnExperimentBegan += OnExperimentBegan;
            ExperimentSequencer.OnSessionBegan    += OnSessionBegan;
            ExperimentSequencer.OnTrialBegan      += OnTrialBegan;
            ExperimentSequencer.OnTrialEnded      += OnTrialEnded;

            if (gsm != null)
                gsm.OnStateChanged += OnStateChanged;

            AnomalyStateManager.OnRegistered   += TrackAsm;
            AnomalyStateManager.OnUnregistered += UntrackAsm;
        }

        private void OnDisable()
        {
            ExperimentSequencer.OnExperimentBegan -= OnExperimentBegan;
            ExperimentSequencer.OnSessionBegan    -= OnSessionBegan;
            ExperimentSequencer.OnTrialBegan      -= OnTrialBegan;
            ExperimentSequencer.OnTrialEnded      -= OnTrialEnded;

            if (gsm != null)
                gsm.OnStateChanged -= OnStateChanged;

            AnomalyStateManager.OnRegistered   -= TrackAsm;
            AnomalyStateManager.OnUnregistered -= UntrackAsm;

            foreach (var asm in _trackedAsms)
                asm.OnAnomalyStateChanged -= OnAnomalyStateChanged;
            _trackedAsms.Clear();
        }

        // ── ASM Registration ──────────────────────────────────────────────────────

        private void TrackAsm(AnomalyStateManager asm)
        {
            if (_trackedAsms.Contains(asm)) return;
            _trackedAsms.Add(asm);
            asm.OnAnomalyStateChanged += OnAnomalyStateChanged;
            Debug.Log($"[SurveyDataRecorder] Tracking ASM: {asm.gameObject.name} ({_trackedAsms.Count} total)");
        }

        private void UntrackAsm(AnomalyStateManager asm)
        {
            if (!_trackedAsms.Remove(asm)) return;
            asm.OnAnomalyStateChanged -= OnAnomalyStateChanged;
            Debug.Log($"[SurveyDataRecorder] Untracked ASM: {asm.gameObject.name} ({_trackedAsms.Count} remaining)");
        }

        // ── Sequencer Event Handlers ──────────────────────────────────────────────

        private void OnExperimentBegan(int subjectID)
        {
            if (OutputPath == null)
            {
                Debug.LogError("[SurveyDataRecorder] No sessionPath yet. Call StartRecording() first.");
                return;
            }

            _experiment = new ExperimentRecord
            {
                subjectID       = subjectID,
                experimentStart = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
            };

            _trialNumber = 0;
            Debug.Log($"[SurveyDataRecorder] Experiment initialised. Output: {OutputPath}");
        }

        private void OnSessionBegan(string sessionLabel)
        {
            _currentSession = new SessionRecord
            {
                sessionLabel = sessionLabel,
                sessionStart = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
            };

            _experiment.sessions.Add(_currentSession);
            _trialNumber = 0;

            Debug.Log($"[SurveyDataRecorder] Session begun: {sessionLabel}");
        }

        private void OnTrialBegan(AnomalyDefinition anomaly)
        {
            if (_currentSession == null)
            {
                Debug.LogError("[SurveyDataRecorder] OnTrialBegan received with no active session.");
                return;
            }

            _trialNumber++;
            string anomalyId = anomaly != null ? anomaly.id : null;

            _currentTrial = new TrialRecord
            {
                trialNumber = _trialNumber,
                anomaly     = anomalyId,
            };

            _currentSession.trials.Add(_currentTrial);
            Debug.Log($"[SurveyDataRecorder] Trial {_trialNumber} begun. Anomaly: {anomalyId ?? "NORMAL"}");
        }

        private void OnTrialEnded()
        {
            if (_currentTrial == null)
            {
                Debug.LogError("[SurveyDataRecorder] OnTrialEnded received with no active trial.");
                return;
            }

            _currentTrial = null;
            Flush();
            Debug.Log("[SurveyDataRecorder] Trial ended and flushed.");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Submit survey responses for the current trial.
        /// Attaches them to the current trial record and flushes to disk.
        /// </summary>
        public void SubmitSurvey(SurveyData data)
        {
            if (_currentTrial == null)
            {
                Debug.LogError("[SurveyDataRecorder] SubmitSurvey called with no active trial.");
                return;
            }

            _currentTrial.survey = new SurveyEntry
            {
                detection    = data.detection,
                confidence   = data.confidence,
                plausibility = data.plausibility,
                reportStart  = data.reportStart,
                reportEnd    = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
            };

            Flush();
        }

        // ── GSM Event Handlers ────────────────────────────────────────────────────

        private void OnStateChanged(int fromIndex, int toIndex, DateTime timestamp)
        {
            if (_currentTrial == null) return;

            _currentTrial.stateTransitions.Add(new TransitionEvent(
                from: gsm.StateName(fromIndex),
                to:   gsm.StateName(toIndex),
                time: timestamp));
        }

        // ── ASM Event Handler ─────────────────────────────────────────────────────

        private void OnAnomalyStateChanged(AnomalyStateManager sender, AnomalyState from, AnomalyState to, DateTime timestamp)
        {
            if (_currentTrial == null) return;

            _currentTrial.anomalyTransitions.Add(new TransitionEvent(
                from:   from.ToString(),
                to:     to.ToString(),
                time:   timestamp,
                source: sender.gameObject.name));
        }

        // ── File Writing ──────────────────────────────────────────────────────────

        private void Flush()
        {
            string path = OutputPath;
            if (path == null)
            {
                Debug.LogError("[SurveyDataRecorder] Cannot flush — no output path available.");
                return;
            }
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting        = Formatting.Indented,
                };
                string json = JsonConvert.SerializeObject(_experiment, settings);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SurveyDataRecorder] Failed to write file: {e.Message}");
            }
        }
    }
}