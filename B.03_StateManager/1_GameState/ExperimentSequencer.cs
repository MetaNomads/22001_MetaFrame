using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    
    
    // ── Stimuli Sequence ────────────────────────────────────────────────────────────────
    // Each entry is an int:  0 = N (noise),  1 = A1,  2 = A2, ... n = An
    
    [System.Serializable]
    public class StimuliSequence
    {
        public List<int> stimulus = new();
    
        public string[] ToStringArray()
        {
            return stimulus.Select(t => t == 0 ? "N" : $"A{t}").ToArray();
        }
    
        public Dictionary<string, int> GetStats(int anomalyCount)
        {
            var counts = new Dictionary<string, int> { ["N"] = 0 };
            for (int i = 1; i <= anomalyCount; i++)
                counts[$"A{i}"] = 0;
            foreach (var t in stimulus)
            {
                string key = t == 0 ? "N" : $"A{t}";
                if (counts.ContainsKey(key)) counts[key]++;
            }
            return counts;
        }
    }
    
    // ── Session Group ─────────────────────────────────────────────────────────────
    
    [System.Serializable]
    public class SessionGroup
    {
        public string        groupName          = "Session";
        public AnomalyDefinition normalDefinition;
        public List<Object>  anomalyObjects = new();
    
        public AnomalyDefinition GetAnomaly(int index)
        {
            if (index < 0 || index >= anomalyObjects.Count) return null;
            return anomalyObjects[index] as AnomalyDefinition;
        }
    
        public List<AnomalyDefinition> Anomalies =>
            anomalyObjects.Select(o => o as AnomalyDefinition).ToList();
    }
    
    // ── Resolved Sequence ──────────────────────────────────────────────────────────
    
    [System.Serializable]
    public class ResolvedSequence
    {
        public string       sessionLabel;
        public SessionGroup group;
        public int          listIndex;
        public AnomalyDefinition[] definitions; // one per trial — null slots mean normal (fallback)
    
        public int TrialCount => definitions?.Length ?? 0;
    
        public AnomalyDefinition AnomalyAt(int trialIndex) => definitions[trialIndex];
    }
    
    // ── ExperimentSequencer ───────────────────────────────────────────────────────
    
    public class ExperimentSequencer : MonoBehaviour
    {
        public static ExperimentSequencer instance { get; private set; }

        [Min(1)] public int anomalyCount = 3;
    
        public int subjectID = 1;
    
        [Header("Tutorial Session")]
        public SessionGroup              tutorialGroup   = new() { groupName = "Tutorial" };
        public List<AnomalyDefinition>   tutorialStimuli = new();
    
        [Header("Session Groups")]
        public List<SessionGroup> sessionGroups = new();
    
        [Header("Stimuli Sequences")]
        public List<StimuliSequence> stimuliSequences = new();
    
        [HideInInspector] public List<ResolvedSequence> resolvedSequences = new();
    
        // ── References ─────────────────────────────────────────────────────────────
    
        [SerializeField] private GameStateManager gsm;
        [SerializeField] private StateDefinition  stateExperimentStart;
        [SerializeField] private StateDefinition  stateSessionStart;
        [SerializeField] private StateDefinition  stateSessionEnd;
        [SerializeField] private StateDefinition  stateTrialStart;
        [SerializeField] private StateDefinition  stateTrialEnd;
        [SerializeField] private StateDefinition  stateIdle;
        [SerializeField] private StateDefinition  stateExperimentEnd;
    
        public GameStateManager  GSM                  => gsm;
        public StateDefinition   StateExperimentStart => stateExperimentStart;
        public StateDefinition   StateSessionStart    => stateSessionStart;
        public StateDefinition   StateSessionEnd      => stateSessionEnd;
        public StateDefinition   StateTrialStart      => stateTrialStart;
        public StateDefinition   StateTrialEnd        => stateTrialEnd;
        public StateDefinition   StateIdle            => stateIdle;
        public StateDefinition   StateExperimentEnd   => stateExperimentEnd;
    
        // ── Runtime State ──────────────────────────────────────────────────────────
    
        private int _sessionIndex = 0;
        private int _trialIndex   = 0;
    
        public int CurrentSessionIndex => _sessionIndex;
        public int CurrentTrialIndex   => _trialIndex;
    
        public ResolvedSequence CurrentSession =>
            resolvedSequences != null && _sessionIndex < resolvedSequences.Count
                ? resolvedSequences[_sessionIndex] : null;
    
        // ── Static Events ──────────────────────────────────────────────────────────
    
        /// <summary>Fires once when the experiment is initialised.</summary>
        public static event System.Action<int>              OnExperimentBegan;
    
        /// <summary>Fires at the start of each session.</summary>
        public static event System.Action<string>           OnSessionBegan;

        /// <summary>Fires at the end of each session.</summary>
        public static event System.Action                   OnSessionEnded;

        /// <summary>Fires at the start of each trial. Carries the active anomaly (null = NORMAL) and the stimulus label (e.g. "Normal", "TMP_MET_001").</summary>
        public static event System.Action<AnomalyDefinition, string> OnTrialBegan;
    
        /// <summary>Fires at the end of each trial.</summary>
        public static event System.Action                   OnTrialEnded;

        /// <summary>Fires when the experiment is complete.</summary>
        public static event System.Action                   OnExperimentEnded;

        // ── Advance ────────────────────────────────────────────────────────────────
    
        /// <summary>
        /// Drives the experiment forward one beat.
        /// Returns true if the first GSM transition succeeded — i.e. the step
        /// was accepted and the experiment state has moved forward.
        /// Returns false if the transition was blocked by GSM allowedFrom rules,
        /// in which case no state has changed and callers should not record or clear.
        /// </summary>
        public bool Advance()
        {
            var current = gsm.CurrentStateDefinition;
    
            if (current == stateExperimentStart || current == stateIdle)
            {
                // → session_start → trial_start
                if (!gsm.RequestTransition(stateSessionStart)) return false;
                OnSessionBegan?.Invoke(CurrentSession.sessionLabel);
                // StartTrial FIRST so _currentTrial exists before the GSM transition
                // fires OnStateChanged → EvaluateTriggers → any TriggerAnomaly calls.
                StartTrial();
                if (!gsm.RequestTransition(stateTrialStart)) return false;
            }
            else
            {
                // Mid-trial → trial_end. This is the gate: if blocked, nothing moves.
                if (!gsm.RequestTransition(stateTrialEnd)) return false;
                AnomalyStateManager.BroadcastTrialEnded();
                OnTrialEnded?.Invoke();
                _trialIndex++;

                if (_trialIndex < CurrentSession.TrialCount)
                {
                    // More trials in this session → trial_start
                    // StartTrial FIRST so _currentTrial exists before the GSM transition
                    // fires OnStateChanged → EvaluateTriggers → any TriggerAnomaly calls.
                    StartTrial();
                    if (!gsm.RequestTransition(stateTrialStart)) return false;
                }
                else
                {
                    // Session complete → session_end
                    if (!gsm.RequestTransition(stateSessionEnd)) return false;
                    OnSessionEnded?.Invoke();
                    _sessionIndex++;
                    _trialIndex = 0;

                    if (_sessionIndex < resolvedSequences.Count)
                    {
                        // More sessions → idle
                        if (!gsm.RequestTransition(stateIdle))
                        {
                            _sessionIndex--;
                            Debug.LogError("[Sequencer] Failed to transition to idle — session index rolled back.");
                            return false;
                        }
                    }
                    else
                    {
                        // All sessions done → experiment_end
                        if (!gsm.RequestTransition(stateExperimentEnd))
                        {
                            _sessionIndex--;
                            Debug.LogError("[Sequencer] Failed to transition to experiment_end — session index rolled back.");
                            return false;
                        }
                        else
                        {
                            OnExperimentEnded?.Invoke();
                        }
                    }
                }
            }

            return true;
        }
    
        // ── Init ───────────────────────────────────────────────────────────────────
    
        /// <summary>
        /// Call once before the experiment starts (e.g. on a setup screen).
        /// Resolves the sequence and broadcasts to any listeners (e.g. SurveyDataRecorder).
        /// </summary>
        public void InitExperiment()
        {
            if (resolvedSequences == null || resolvedSequences.Count == 0)
                LoadSequence();
    
            _sessionIndex = 0;
            _trialIndex   = 0;
    
            OnExperimentBegan?.Invoke(subjectID);
            Debug.Log("[Sequencer] Experiment initialised. Call Advance() from game_start to begin.");
        }
    
        // ── Internal ───────────────────────────────────────────────────────────────
    
        private void StartTrial()
        {
            AnomalyDefinition anomaly  = CurrentSession.AnomalyAt(_trialIndex);
            string            stimulus = anomaly != null ? anomaly.id : "Normal";
            AnomalyStateManager.BroadcastTrialBegan(anomaly);
            OnTrialBegan?.Invoke(anomaly, stimulus);
    
            Debug.Log($"[Sequencer] Session '{CurrentSession.sessionLabel}' " +
                      $"| Trial {_trialIndex + 1}/{CurrentSession.TrialCount} " +
                      $"| Stimulus: {stimulus}");
        }
    
        public void LoadSequence()
        {
            resolvedSequences.Clear();
    
            // ── Tutorial — direct definition list ──────────────────────────────────
            resolvedSequences.Add(new ResolvedSequence
            {
                sessionLabel = tutorialGroup.groupName,
                group        = tutorialGroup,
                listIndex    = 0,
                definitions  = tutorialStimuli.ToArray(),
            });
    
            if (subjectID < 1 ||
                sessionGroups    == null || sessionGroups.Count    == 0 ||
                stimuliSequences == null || stimuliSequences.Count == 0)
            {
                Debug.Log(BuildSummary());
                return;
            }
    
            int n          = sessionGroups.Count;
            int orderShift = (subjectID - 1) % n;
    
            for (int s = 0; s < n; s++)
            {
                int groupIdx = (s + orderShift) % n;
                int listIdx  = ((subjectID - 1) + s) % stimuliSequences.Count;
    
                var   group    = sessionGroups[groupIdx];
                var   intList  = stimuliSequences[listIdx].stimulus;
                var   defs     = new AnomalyDefinition[intList.Count];
    
                for (int t = 0; t < intList.Count; t++)
                    defs[t] = intList[t] == 0
                        ? group.normalDefinition
                        : group.GetAnomaly(intList[t] - 1);
    
                resolvedSequences.Add(new ResolvedSequence
                {
                    sessionLabel = group.groupName,
                    group        = group,
                    listIndex    = listIdx + 1,
                    definitions  = defs,
                });
            }
    
            Debug.Log(BuildSummary());
        }
    
        /// <summary>Returns the tutorial session (always resolvedSequences[0]).</summary>
        public ResolvedSequence GetTutorialSession()
        {
            if (resolvedSequences == null || resolvedSequences.Count == 0)
            {
                Debug.LogWarning("[Sequencer] No sessions found. Call LoadSequence() first.");
                return null;
            }
            return resolvedSequences[0];
        }
    
        /// <summary>
        /// Returns a regular session by 1-based number.
        /// resolvedSequences[0] is always the tutorial, so Session 1 lives at index 1.
        /// </summary>
        public ResolvedSequence GetSession(int sessionNumber)
        {
            // offset by 1 to skip the tutorial slot at index 0
            int i = sessionNumber;
            if (resolvedSequences == null || i < 1 || i >= resolvedSequences.Count)
            {
                Debug.LogWarning("[Sequencer] Session not found. Call LoadSequence() first.");
                return null;
            }
            return resolvedSequences[i];
        }
    
        private string BuildSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Sequencer] Subject S{subjectID:D2} — {resolvedSequences.Count} sessions (incl. tutorial) | {anomalyCount} anomalies/session");
            foreach (var s in resolvedSequences)
            {
                string ids = string.Join(", ", s.group?.Anomalies
                    .Where(a => a != null).Select(a => a.id) ?? Enumerable.Empty<string>());
                string listLabel = s.listIndex == 0 ? "Tutorial List" : $"List {s.listIndex}";
                sb.AppendLine($"  {s.sessionLabel}: [{s.group?.groupName}]  {ids}  |  {listLabel} ({s.definitions?.Length ?? 0} trials)");
            }
            return sb.ToString();
        }
    
        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            LoadSequence();
            InitExperiment();
            gsm.ForceState(stateExperimentStart);
            Debug.Log("[Sequencer] Ready at experiment_start.");
        }
    }

} // namespace MetaFrame.State

