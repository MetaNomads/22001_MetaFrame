using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MetaFrame.State;
using Newtonsoft.Json;

namespace MetaFrame.Data
{
    // ── JSON Data Models ──────────────────────────────────────────────────────────

    public class TransitionEvent
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string source;
        public string from;
        public string to;
        public string timestamp;

        public TransitionEvent(string from, string to, DateTime time, string source = null)
        {
            this.source   = string.IsNullOrEmpty(source) ? null : source;
            this.from     = from;
            this.to       = to;
            this.timestamp = time.ToString("yyyy-MM-dd_HH-mm-ss.fff");
        }
    }

    public class SurveyEntry
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string detection;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string confidence;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string plausibility;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string reportStart;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string reportEnd;
    }

    public class TrialRecord
    {
        public int    trialNumber;
        public string stimulus;

        public List<TransitionEvent> stateTransitions   = new();
        public List<TransitionEvent> anomalyTransitions = new();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SurveyEntry survey;
    }

    public class SessionRecord
    {
        public string sessionLabel;
        public string sessionStart;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string sessionEnd;
        public List<TrialRecord> trials = new();
    }

    public class ExperimentRecord
    {
        public int    subjectID;
        public string experimentStart;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string experimentEnd;
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

    // ── ExperimentDataRecorder ────────────────────────────────────────────────────────

    public class ExperimentDataRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameStateManager     gsm;
        [SerializeField] private TrackingDataRecorder trackingdataRecorder;
        [SerializeField] private SurveyDataRecorder   surveyRecorder;

        // ── File path ──────────────────────────────────────────────────────────────

        private string OutputPath => trackingdataRecorder != null && !string.IsNullOrEmpty(trackingdataRecorder.sessionPath)
            ? Path.Combine(trackingdataRecorder.sessionPath, "ExperimentData.json")
            : null;

        // ── In-memory state ────────────────────────────────────────────────────────

        private ExperimentRecord _experiment;
        private SessionRecord    _currentSession;
        private TrialRecord      _currentTrial;
        private string           _prevGsmStateName;

        // ── ASM tracking ──────────────────────────────────────────────────────────

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
            ExperimentSequencer.OnSessionEnded    += OnSessionEnded;
            ExperimentSequencer.OnTrialBegan      += OnTrialBegan;
            ExperimentSequencer.OnTrialEnded      += OnTrialEnded;
            ExperimentSequencer.OnExperimentEnded += OnExperimentEnded;

            if (gsm != null)
                gsm.OnStateChanged += OnStateChanged;

            AnomalyStateManager.OnRegistered   += TrackAsm;
            AnomalyStateManager.OnUnregistered += UntrackAsm;
        }

        private void OnDisable()
        {
            ExperimentSequencer.OnExperimentBegan -= OnExperimentBegan;
            ExperimentSequencer.OnSessionBegan    -= OnSessionBegan;
            ExperimentSequencer.OnSessionEnded    -= OnSessionEnded;
            ExperimentSequencer.OnTrialBegan      -= OnTrialBegan;
            ExperimentSequencer.OnTrialEnded      -= OnTrialEnded;
            ExperimentSequencer.OnExperimentEnded -= OnExperimentEnded;

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
        }

        private void UntrackAsm(AnomalyStateManager asm)
        {
            if (!_trackedAsms.Remove(asm)) return;
            asm.OnAnomalyStateChanged -= OnAnomalyStateChanged;
        }

        // ── Sequencer Event Handlers ──────────────────────────────────────────────

        private void OnExperimentBegan(int subjectID)
        {
            if (OutputPath == null)
            {
                Debug.LogError("[ExperimentDataRecorder] No sessionPath. Call StartRecording() first.");
                return;
            }

            _experiment = new ExperimentRecord
            {
                subjectID       = subjectID,
                experimentStart = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
            };

            _prevGsmStateName = null;
            Debug.Log($"[ExperimentDataRecorder] Experiment began. Output: {OutputPath}");
        }

        private void OnSessionBegan(string sessionLabel)
        {
            if (_experiment == null)
            {
                Debug.LogError("[ExperimentDataRecorder] OnSessionBegan — no experiment record. Was OnExperimentBegan received?");
                return;
            }

            _currentSession = new SessionRecord
            {
                sessionLabel = sessionLabel,
                sessionStart = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
            };
            _experiment.sessions.Add(_currentSession);
            Debug.Log($"[ExperimentDataRecorder] Session began: {sessionLabel}");
        }

        private void OnSessionEnded()
        {
            if (_currentSession == null) return;
            _currentSession.sessionEnd = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff");
            _currentSession = null;
            Flush();
            Debug.Log("[ExperimentDataRecorder] Session ended.");
        }

        private void OnTrialBegan(AnomalyDefinition anomaly, string stimulus)
        {
            if (_currentSession == null)
            {
                Debug.LogError("[ExperimentDataRecorder] OnTrialBegan — no active session.");
                return;
            }

            _currentTrial = new TrialRecord
            {
                trialNumber = _currentSession.trials.Count + 1,
                stimulus    = stimulus,
            };

            // trial_start was entered before OnTrialBegan fired — record it as the opening entry
            if (gsm != null)
            {
                _currentTrial.stateTransitions.Add(new TransitionEvent(
                    from: _prevGsmStateName ?? "—",
                    to:   gsm.StateName(gsm.CurrentStateIndex),
                    time: DateTime.Now));
            }

            _currentSession.trials.Add(_currentTrial);
            Debug.Log($"[ExperimentDataRecorder] Trial {_currentTrial.trialNumber} began in '{_currentSession.sessionLabel}'. Stimulus: {stimulus}");
        }

        private void OnTrialEnded()
        {
            if (_currentTrial == null)
            {
                Debug.LogError("[ExperimentDataRecorder] OnTrialEnded — no active trial.");
                return;
            }

            _currentTrial = null;
            Flush();
        }

        private void OnExperimentEnded()
        {
            if (_experiment == null) return;
            _experiment.experimentEnd = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff");
            Flush();
            Debug.Log("[ExperimentDataRecorder] Experiment ended.");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// True when the survey has valid data that can be captured.
        /// ExperimentController checks this before allowing Advance().
        /// Returns true if no surveyRecorder is assigned (survey is optional).
        /// </summary>
        public bool IsSurveyReady =>
            surveyRecorder == null || surveyRecorder.IsReady;

        /// <summary>
        /// Collects survey responses from SurveyDataRecorder and writes them to the current trial.
        /// Call this immediately before Advance().
        /// </summary>
        public void CaptureSurvey()
        {
            if (surveyRecorder == null || _currentTrial == null) return;

            var data = surveyRecorder.Collect();
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

        /// <summary>Directly submit survey data without going through SurveyDataRecorder.</summary>
        public void SubmitSurvey(SurveyData data)
        {
            if (_currentTrial == null)
            {
                Debug.LogError("[ExperimentDataRecorder] SubmitSurvey — no active trial.");
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

        // ── GSM Event Handler ─────────────────────────────────────────────────────

        private void OnStateChanged(int fromIndex, int toIndex, DateTime timestamp)
        {
            // Always track previous state name for the opening trial snapshot
            _prevGsmStateName = gsm.StateName(fromIndex);

            // Only record transitions that occur during an active trial
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
                Debug.LogError("[ExperimentDataRecorder] Cannot flush — no output path.");
                return;
            }
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting        = Formatting.Indented,
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(_experiment, settings));
            }
            catch (Exception e)
            {
                Debug.LogError($"[ExperimentDataRecorder] Failed to write: {e.Message}");
            }
        }
    }
}