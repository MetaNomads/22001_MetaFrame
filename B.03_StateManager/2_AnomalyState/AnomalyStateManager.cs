using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using static MetaFrame.State.GameStateManager;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    // ── Anomaly State ─────────────────────────────────────────────────────────────

    [System.Flags]
    public enum AnomalyState
    {
        Disabled  = 1 << 0,
        Active    = 1 << 1,
        Triggered = 1 << 2,
        Completed = 1 << 3,
    }

    // ── Condition Mode ────────────────────────────────────────────────────────────

    public enum ConditionMode { Disabled, AND, OR }

    // ── Script Trigger Entry ──────────────────────────────────────────────────────

    /// <summary>
    /// One slot in a binding's Custom Trigger list.
    /// Each entry has its own AND / OR / Disabled mode.
    /// </summary>
    [System.Serializable]
    public class ScriptTriggerEntry
    {
        [Tooltip("Disabled — ignored.\nAND — this trigger must pass.\nOR  — this trigger passing alone is enough.")]
        public ConditionMode mode = ConditionMode.AND;

        [Tooltip("Drag any MonoBehaviour that implements IAnomalyTrigger.")]
        public MonoBehaviour script;
    }

    // ── Event Binding ─────────────────────────────────────────────────────────────

    [System.Serializable]
    public class AnomalyEventBinding
    {
        [Tooltip("Optional label. Auto-generated from active conditions if empty.")]
        public string bindingName = "";

        [Tooltip("Disabled — ignored.\nAND — must pass.\nOR  — passing alone fires the binding.")]
        public ConditionMode levelStateMode = ConditionMode.AND;
        public LevelState levelStates;

        [Tooltip("Disabled — ignored.\nAND — must pass.\nOR  — passing alone fires the binding.")]
        public ConditionMode anomalyStateMode = ConditionMode.AND;
        public AnomalyState anomalyStates;

        [Tooltip("Disabled — ignored.\nAND — trigger group must pass.\nOR  — trigger group passing alone fires the binding.")]
        public ConditionMode triggerMode = ConditionMode.Disabled;

        [Tooltip("Each trigger entry carries its own AND / OR / Disabled mode.")]
        public List<ScriptTriggerEntry> triggerScripts = new();

        public UnityEvent onEnter;
        public UnityEvent onExit;

        // ── Evaluation ────────────────────────────────────────────

        public bool Evaluate(LevelState levelState, AnomalyState anomalyState)
        {
            if (levelStateMode  == ConditionMode.Disabled &&
                anomalyStateMode == ConditionMode.Disabled &&
                triggerMode      == ConditionMode.Disabled) return true;

            bool levelResult  = (levelStates   & levelState)   != 0;
            bool anomalyResult= (anomalyStates & anomalyState) != 0;
            bool triggerResult= EvaluateTriggerGroup();

            return EvaluateGroup(
                (levelStateMode,  levelResult),
                (anomalyStateMode, anomalyResult),
                (triggerMode,     triggerResult));
        }

        private static bool EvaluateGroup(params (ConditionMode mode, bool result)[] entries)
        {
            bool orPresent   = false;
            bool orSatisfied = false;

            foreach (var (mode, result) in entries)
            {
                if (mode == ConditionMode.Disabled) continue;
                if (mode == ConditionMode.AND && !result) return false;
                if (mode == ConditionMode.OR)
                {
                    orPresent = true;
                    if (result) orSatisfied = true;
                }
            }
            return !orPresent || orSatisfied;
        }

        private bool EvaluateTriggerGroup()
        {
            if (triggerScripts == null || triggerScripts.Count == 0) return true;

            var entries = new List<(ConditionMode mode, bool result)>();
            foreach (var entry in triggerScripts)
            {
                if (entry == null || entry.mode == ConditionMode.Disabled) continue;
                bool r = entry.script is IAnomalyTrigger t && t.Evaluate();
                entries.Add((entry.mode, r));
            }

            if (entries.Count == 0) return true;
            return EvaluateGroup(entries.ToArray());
        }

        public string AutoLabel()
        {
            var andParts = new List<string>();
            var orParts  = new List<string>();

            if (levelStateMode   == ConditionMode.AND) andParts.Add(((LevelState)levelStates).ToString());
            if (levelStateMode   == ConditionMode.OR)  orParts.Add(((LevelState)levelStates).ToString());
            if (anomalyStateMode == ConditionMode.AND) andParts.Add(((AnomalyState)anomalyStates).ToString());
            if (anomalyStateMode == ConditionMode.OR)  orParts.Add(((AnomalyState)anomalyStates).ToString());

            int active = triggerScripts?.FindAll(e => e?.mode != ConditionMode.Disabled).Count ?? 0;
            if (triggerMode == ConditionMode.AND) andParts.Add($"{active} trigger(s)");
            if (triggerMode == ConditionMode.OR)  orParts.Add($"{active} trigger(s)");

            if (andParts.Count == 0 && orParts.Count == 0) return "(always fires)";

            var segments = new List<string>();
            if (andParts.Count > 0) segments.Add(string.Join(" AND ", andParts));
            if (orParts.Count  > 0) segments.Add(string.Join(" OR ",  orParts));
            return string.Join("  |  ", segments);
        }
    }

    // ── AnomalyStateManager ───────────────────────────────────────────────────────

    public class AnomalyStateManager : MonoBehaviour
    {
        [SerializeField] protected AnomalyDefinition anomalyToTrigger;

        [Header("Event Bindings")]
        [SerializeField] private List<AnomalyEventBinding> eventBindings = new();

        private AnomalyState _currentAnomalyState = AnomalyState.Disabled;
        private LevelState   _currentLevelState;
        private int          _pendingActions = 0;
        private readonly HashSet<AnomalyAction> _activeActions = new();
        private readonly HashSet<int> _enteredBindings = new(); // indices of currently-entered bindings

        public AnomalyState      CurrentAnomalyState => _currentAnomalyState;
        public LevelState        CurrentLevelState   => _currentLevelState;
        public AnomalyDefinition AnomalyToTrigger    => anomalyToTrigger;
        public int               PendingActions      => _pendingActions;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            var gsm = GameStateManager.instance;
            if (gsm == null) { Debug.LogError("[AnomalyStateManager] No GameStateManager found!"); return; }
            gsm.GameStateTrigger  += OnGameStateTrigger;
            gsm.LevelStateTrigger += OnLevelStateUpdated;
            SetupObjectAtStart();
        }

        private void OnDestroy()
        {
            var gsm = GameStateManager.instance;
            if (gsm == null) return;
            gsm.GameStateTrigger  -= OnGameStateTrigger;
            gsm.LevelStateTrigger -= OnLevelStateUpdated;
        }

        // ── GSM Callbacks ──────────────────────────────────────────

        private void OnGameStateTrigger(SessionData? sessionData, int trialNumber, TrialData trialData)
        {
            _currentLevelState = 0;
            _pendingActions    = 0;
            _activeActions.Clear();
            _enteredBindings.Clear();
            bool isSelected = trialData.anomalyDefinition != null
                           && trialData.anomalyDefinition == anomalyToTrigger;
            SetAnomalyState(isSelected ? AnomalyState.Active : AnomalyState.Disabled);
        }

        private void OnLevelStateUpdated(SessionData? sessionData, int trialNumber, LevelState levelState)
        {
            _currentLevelState = levelState;
            CheckAndFireBindings();
        }

        // ── State Machine ──────────────────────────────────────────

        private void SetAnomalyState(AnomalyState newState)
        {
            _currentAnomalyState = newState;
            CheckAndFireBindings();
        }

        private void CheckAndFireBindings()
        {
            bool anyEnteredOnActive = false;

            for (int i = 0; i < eventBindings.Count; i++)
            {
                var binding    = eventBindings[i];
                bool passed    = binding.Evaluate(_currentLevelState, _currentAnomalyState);
                bool wasEntered = _enteredBindings.Contains(i);

                if (passed && !wasEntered)
                {
                    _enteredBindings.Add(i);
                    binding.onEnter?.Invoke();

                    bool targetsActive = binding.anomalyStateMode == ConditionMode.Disabled
                                      || (binding.anomalyStates & AnomalyState.Active) != 0;
                    if (targetsActive) anyEnteredOnActive = true;
                }
                else if (!passed && wasEntered)
                {
                    _enteredBindings.Remove(i);
                    binding.onExit?.Invoke();
                }
            }

            if (anyEnteredOnActive && _currentAnomalyState == AnomalyState.Active)
            {
                _currentAnomalyState = AnomalyState.Triggered;

                if (_pendingActions <= 0)
                {
                    _pendingActions = 0;
                    _currentAnomalyState = AnomalyState.Completed;
                    OnAnomalyCompleted();
                }
            }
        }

        // ── Public API ─────────────────────────────────────────────

        public void RegisterActiveAction(AnomalyAction action)   => _activeActions.Add(action);
        public void UnregisterActiveAction(AnomalyAction action) => _activeActions.Remove(action);

        public void RegisterPendingAction()
        {
            _pendingActions++;
            Debug.Log($"[AnomalyStateManager] '{name}' pending: {_pendingActions}");
        }

        public void SignalActionComplete()
        {
            if (_currentAnomalyState != AnomalyState.Triggered) return;

            _pendingActions = Mathf.Max(0, _pendingActions - 1);
            Debug.Log($"[AnomalyStateManager] '{name}' pending: {_pendingActions}");

            if (_pendingActions > 0) return;

            _currentAnomalyState = AnomalyState.Completed;
            OnAnomalyCompleted();
            Debug.Log($"[AnomalyStateManager] '{name}' → Completed.");
        }

        public void CancelAnomaly()
        {
            // Exit all currently entered bindings
            foreach (int i in _enteredBindings)
                if (i < eventBindings.Count)
                    eventBindings[i].onExit?.Invoke();
            _enteredBindings.Clear();

            foreach (var action in _activeActions)
                action?.CancelAnomalyAction();
            _activeActions.Clear();

            OnAnomalyCancelled();
            _currentAnomalyState = AnomalyState.Disabled;
            _currentLevelState   = 0;
            _pendingActions      = 0;
        }

        public void DEBUG_InvokeEnter(int index)
        {
            if (index >= 0 && index < eventBindings.Count)
                eventBindings[index].onEnter?.Invoke();
        }

        public void DEBUG_InvokeExit(int index)
        {
            if (index >= 0 && index < eventBindings.Count)
                eventBindings[index].onExit?.Invoke();
        }

        protected virtual void SetupObjectAtStart() { }
        protected virtual void OnAnomalyCancelled() { }
        protected virtual void OnAnomalyCompleted() { }
    }

    // ── Editor ────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [CustomEditor(typeof(AnomalyStateManager), true)]
    public class AnomalyStateManagerEditor : Editor
    {
        private SerializedProperty _anomalyToTrigger;
        private SerializedProperty _eventBindings;
        private readonly List<bool> _foldouts = new();

        private void OnEnable()
        {
            _anomalyToTrigger = serializedObject.FindProperty("anomalyToTrigger");
            _eventBindings    = serializedObject.FindProperty("eventBindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_anomalyToTrigger, new GUIContent("Anomaly To Trigger"));
            DrawPropertiesExcluding(serializedObject, "m_Script", "anomalyToTrigger", "eventBindings");
            DrawBindingList();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawSectionBlock("Runtime State", "Read-only.", () =>
                {
                    var r = (AnomalyStateManager)target;
                    GUI.enabled = false;
                    EditorGUILayout.EnumPopup("Level State",    r.CurrentLevelState);
                    EditorGUILayout.EnumPopup("Anomaly State",  r.CurrentAnomalyState);
                    EditorGUILayout.IntField("Pending Actions", r.PendingActions);
                    GUI.enabled = true;
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("SignalActionComplete()")) r.SignalActionComplete();
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 0.6f, 0.4f);
                    if (GUILayout.Button("CancelAnomaly()")) r.CancelAnomaly();
                    GUI.color = prev;
                });
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Binding List ───────────────────────────────────────────

        private void DrawBindingList()
        {
            while (_foldouts.Count < _eventBindings.arraySize) _foldouts.Add(true);

            for (int i = 0; i < _eventBindings.arraySize; i++)
            {
                var entry           = _eventBindings.GetArrayElementAtIndex(i);
                var nameProp        = entry.FindPropertyRelative("bindingName");
                var levelModeProp   = entry.FindPropertyRelative("levelStateMode");
                var levelProp       = entry.FindPropertyRelative("levelStates");
                var anomalyModeProp = entry.FindPropertyRelative("anomalyStateMode");
                var anomalyProp     = entry.FindPropertyRelative("anomalyStates");
                var triggerModeProp = entry.FindPropertyRelative("triggerMode");
                var triggersProp    = entry.FindPropertyRelative("triggerScripts");
                var onEnterProp     = entry.FindPropertyRelative("onEnter");
                var onExitProp      = entry.FindPropertyRelative("onExit");

                var lMode = (ConditionMode)levelModeProp.intValue;
                var aMode = (ConditionMode)anomalyModeProp.intValue;
                var tMode = (ConditionMode)triggerModeProp.intValue;

                string autoLabel = BuildAutoLabel(lMode, levelProp, aMode, anomalyProp, tMode, triggersProp);
                string header    = string.IsNullOrWhiteSpace(nameProp.stringValue) ? autoLabel : nameProp.stringValue;

                EditorGUILayout.BeginVertical(GUI.skin.box);

                // Foldout + delete
                EditorGUILayout.BeginHorizontal();
                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i], header, true, EditorStyles.foldoutHeader);
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    _eventBindings.DeleteArrayElementAtIndex(i);
                    _foldouts.RemoveAt(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                if (!_foldouts[i]) { EditorGUILayout.EndVertical(); EditorGUILayout.Space(2); continue; }

                EditorGUI.indentLevel++;

                // Name
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Event Name");
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Preconditions", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                // Level State
                DrawModeRow(levelModeProp, "Level State", () =>
                {
                    EditorGUI.BeginChangeCheck();
                    var v = (LevelState)EditorGUILayout.EnumFlagsField(GUIContent.none, (LevelState)levelProp.intValue);
                    if (EditorGUI.EndChangeCheck()) levelProp.intValue = (int)v;
                });

                // Anomaly State
                DrawModeRow(anomalyModeProp, "Anomaly State", () =>
                {
                    EditorGUI.BeginChangeCheck();
                    var v = (AnomalyState)EditorGUILayout.EnumFlagsField(GUIContent.none, (AnomalyState)anomalyProp.intValue);
                    if (EditorGUI.EndChangeCheck()) anomalyProp.intValue = (int)v;
                });

                // Custom Trigger
                DrawModeRow(triggerModeProp, "Custom Trigger", null,
                    drawBelow: tMode != ConditionMode.Disabled
                        ? () => DrawTriggerList(triggersProp)
                        : (System.Action)null);

                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(onEnterProp, new GUIContent("On Enter"));
                EditorGUILayout.PropertyField(onExitProp,  new GUIContent("On Exit"));

                // Debug buttons
                GUI.enabled = Application.isPlaying;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = new Color(0.4f, 0.9f, 0.5f);
                if (GUILayout.Button("▶  Enter", GUILayout.Width(90)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ((AnomalyStateManager)target).DEBUG_InvokeEnter(i);
                }
                GUI.color = new Color(1f, 0.7f, 0.4f);
                if (GUILayout.Button("◀  Exit", GUILayout.Width(90)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ((AnomalyStateManager)target).DEBUG_InvokeExit(i);
                }
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(2);
            GUI.color = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("+ Add Binding"))
            {
                _eventBindings.InsertArrayElementAtIndex(_eventBindings.arraySize);
                _foldouts.Add(true);
            }
            GUI.color = Color.white;
        }

        // ── Trigger List ────────────────────────────────────────────

        private void DrawTriggerList(SerializedProperty triggersProp)
        {
            const float MODE_W    = 120f;
            const float WARN_W    = 18f;
            const float REMOVE_W  = 20f;
            const float GAP       = 4f;

            EditorGUI.indentLevel++;

            for (int c = 0; c < triggersProp.arraySize; c++)
            {
                var entryProp  = triggersProp.GetArrayElementAtIndex(c);
                var modeProp   = entryProp.FindPropertyRelative("mode");
                var scriptProp = entryProp.FindPropertyRelative("script");
                var mode       = (ConditionMode)modeProp.intValue;
                var mb         = scriptProp.objectReferenceValue as MonoBehaviour;
                bool invalid   = mb != null && mb is not IAnomalyTrigger;

                // Use GetControlRect so we control the exact width
                float totalW  = EditorGUIUtility.currentViewWidth
                              - EditorGUI.indentLevel * 15f
                              - 6f; // box padding
                float warnW   = invalid ? WARN_W + GAP : 0f;
                float fieldW  = totalW - MODE_W - GAP - warnW - REMOVE_W - GAP;

                Rect rowRect  = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                float x       = rowRect.x;
                float y       = rowRect.y;
                float h       = rowRect.height;

                // Mode dropdown
                var modeRect = new Rect(x, y, MODE_W, h);
                GUI.color = ModeColour(mode);
                EditorGUI.BeginChangeCheck();
                var newMode = (ConditionMode)EditorGUI.EnumPopup(modeRect, GUIContent.none, mode);
                if (EditorGUI.EndChangeCheck()) modeProp.intValue = (int)newMode;
                GUI.color = Color.white;
                x += MODE_W + GAP;

                // Script object field
                GUI.enabled = mode != ConditionMode.Disabled;
                if (invalid) GUI.color = new Color(1f, 0.75f, 0.4f);
                EditorGUI.BeginChangeCheck();
                var newObj = EditorGUI.ObjectField(
                    new Rect(x, y, fieldW, h),
                    GUIContent.none, scriptProp.objectReferenceValue, typeof(MonoBehaviour), true);
                if (EditorGUI.EndChangeCheck()) scriptProp.objectReferenceValue = newObj;
                GUI.color   = Color.white;
                GUI.enabled = true;
                x += fieldW + GAP;

                // Warning icon
                if (invalid)
                {
                    EditorGUI.LabelField(new Rect(x, y, WARN_W, h),
                        new GUIContent("⚠", "Must implement IAnomalyTrigger"));
                    x += WARN_W + GAP;
                }

                // Remove button
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(x, y, REMOVE_W, h), "−"))
                {
                    triggersProp.DeleteArrayElementAtIndex(c);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(2);

            // Neutral "Add Trigger" button — small, left-aligned, no colour
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Trigger", GUILayout.Width(120)))
                triggersProp.InsertArrayElementAtIndex(triggersProp.arraySize);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        // ── Mode Row ───────────────────────────────────────────────

        private void DrawModeRow(
            SerializedProperty modeProp,
            string             label,
            System.Action      drawField,
            System.Action      drawBelow = null)
        {
            var mode = (ConditionMode)modeProp.intValue;

            EditorGUILayout.BeginHorizontal();

            GUI.color = ModeColour(mode);
            EditorGUI.BeginChangeCheck();
            var newMode = (ConditionMode)EditorGUILayout.EnumPopup(GUIContent.none, mode, GUILayout.Width(90));
            if (EditorGUI.EndChangeCheck()) modeProp.intValue = (int)newMode;
            GUI.color = Color.white;

            var labelStyle = new GUIStyle(EditorStyles.label);
            if (mode == ConditionMode.Disabled) labelStyle.normal.textColor = Color.gray;
            EditorGUILayout.LabelField(label, labelStyle,
                GUILayout.Width(EditorGUIUtility.labelWidth - 76));

            if (drawField != null)
            {
                GUI.enabled = mode != ConditionMode.Disabled;
                drawField();
                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();

            if (mode != ConditionMode.Disabled && drawBelow != null)
            {
                EditorGUI.indentLevel++;
                drawBelow();
                EditorGUI.indentLevel--;
            }
        }

        // ── Helpers ────────────────────────────────────────────────

        private static Color ModeColour(ConditionMode m) => m switch
        {
            ConditionMode.AND => new Color(0.55f, 0.85f, 0.55f),
            ConditionMode.OR  => new Color(1.00f, 0.82f, 0.40f),
            _                 => new Color(0.55f, 0.55f, 0.55f),
        };

        private static string BuildAutoLabel(
            ConditionMode lMode, SerializedProperty levelProp,
            ConditionMode aMode, SerializedProperty anomalyProp,
            ConditionMode tMode, SerializedProperty triggersProp)
        {
            var andParts = new List<string>();
            var orParts  = new List<string>();

            if (lMode == ConditionMode.AND) andParts.Add(((LevelState)levelProp.intValue).ToString());
            if (lMode == ConditionMode.OR)  orParts.Add(((LevelState)levelProp.intValue).ToString());
            if (aMode == ConditionMode.AND) andParts.Add(((AnomalyState)anomalyProp.intValue).ToString());
            if (aMode == ConditionMode.OR)  orParts.Add(((AnomalyState)anomalyProp.intValue).ToString());

            int active = 0;
            for (int s = 0; s < triggersProp.arraySize; s++)
            {
                var m = (ConditionMode)triggersProp.GetArrayElementAtIndex(s)
                            .FindPropertyRelative("mode").intValue;
                if (m != ConditionMode.Disabled) active++;
            }
            if (tMode == ConditionMode.AND) andParts.Add($"{active} trigger(s)");
            if (tMode == ConditionMode.OR)  orParts.Add($"{active} trigger(s)");

            if (andParts.Count == 0 && orParts.Count == 0) return "(always fires)";

            var segments = new List<string>();
            if (andParts.Count > 0) segments.Add(string.Join(" AND ", andParts));
            if (orParts.Count  > 0) segments.Add(string.Join(" OR ",  orParts));
            return string.Join("  |  ", segments);
        }

        private void DrawSectionBlock(string title, string subtitle, System.Action draw)
        {
            var style = new GUIStyle(GUI.skin.box) { padding = new RectOffset(6, 6, 6, 6) };
            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(subtitle, new GUIStyle(EditorStyles.miniLabel)
                { wordWrap = true, fontStyle = FontStyle.Italic });
            EditorGUILayout.Space(4);
            EditorGUI.indentLevel++;
            draw();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }
#endif
}