// ── Custom Editor ──────────────────────────────────────────────────────────────

#if UNITY_EDITOR
namespace MetaFrame.State
{
    [CustomEditor(typeof(ExperimentSequencer))]
    public class ExperimentSequencerEditor : Editor
    {
        private bool _tutorialFoldout    = false;
        private bool _groupsFoldout      = false;
        private bool _listsFoldout       = false;
        private bool _stateConfigFoldout = false;
    
        private List<bool> _groupExpanded = new();
        private List<bool> _listExpanded  = new();
    
        private SerializedProperty _anomalyCount;
        private SerializedProperty _subjectID;
        private SerializedProperty _tutorialGroup;
        private SerializedProperty _tutorialStimuli;
        private SerializedProperty _sessionGroups;
        private SerializedProperty _stimuliSequences;
        private SerializedProperty _gsm;
        private SerializedProperty _stateExperimentStart;
        private SerializedProperty _stateSessionStart;
        private SerializedProperty _stateSessionEnd;
        private SerializedProperty _stateTrialStart;
        private SerializedProperty _stateTrialEnd;
        private SerializedProperty _stateIdle;
        private SerializedProperty _stateExperimentEnd;
    
        private void OnEnable()
        {
            _anomalyCount            = serializedObject.FindProperty("anomalyCount");
            _subjectID               = serializedObject.FindProperty("subjectID");
            _tutorialGroup           = serializedObject.FindProperty("tutorialGroup");
            _tutorialStimuli         = serializedObject.FindProperty("tutorialStimuli");
            _sessionGroups           = serializedObject.FindProperty("sessionGroups");
            _stimuliSequences        = serializedObject.FindProperty("stimuliSequences");
            _gsm                     = serializedObject.FindProperty("gsm");
            _stateExperimentStart    = serializedObject.FindProperty("stateExperimentStart");
            _stateSessionStart       = serializedObject.FindProperty("stateSessionStart");
            _stateSessionEnd         = serializedObject.FindProperty("stateSessionEnd");
            _stateTrialStart         = serializedObject.FindProperty("stateTrialStart");
            _stateTrialEnd           = serializedObject.FindProperty("stateTrialEnd");
            _stateIdle               = serializedObject.FindProperty("stateIdle");
            _stateExperimentEnd      = serializedObject.FindProperty("stateExperimentEnd");
        }
    
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var seq = (ExperimentSequencer)target;
    
