using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

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

    [System.Serializable]
    public class ConditionEntry
    {
        [Tooltip("Disabled — ignored.\nAND — this condition must pass.\nOR  — this condition passing alone is enough.")]
        public ConditionMode mode = ConditionMode.AND;

        [Tooltip("Drag any MonoBehaviour that implements IAnomalyCondition.")]
        public MonoBehaviour script;
    }

    // ── Anomaly Trigger ───────────────────────────────────────────────────────────

    [System.Serializable]
    public class AnomalyTrigger
    {
        [Tooltip("Optional label. Auto-generated from active conditions if empty.")]
        public string triggerName = "";

        // ── Game State condition ───────────────────────────────────
        [Tooltip("Disabled — ignored.\nAND — must pass.\nOR  — passing alone fires the trigger.")]
        public ConditionMode gameStateMode = ConditionMode.AND;

        [Tooltip("The GameStateManager state index that satisfies this condition.")]
        [StateIndex] public int stateIndex;

        // ── Anomaly State condition ────────────────────────────────
        [Tooltip("Disabled — ignored.\nAND — must pass.\nOR  — passing alone fires the trigger.")]
        public ConditionMode anomalyStateMode = ConditionMode.AND;
        public AnomalyState anomalyStates;

        // ── Script Condition group ─────────────────────────────────
        [Tooltip("Disabled — ignored.\nAND — condition group must pass.\nOR  — condition group passing alone fires the trigger.")]
        public ConditionMode conditionMode = ConditionMode.Disabled;

        [Tooltip("Each condition entry carries its own AND / OR / Disabled mode.")]
        public List<ConditionEntry> conditions = new();

        public UnityEvent onEnter;
        public UnityEvent onExit;

        // ── Evaluation ────────────────────────────────────────────

        /// <summary>
        /// Evaluates this binding against the current game state index and anomaly state.
        /// </summary>
        public bool Evaluate(int currentStateIndex, AnomalyState anomalyState)
        {
            if (gameStateMode    == ConditionMode.Disabled &&
                anomalyStateMode == ConditionMode.Disabled &&
                conditionMode    == ConditionMode.Disabled) return true;

            bool stateResult     = currentStateIndex == stateIndex;
            bool anomalyResult   = (anomalyStates & anomalyState) != 0;
            bool conditionResult = EvaluateConditionGroup();

            return EvaluateGroup(
                (gameStateMode,    stateResult),
                (anomalyStateMode, anomalyResult),
                (conditionMode,    conditionResult));
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

        private bool EvaluateConditionGroup()
        {
            if (conditions == null || conditions.Count == 0) return true;

            var entries = new List<(ConditionMode mode, bool result)>();
            foreach (var entry in conditions)
            {
                if (entry == null || entry.mode == ConditionMode.Disabled) continue;
                bool r = entry.script is IAnomalyCondition c && c.Evaluate();
                entries.Add((entry.mode, r));
            }

            if (entries.Count == 0) return true;
            return EvaluateGroup(entries.ToArray());
        }

        public string AutoLabel()
        {
            var andParts = new List<string>();
            var orParts  = new List<string>();

            var gsm = GameStateManager.instance;

            if (gameStateMode == ConditionMode.AND || gameStateMode == ConditionMode.OR)
            {
                string stateLabel = gsm != null ? gsm.StateName(stateIndex) : $"[{stateIndex}]";
                if (gameStateMode == ConditionMode.AND) andParts.Add(stateLabel);
                else                                     orParts.Add(stateLabel);
            }

            if (anomalyStateMode == ConditionMode.AND) andParts.Add(anomalyStates.ToString());
            if (anomalyStateMode == ConditionMode.OR)  orParts.Add(anomalyStates.ToString());

            int active = conditions?.FindAll(e => e?.mode != ConditionMode.Disabled).Count ?? 0;
            if (conditionMode == ConditionMode.AND) andParts.Add($"{active} condition(s)");
            if (conditionMode == ConditionMode.OR)  orParts.Add($"{active} condition(s)");

            if (andParts.Count == 0 && orParts.Count == 0) return "(always fires)";

            var segments = new List<string>();
            if (andParts.Count > 0) segments.Add(string.Join(" AND ", andParts));
            if (orParts.Count  > 0) segments.Add(string.Join(" OR  ", orParts));
            return string.Join("  |  ", segments);
        }
    }

    // ── AnomalyStateManager ───────────────────────────────────────────────────────

    public class AnomalyStateManager : MonoBehaviour
    {
        [SerializeField] protected AnomalyDefinition anomalyToTrigger;

        [Tooltip("If enabled, any state change will cancel ongoing actions and proceed immediately.\n" +
                 "If disabled, state change requests are ignored while actions are still running.")]
        [SerializeField] private bool cancelActionsOnStateChange = true;

        [Header("Triggers")]
        [SerializeField] private List<AnomalyTrigger> triggers = new();

        private AnomalyState          _currentAnomalyState = AnomalyState.Disabled;
        private int                   _currentStateIndex   = -1;
        private int                   _pendingActions;
        private readonly HashSet<AnomalyAction> _activeActions   = new();
        private readonly HashSet<int>           _enteredTriggers = new();

        public AnomalyState      CurrentAnomalyState => _currentAnomalyState;
        public int               CurrentStateIndex   => _currentStateIndex;
        public AnomalyDefinition AnomalyToTrigger    => anomalyToTrigger;
        public int               PendingActions      => _pendingActions;

        /// <summary>Fires whenever an ASM is enabled in the scene. Recorder uses this to subscribe.</summary>
        public static event Action<AnomalyStateManager> OnRegistered;

        /// <summary>Fires whenever an ASM is disabled or destroyed. Recorder uses this to unsubscribe.</summary>
        public static event Action<AnomalyStateManager> OnUnregistered;

        /// <summary>Fires whenever the anomaly state changes. Passes sender, from/to states, and exact timestamp.</summary>
        public event Action<AnomalyStateManager, AnomalyState, AnomalyState, DateTime> OnAnomalyStateChanged;

        private bool _started;

        // ── Static Trial State — set by whoever drives trials (e.g. ExperimentSequencer) ──

        private static AnomalyDefinition _currentTrialAnomaly;

        /// <summary>
        /// Call this at the start of each trial to broadcast the active anomaly to all ASMs.
        /// ExperimentSequencer calls this — but any other system can too.
        /// </summary>
        public static void BroadcastTrialBegan(AnomalyDefinition anomaly)
        {
            _currentTrialAnomaly = anomaly;
            foreach (var asm in _allAsms)
                asm.ActivateAnomaly(anomaly);
        }

        /// <summary>Call this at trial end to clear the cached anomaly.</summary>
        public static void BroadcastTrialEnded()
        {
            _currentTrialAnomaly = null;
        }

        private static readonly List<AnomalyStateManager> _allAsms = new();

        // ── Lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            var gsm = GameStateManager.instance;
            if (gsm == null) { Debug.LogError("[ASM] No GameStateManager found!"); return; }

            gsm.OnStateChanged += OnStateChanged;
            _allAsms.Add(this);
            SetupObjectAtStart();

            OnRegistered?.Invoke(this);
            _started = true;
            SyncState();
        }

        private void OnEnable()
        {
            if (_started) SyncState();
        }

        private void OnDestroy()
        {
            var gsm = GameStateManager.instance;
            if (gsm != null)
                gsm.OnStateChanged -= OnStateChanged;

            _allAsms.Remove(this);
            OnUnregistered?.Invoke(this);
        }

        // ── Trial Activation ───────────────────────────────────────────────────

        private void ActivateAnomaly(AnomalyDefinition activeAnomaly)
        {
            _pendingActions    = 0;
            _activeActions.Clear();
            _enteredTriggers.Clear();

            bool isSelected = activeAnomaly != null && activeAnomaly == anomalyToTrigger;
            SetAnomalyState(isSelected ? AnomalyState.Active : AnomalyState.Disabled);
        }

        private void OnStateChanged(int fromIndex, int toIndex, DateTime _)
        {
            _currentStateIndex = toIndex;
            EvaluateTriggers();
        }

        /// <summary>
        /// Syncs GSM state and anomaly state from the cached trial anomaly.
        /// Called automatically on Start() and OnEnable() so late-spawned objects
        /// catch up without needing any external call.
        /// </summary>
        public void SyncState()
        {
            var gsm = GameStateManager.instance;
            if (gsm == null) return;
            _currentStateIndex = gsm.CurrentStateIndex;

            bool isSelected      = _currentTrialAnomaly != null && _currentTrialAnomaly == anomalyToTrigger;
            _currentAnomalyState = isSelected ? AnomalyState.Active : AnomalyState.Disabled;

            EvaluateTriggers();
        }

        // ── State Machine ──────────────────────────────────────────

        private void SetAnomalyState(AnomalyState newState)
        {
            if (_currentAnomalyState == newState) return;

            if (_pendingActions > 0)
            {
                if (cancelActionsOnStateChange)
                    CancelAllActions();
                else
                {
                    Debug.LogWarning($"[ASM] State change to {newState} ignored — {_pendingActions} action(s) still running on '{gameObject.name}'.");
                    return;
                }
            }

            AnomalyState prev    = _currentAnomalyState;
            _currentAnomalyState = newState;
            OnAnomalyStateChanged?.Invoke(this, prev, newState, DateTime.Now);
            EvaluateTriggers();
        }

        private void EvaluateTriggers()
        {
            for (int i = 0; i < triggers.Count; i++)
            {
                var  trigger    = triggers[i];
                bool passes     = trigger.Evaluate(_currentStateIndex, _currentAnomalyState);
                bool wasActive  = _enteredTriggers.Contains(i);

                if (passes && !wasActive)
                {
                    _enteredTriggers.Add(i);
                    trigger.onEnter?.Invoke();
                }
                else if (!passes && wasActive)
                {
                    _enteredTriggers.Remove(i);
                    trigger.onExit?.Invoke();
                }
            }
        }

        // ── Action Tracking ────────────────────────────────────────

        /// <summary>Register an async action before Execute(). One-shots should not call this.</summary>
        public void RegisterAction(AnomalyAction action)
        {
            if (_activeActions.Add(action))
                _pendingActions++;
        }

        /// <summary>Signal that an async action has finished.</summary>
        public void CompleteAnomaly(AnomalyAction action)
        {
            if (!_activeActions.Remove(action)) return;
            _pendingActions = Mathf.Max(0, _pendingActions - 1);
            if (_pendingActions == 0 && _currentAnomalyState == AnomalyState.Triggered)
                SetAnomalyState(AnomalyState.Completed);
        }

        /// <summary>Cancel all running async actions.</summary>
        public void CancelAllActions()
        {
            foreach (var action in _activeActions)
                action.CancelAnomalyAction();
            _activeActions.Clear();
            _pendingActions = 0;
        }

        // ── Anomaly Control ────────────────────────────────────────

        public void TriggerAnomaly()
        {
            if (_currentAnomalyState != AnomalyState.Active) return;
            SetAnomalyState(AnomalyState.Triggered);
        }

        /// <summary>Editor debug only — bypass guards and force any anomaly state directly.</summary>
        public void ForceAnomalyState(AnomalyState state) => SetAnomalyState(state);

        // ── Virtual Hooks ──────────────────────────────────────────

        protected virtual void SetupObjectAtStart() { }

        // ── Getters ────────────────────────────────────────────────

        public string GetCurrentAnomalyName() =>
            anomalyToTrigger != null ? anomalyToTrigger.ToString() : "NONE";
    }

    // ── Editor ────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [CustomEditor(typeof(AnomalyStateManager), editorForChildClasses: true)]
    public class AnomalyStateManagerEditor : Editor
    {
        private SerializedProperty _anomalyToTrigger;
        private SerializedProperty _cancelActionsOnStateChange;
        private SerializedProperty _triggers;
        private List<bool> _foldouts = new();

        private void OnEnable()
        {
            _anomalyToTrigger           = serializedObject.FindProperty("anomalyToTrigger");
            _cancelActionsOnStateChange = serializedObject.FindProperty("cancelActionsOnStateChange");
            _triggers                   = serializedObject.FindProperty("triggers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", "anomalyToTrigger", "cancelActionsOnStateChange", "triggers");
            EditorGUILayout.PropertyField(_anomalyToTrigger);

            var cancelProp = _cancelActionsOnStateChange ?? serializedObject.FindProperty("cancelActionsOnStateChange");
            if (cancelProp != null)
                EditorGUILayout.PropertyField(cancelProp, new GUIContent("Cancel Actions on State Change"));
            else
                Debug.LogError("[ASM Editor] cancelActionsOnStateChange property not found.");

            // ── Debug: Force Anomaly State ─────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Debug", EditorStyles.miniBoldLabel);

            bool inPlayMode = Application.isPlaying;
            var  asm        = (AnomalyStateManager)target;

            GUI.enabled = inPlayMode;
            EditorGUILayout.BeginHorizontal();

            var states = new (string label, AnomalyState state, Color color)[]
            {
                ("Disabled",  AnomalyState.Disabled,  new Color(0.55f, 0.55f, 0.55f)),
                ("Active",    AnomalyState.Active,    new Color(0.40f, 0.75f, 1.00f)),
                ("Triggered", AnomalyState.Triggered, new Color(1.00f, 0.82f, 0.30f)),
                ("Completed", AnomalyState.Completed, new Color(0.50f, 1.00f, 0.60f)),
            };

            foreach (var (label, state, color) in states)
            {
                bool isCurrent = inPlayMode && asm.CurrentAnomalyState == state;
                GUI.color = isCurrent ? color : new Color(color.r, color.g, color.b, 0.45f);
                if (GUILayout.Button(label, GUILayout.Height(22)))
                    asm.ForceAnomalyState(state);
            }

            GUI.color   = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!inPlayMode)
                EditorGUILayout.HelpBox("Enter Play Mode to force states.", MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Triggers", EditorStyles.boldLabel);

            while (_foldouts.Count < _triggers.arraySize) _foldouts.Add(true);
            while (_foldouts.Count > _triggers.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < _triggers.arraySize; i++)
                DrawBinding(i);

            EditorGUILayout.Space(2);
            GUI.color = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("+ Add Trigger"))
            {
                _triggers.InsertArrayElementAtIndex(_triggers.arraySize);
                _foldouts.Add(true);
            }
            GUI.color = Color.white;

            serializedObject.ApplyModifiedProperties();
        }

        // ── Binding Row ────────────────────────────────────────────

        private void DrawBinding(int i)
        {
            var binding      = _triggers.GetArrayElementAtIndex(i);
            var nameProp     = binding.FindPropertyRelative("triggerName");
            var gsModeProp   = binding.FindPropertyRelative("gameStateMode");
            var indexProp    = binding.FindPropertyRelative("stateIndex");
            var aModeProp    = binding.FindPropertyRelative("anomalyStateMode");
            var aStateProp   = binding.FindPropertyRelative("anomalyStates");
            var tModeProp    = binding.FindPropertyRelative("conditionMode");
            var trigsProp    = binding.FindPropertyRelative("conditions");
            var onEnterProp  = binding.FindPropertyRelative("onEnter");
            var onExitProp   = binding.FindPropertyRelative("onExit");

            // Auto-label header
            string stateLabel   = ResolveStateLabel(indexProp.intValue);
            string autoLabel    = BuildAutoLabel(
                (ConditionMode)gsModeProp.intValue, stateLabel,
                (ConditionMode)aModeProp.intValue,  aStateProp,
                (ConditionMode)tModeProp.intValue,  trigsProp);

                string header = string.IsNullOrEmpty(nameProp.stringValue)
                ? autoLabel
                : $"{nameProp.stringValue}  —  {autoLabel}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Foldout + remove button
            EditorGUILayout.BeginHorizontal();
            _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i], header, true);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                _triggers.DeleteArrayElementAtIndex(i);
                _foldouts.RemoveAt(i);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_foldouts[i])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Trigger Name"));
                EditorGUILayout.Space(4);

                // Game State — mode + dropdown
                DrawModeRow(gsModeProp, "Game State", () =>
                    EditorGUILayout.PropertyField(indexProp, GUIContent.none));

                // Anomaly State — mode + flags
                DrawModeRow(aModeProp, "Anomaly State", () =>
                    EditorGUILayout.PropertyField(aStateProp, GUIContent.none));

                // Script Conditions — mode + list
                DrawModeRow(tModeProp, "Script Conditions", null, () =>
                    DrawTriggerList(trigsProp));

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(onEnterProp, new GUIContent("On Enter"));
                EditorGUILayout.PropertyField(onExitProp,  new GUIContent("On Exit"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── Trigger List ────────────────────────────────────────────

        private void DrawTriggerList(SerializedProperty triggersProp)
        {
            const float MODE_W   = 120f;
            const float WARN_W   = 18f;
            const float REMOVE_W = 20f;
            const float GAP      = 4f;

            EditorGUI.indentLevel++;

            for (int c = 0; c < triggersProp.arraySize; c++)
            {
                var entryProp  = triggersProp.GetArrayElementAtIndex(c);
                var modeProp   = entryProp.FindPropertyRelative("mode");
                var scriptProp = entryProp.FindPropertyRelative("script");
                var mode       = (ConditionMode)modeProp.intValue;
                var mb         = scriptProp.objectReferenceValue as MonoBehaviour;
                bool invalid   = mb != null && mb is not IAnomalyCondition;

                float totalW = EditorGUIUtility.currentViewWidth - EditorGUI.indentLevel * 15f - 6f;
                float warnW  = invalid ? WARN_W + GAP : 0f;
                float fieldW = totalW - MODE_W - GAP - warnW - REMOVE_W - GAP;

                Rect  row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                float x = row.x, y = row.y, h = row.height;

                GUI.color = ModeColour(mode);
                EditorGUI.BeginChangeCheck();
                var newMode = (ConditionMode)EditorGUI.EnumPopup(new Rect(x, y, MODE_W, h), GUIContent.none, mode);
                if (EditorGUI.EndChangeCheck()) modeProp.intValue = (int)newMode;
                GUI.color = Color.white;
                x += MODE_W + GAP;

                GUI.enabled = mode != ConditionMode.Disabled;
                if (invalid) GUI.color = new Color(1f, 0.75f, 0.4f);
                EditorGUI.BeginChangeCheck();
                var newObj = EditorGUI.ObjectField(new Rect(x, y, fieldW, h),
                    GUIContent.none, scriptProp.objectReferenceValue, typeof(MonoBehaviour), true);
                if (EditorGUI.EndChangeCheck()) scriptProp.objectReferenceValue = newObj;
                GUI.color = Color.white; GUI.enabled = true;
                x += fieldW + GAP;

                if (invalid)
                {
                    EditorGUI.LabelField(new Rect(x, y, WARN_W, h),
                        new GUIContent("⚠", "Must implement IAnomalyCondition"));
                    x += WARN_W + GAP;
                }

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
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Condition", GUILayout.Width(120)))
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
            var newMode = (ConditionMode)EditorGUILayout.EnumPopup(
                GUIContent.none, mode, GUILayout.Width(90));
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

        private static string ResolveStateLabel(int index)
        {
            var gsm = GetGSM();
            return gsm != null ? gsm.StateName(index) : $"[{index}]";
        }

        private static GameStateManager GetGSM()
        {
            if (GameStateManager.instance != null) return GameStateManager.instance;
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<GameStateManager>();
#else
            return UnityEngine.Object.FindObjectOfType<GameStateManager>();
#endif
        }

        private static string BuildAutoLabel(
            ConditionMode gsMode,  string stateLabel,
            ConditionMode aMode,   SerializedProperty anomalyProp,
            ConditionMode tMode,   SerializedProperty triggersProp)
        {
            var andParts = new List<string>();
            var orParts  = new List<string>();

            if (gsMode == ConditionMode.AND) andParts.Add(stateLabel);
            if (gsMode == ConditionMode.OR)  orParts.Add(stateLabel);

            if (aMode == ConditionMode.AND) andParts.Add(((AnomalyState)anomalyProp.intValue).ToString());
            if (aMode == ConditionMode.OR)  orParts.Add(((AnomalyState)anomalyProp.intValue).ToString());

            int active = 0;
            for (int s = 0; s < triggersProp.arraySize; s++)
            {
                var m = (ConditionMode)triggersProp.GetArrayElementAtIndex(s)
                            .FindPropertyRelative("mode").intValue;
                if (m != ConditionMode.Disabled) active++;
            }
            if (tMode == ConditionMode.AND) andParts.Add($"{active} condition(s)");
            if (tMode == ConditionMode.OR)  orParts.Add($"{active} condition(s)");

            if (andParts.Count == 0 && orParts.Count == 0) return "(always fires)";

            var segments = new List<string>();
            if (andParts.Count > 0) segments.Add(string.Join(" AND ", andParts));
            if (orParts.Count  > 0) segments.Add(string.Join(" OR  ", orParts));
            return string.Join("  |  ", segments);
        }
    }
#endif
}