            // Guard: Odin Inspector can invoke OnInspectorGUI before OnEnable
            if (_anomalyCount == null) OnEnable();
    
            // ── Subject ID ──────────────────────────────────────────────
            int ac = Mathf.Max(1, _anomalyCount.intValue);
            EditorGUILayout.PropertyField(_subjectID, new GUIContent("Subject ID"));
    
            // ── Resolved Sequence — computed inline, no LoadSequence() ──
            int n         = _sessionGroups.arraySize;
            int listCount = _stimuliSequences.arraySize;
            int sid       = _subjectID.intValue;
    
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Resolved Sequence", EditorStyles.miniBoldLabel);
            GUI.enabled = false;
    
            // ── Tutorial row (always first) ──────────────────────────────
            {
                string tutGroupName      = _tutorialGroup.FindPropertyRelative("groupName").stringValue;
                int    tutStimulusCount  = _tutorialStimuli.arraySize;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.TextField($"{tutGroupName}   |   Tutorial   |   {tutStimulusCount} trials");
                EditorGUILayout.EndVertical();
            }
    
            // ── Regular session rows ────────────────────────────────────
            if (n > 0 && listCount > 0 && sid >= 1)
            {
                int orderShift = (sid - 1) % n;
    
                for (int s = 0; s < n; s++)
                {
                    int groupIdx = (s + orderShift) % n;
                    int listIdx  = ((sid - 1) + s) % listCount;
    
                    var groupProp = _sessionGroups.GetArrayElementAtIndex(groupIdx);
                    string groupName = groupProp.FindPropertyRelative("groupName").stringValue;
    
                    var objsProp = groupProp.FindPropertyRelative("anomalyObjects");
                    var ids = new List<string>();
                    for (int a = 0; a < objsProp.arraySize; a++)
                    {
                        var def = objsProp.GetArrayElementAtIndex(a).objectReferenceValue as AnomalyDefinition;
                        if (def != null) ids.Add(def.id);
                    }
    
                    int stimulusCount = 0;
                    if (listIdx < seq.stimuliSequences?.Count)
                        stimulusCount = seq.stimuliSequences[listIdx].stimulus.Count;
    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.TextField($"{groupName}   |   List {listIdx + 1}   |   {stimulusCount} stimulus");
                    if (ids.Count > 0)
                        EditorGUILayout.TextField(string.Join(", ", ids));
                    EditorGUILayout.EndVertical();
                }
            }
            else if (n > 0 && listCount > 0)
            {
                EditorGUILayout.HelpBox("Enter a valid Subject ID to see the resolved sequence.", MessageType.Info);
            }
    
            GUI.enabled = true;
    
            // ── State Configuration ─────────────────────────────────────
            EditorGUILayout.Space(10);
            _stateConfigFoldout = EditorGUILayout.Foldout(
                _stateConfigFoldout, "State Configuration",
                true, EditorStyles.foldoutHeader);
    
            if (_stateConfigFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_gsm,                  new GUIContent("Game State Manager"));
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("State References", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(_stateExperimentStart, new GUIContent("Experiment Start"));
                EditorGUILayout.PropertyField(_stateSessionStart,    new GUIContent("Session Start"));
                EditorGUILayout.PropertyField(_stateSessionEnd,      new GUIContent("Session End"));
                EditorGUILayout.PropertyField(_stateTrialStart,      new GUIContent("Trial Start"));
                EditorGUILayout.PropertyField(_stateTrialEnd,        new GUIContent("Trial End"));
                EditorGUILayout.PropertyField(_stateIdle,            new GUIContent("Idle"));
                EditorGUILayout.PropertyField(_stateExperimentEnd,   new GUIContent("Experiment End"));
                EditorGUI.indentLevel--;
            }
    
            // ── Tutorial Session ────────────────────────────────────────
            EditorGUILayout.Space(10);
            _tutorialFoldout = EditorGUILayout.Foldout(
                _tutorialFoldout, "Tutorial Session",
                true, EditorStyles.foldoutHeader);
    
            if (_tutorialFoldout)
            {
                EditorGUI.indentLevel++;

                // ── Stimuli — direct AnomalyDefinition list ─────────────
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Stimuli  ({_tutorialStimuli.arraySize})", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("+", GUILayout.Width(26))) _tutorialStimuli.arraySize++;
                if (_tutorialStimuli.arraySize > 0 && GUILayout.Button("−", GUILayout.Width(26))) _tutorialStimuli.arraySize--;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                for (int t = 0; t < _tutorialStimuli.arraySize; t++)
                {
                    var elemProp = _tutorialStimuli.GetArrayElementAtIndex(t);
                    var current  = elemProp.objectReferenceValue as AnomalyDefinition;
                    string hint  = current != null ? $"  [{current.id}]" : "  [Normal]";

                    Rect rect = EditorGUILayout.GetControlRect();
                    EditorGUI.BeginChangeCheck();
                    var picked = (AnomalyDefinition)EditorGUI.ObjectField(
                        rect, new GUIContent($"{t + 1:D2}{hint}"),
                        current, typeof(AnomalyDefinition), false);
                    if (EditorGUI.EndChangeCheck())
                        elemProp.objectReferenceValue = picked;
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel--;
            }
    
            // ── Session Groups ──────────────────────────────────────────
            EditorGUILayout.Space(10);
            _groupsFoldout = EditorGUILayout.Foldout(
                _groupsFoldout, $"Session Groups  ({_sessionGroups.arraySize})",
                true, EditorStyles.foldoutHeader);
    
            if (_groupsFoldout)
            {
                EditorGUI.indentLevel++;
    
                // Anomaly count lives here — defines A1..An slots for all groups
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_anomalyCount, new GUIContent("Anomaly Count",
                    "Number of anomaly variants per session. Defines A1..An slots in each group."));
                bool countChanged = EditorGUI.EndChangeCheck();
                string slotPreview = "N   " + string.Join("   ", Enumerable.Range(1, ac).Select(i => $"A{i}"));
                EditorGUILayout.HelpBox($"Stimulus entry options:   {slotPreview}", MessageType.None);
                if (countChanged)
                {
                    for (int g = 0; g < _sessionGroups.arraySize; g++)
                        _sessionGroups.GetArrayElementAtIndex(g)
                                      .FindPropertyRelative("anomalyObjects").arraySize = ac;
                }
                EditorGUILayout.Space(4);
    
                while (_groupExpanded.Count < _sessionGroups.arraySize) _groupExpanded.Add(true);
                while (_groupExpanded.Count > _sessionGroups.arraySize) _groupExpanded.RemoveAt(_groupExpanded.Count - 1);
    
                for (int g = 0; g < _sessionGroups.arraySize; g++)
                {
                    var groupProp = _sessionGroups.GetArrayElementAtIndex(g);
                    var nameProp  = groupProp.FindPropertyRelative("groupName");
                    var objsProp  = groupProp.FindPropertyRelative("anomalyObjects");
    
                    if (objsProp.arraySize != ac) objsProp.arraySize = ac;
    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    
                    EditorGUILayout.BeginHorizontal();
                    _groupExpanded[g] = EditorGUILayout.Foldout(
                        _groupExpanded[g], $"Session {g + 1}  —  {nameProp.stringValue}", true);
                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _sessionGroups.DeleteArrayElementAtIndex(g);
                        _groupExpanded.RemoveAt(g);
                        serializedObject.ApplyModifiedProperties();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
    
                    if (_groupExpanded[g])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        string newName = EditorGUILayout.TextField(new GUIContent("Session Name"), nameProp.stringValue);
                        if (EditorGUI.EndChangeCheck())
                            nameProp.stringValue = newName;

                        // Normal definition
                        var normalProp   = groupProp.FindPropertyRelative("normalDefinition");
                        var normalDef    = normalProp.objectReferenceValue as AnomalyDefinition;
                        string normalHint = normalDef != null ? $"  [{normalDef.id}]" : "  (none)";
                        Rect normalRect  = EditorGUILayout.GetControlRect();
                        EditorGUI.BeginChangeCheck();
                        var pickedNormal = (AnomalyDefinition)EditorGUI.ObjectField(
                            normalRect, new GUIContent($"Normal{normalHint}"),
                            normalDef, typeof(AnomalyDefinition), false);
                        if (EditorGUI.EndChangeCheck())
                            normalProp.objectReferenceValue = pickedNormal;

                        EditorGUILayout.LabelField("Anomaly Definitions", EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
    
                        for (int a = 0; a < ac; a++)
                        {
                            var elemProp = objsProp.GetArrayElementAtIndex(a);
                            var current  = elemProp.objectReferenceValue as AnomalyDefinition;
                            string hint  = current != null ? $"  [{current.id}]" : "";
    
                            Rect rect = EditorGUILayout.GetControlRect();
                            EditorGUI.BeginChangeCheck();
                            var picked = (AnomalyDefinition)EditorGUI.ObjectField(
                                rect,
                                new GUIContent($"A{a + 1}{hint}"),
                                current,
                                typeof(AnomalyDefinition),
                                false);
                            if (EditorGUI.EndChangeCheck())
                                elemProp.objectReferenceValue = picked;
                        }
    
                        EditorGUI.indentLevel--;
                        EditorGUI.indentLevel--;
                    }
    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
    
                if (GUILayout.Button("+ Add Session Group", GUILayout.Height(24)))
                {
                    _sessionGroups.arraySize++;
                    var ng = _sessionGroups.GetArrayElementAtIndex(_sessionGroups.arraySize - 1);
                    ng.FindPropertyRelative("groupName").stringValue      = $"Session {_sessionGroups.arraySize}";
                    ng.FindPropertyRelative("anomalyObjects").arraySize   = ac;
                    _groupExpanded.Add(true);
                }
    
                EditorGUI.indentLevel--;
            }
    
            // ── Stimuli Sequences ─────────────────────────────────────────────
            EditorGUILayout.Space(10);
            _listsFoldout = EditorGUILayout.Foldout(
                _listsFoldout, $"Stimuli Sequences  ({_stimuliSequences.arraySize})",
                true, EditorStyles.foldoutHeader);
    
            if (_listsFoldout)
            {
                EditorGUI.indentLevel++;
    
                string[] popupOptions = new string[ac + 1];
                popupOptions[0] = "N";
                for (int i = 0; i < ac; i++) popupOptions[i + 1] = $"A{i + 1}";
    
                while (_listExpanded.Count < _stimuliSequences.arraySize) _listExpanded.Add(true);
                while (_listExpanded.Count > _stimuliSequences.arraySize) _listExpanded.RemoveAt(_listExpanded.Count - 1);
    
                for (int l = 0; l < _stimuliSequences.arraySize; l++)
                {
                    var listProp     = _stimuliSequences.GetArrayElementAtIndex(l);
                    var stimulusProp = listProp.FindPropertyRelative("stimulus");
    
                    StimuliSequence tl = (seq.stimuliSequences != null && l < seq.stimuliSequences.Count) ? seq.stimuliSequences[l] : null;
                    string stats = BuildStats(tl, ac);
    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    
                    EditorGUILayout.BeginHorizontal();
                    _listExpanded[l] = EditorGUILayout.Foldout(
                        _listExpanded[l], $"Sequence {l + 1}   —   {stats}", true);
                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _stimuliSequences.DeleteArrayElementAtIndex(l);
                        _listExpanded.RemoveAt(l);
                        serializedObject.ApplyModifiedProperties();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
    
                    if (_listExpanded[l])
                    {
                        EditorGUI.indentLevel++;
    
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Stimulus  ({stimulusProp.arraySize})", GUILayout.Width(110));
                        if (GUILayout.Button("+", GUILayout.Width(26))) stimulusProp.arraySize++;
                        if (stimulusProp.arraySize > 0 && GUILayout.Button("−", GUILayout.Width(26))) stimulusProp.arraySize--;
                        EditorGUILayout.EndHorizontal();
    
                        for (int t = 0; t < stimulusProp.arraySize; t++)
                        {
                            var entryProp = stimulusProp.GetArrayElementAtIndex(t);
                            int current   = Mathf.Clamp(entryProp.intValue, 0, ac);
    
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"  {t + 1:D2}", GUILayout.Width(36));
                            int chosen = EditorGUILayout.Popup(current, popupOptions);
                            if (chosen != current) entryProp.intValue = chosen;
                            EditorGUILayout.EndHorizontal();
                        }
    
                        EditorGUI.indentLevel--;
                    }
    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
    
                if (GUILayout.Button("+ Add Stimuli Sequence", GUILayout.Height(24)))
                    _stimuliSequences.arraySize++;
    
                EditorGUI.indentLevel--;
            }
    
            serializedObject.ApplyModifiedProperties();
        }
    
        private static string BuildStats(StimuliSequence tl, int ac)
        {
            if (tl == null || tl.stimulus.Count == 0) return "empty";
            var s = tl.GetStats(ac);
            return $"{tl.stimulus.Count} stimulus  |  " +
                   string.Join("  ", s.Select(kv => $"{kv.Key}×{kv.Value}"));
        }
    
        private static void SectionLabel(string text)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }
    }
} // namespace MetaFrame.State
#endif