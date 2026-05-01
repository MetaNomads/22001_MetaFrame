using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using MetaFrame.Contracts;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    // ── Anomaly State ─────────────────────────────────────────────────────────────

    [System.Flags]
    public enum AnomalyState
    {
        Disabled = 1 << 0,
        Active = 1 << 1,
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
            if (gameStateMode == ConditionMode.Disabled &&
                anomalyStateMode == ConditionMode.Disabled &&
                conditionMode == ConditionMode.Disabled) return true;

            bool stateResult     = currentStateIndex == stateIndex;
            bool anomalyResult   = (anomalyStates & anomalyState) != 0;
            bool conditionResult = EvaluateConditionGroup();

            // FIX: was EvaluateGroup(params (ConditionMode,bool)[]) which allocated
            // a new tuple array on the heap every call. EvaluateTriggers() calls
            // Evaluate() on every trigger in every ASM on every GSM or anomaly
            // state change — the params alloc was constant GC churn during gameplay.
            // Inlined to three explicit checks with zero allocations.
            return EvaluateGroup(
                gameStateMode,    stateResult,
                anomalyStateMode, anomalyResult,
                conditionMode,    conditionResult);
        }

        private static bool EvaluateGroup(
            ConditionMode gsMode,  bool gsResult,
            ConditionMode aMode,   bool aResult,
            ConditionMode tMode,   bool tResult)
        {
            bool orPresent = false, orSatisfied = false;

            // Game state
            if (gsMode == ConditionMode.AND && !gsResult) return false;
            if (gsMode == ConditionMode.OR) { orPresent = true; if (gsResult) orSatisfied = true; }

            // Anomaly state
            if (aMode == ConditionMode.AND && !aResult) return false;
            if (aMode == ConditionMode.OR) { orPresent = true; if (aResult) orSatisfied = true; }

            // Script conditions
            if (tMode == ConditionMode.AND && !tResult) return false;
            if (tMode == ConditionMode.OR) { orPresent = true; if (tResult) orSatisfied = true; }

            return !orPresent || orSatisfied;
        }

        private bool EvaluateConditionGroup()
        {
            if (conditions == null || conditions.Count == 0) return true;

            bool orPresent = false;
            bool orSatisfied = false;

            foreach (var entry in conditions)
            {
                if (entry == null || entry.mode == ConditionMode.Disabled) continue;
                bool r = entry.script is IAnomalyCondition c && c.Evaluate();
                if (entry.mode == ConditionMode.AND && !r) return false;
                if (entry.mode == ConditionMode.OR)
                {
                    orPresent = true;
                    if (r) orSatisfied = true;
                }
            }

            return !orPresent || orSatisfied;
        }

        public string AutoLabel()
        {
            var andParts = new List<string>();
            var orParts = new List<string>();

            var gsm = GameStateManager.instance;

            if (gameStateMode == ConditionMode.AND || gameStateMode == ConditionMode.OR)
            {
                string stateLabel = gsm != null ? gsm.StateName(stateIndex) : $"[{stateIndex}]";
                if (gameStateMode == ConditionMode.AND) andParts.Add(stateLabel);
                else orParts.Add(stateLabel);
            }

            if (anomalyStateMode == ConditionMode.AND) andParts.Add(anomalyStates.ToString());
            if (anomalyStateMode == ConditionMode.OR) orParts.Add(anomalyStates.ToString());

            int active = conditions?.FindAll(e => e?.mode != ConditionMode.Disabled).Count ?? 0;
            if (conditionMode == ConditionMode.AND) andParts.Add($"{active} condition(s)");
            if (conditionMode == ConditionMode.OR) orParts.Add($"{active} condition(s)");

            if (andParts.Count == 0 && orParts.Count == 0) return "(always fires)";

            var segments = new List<string>();
            if (andParts.Count > 0) segments.Add(string.Join(" AND ", andParts));
            if (orParts.Count > 0) segments.Add(string.Join(" OR  ", orParts));
            return string.Join("  |  ", segments);
        }
    }

    // ── AnomalyStateManager ───────────────────────────────────────────────────────

    public class AnomalyStateManager : MonoBehaviour, ISelfHealing
    {
        [SerializeField] protected AnomalyDefinition anomalyToTrigger;

        [Tooltip("If enabled, any state change will cancel ongoing actions and proceed immediately.\n" +
                 "If disabled, state change requests are ignored while actions are still running.")]
        [SerializeField] private bool cancelActionsOnStateChange = true;

        [Header("Triggers")]
        [SerializeField] private List<AnomalyTrigger> triggers = new();

        private AnomalyState _currentAnomalyState = AnomalyState.Disabled;
        private int _currentStateIndex = -1;
        private int _pendingActions;
        private readonly HashSet<AnomalyAction> _activeActions = new();
        private readonly HashSet<int> _enteredTriggers = new();

        // FIX: cache gameObject.name — Unity's gameObject.name allocates a new
        // string on every access. This is used by ExperimentDataRecorder on every
        // anomaly state transition during a trial.
        private string _cachedGameObjectName;
        public  string CachedName => _cachedGameObjectName;

        // FIX: static lookup for AnomalyState enum → string.
        // AnomalyState.ToString() allocates a new string every call.
        // Used by ExperimentDataRecorder.OnAnomalyStateChanged per transition.
        private static readonly string[] _anomalyStateNames =
        {
            "Disabled",   // bit 0 (1 << 0)
            "Active",     // bit 1 (1 << 1)
            "Triggered",  // bit 2 (1 << 2)
            "Completed",  // bit 3 (1 << 3)
        };

        // Returns the cached name string for a given AnomalyState flag value.
        // Falls back to ToString() for unexpected values.
        public static string AnomalyStateName(AnomalyState state) => state switch
        {
            AnomalyState.Disabled  => _anomalyStateNames[0],
            AnomalyState.Active    => _anomalyStateNames[1],
            AnomalyState.Triggered => _anomalyStateNames[2],
            AnomalyState.Completed => _anomalyStateNames[3],
            _                      => state.ToString(),
        };

        public AnomalyState CurrentAnomalyState => _currentAnomalyState;
        public int CurrentStateIndex => _currentStateIndex;
        public AnomalyDefinition AnomalyToTrigger => anomalyToTrigger;
        public int PendingActions => _pendingActions;

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
            foreach (var asm in _allAsms.ToList())
                asm.ActivateAnomaly(anomaly);
        }

        /// <summary>Call this at trial end to clear the cached anomaly.</summary>
        public static void BroadcastTrialEnded()
        {
            _currentTrialAnomaly = null;
        }

        private static readonly HashSet<AnomalyStateManager> _allAsms = new();

        // ── Lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            _cachedGameObjectName = gameObject.name;
            SetupObjectAtStart();
            _started = true;
        }

        private void OnEnable()
        {
            // FIX (S-4): null-guard. If script execution order or scene state
            // puts an ASM before the GSM's Awake runs, GameStateManager.instance
            // is null here. The previous code threw NRE and silently left the
            // ASM permanently unsubscribed for the rest of the session — a
            // determinism violation that affects research data integrity.
            // Now we log loudly and still register so OnRegistered fires.
            var gsm = GameStateManager.instance;

            if (gsm != null)
            {
                gsm.OnStateChanged += OnStateChanged;
            }
            else
            {
                Debug.LogWarning(
                    $"[ASM:{name}] GameStateManager.instance was null in OnEnable — " +
                    "OnStateChanged subscription skipped. This ASM will not respond to " +
                    "GSM transitions. Ensure GameStateManager has a lower script execution " +
                    "order or that it Awakes before this object becomes enabled.");
            }

            _allAsms.Add(this);
            OnRegistered?.Invoke(this);
            SyncState();
        }

        private void OnDisable()
        {
            var gsm = GameStateManager.instance;
            if (gsm != null)
                gsm.OnStateChanged -= OnStateChanged;

            _allAsms.Remove(this);
            OnUnregistered?.Invoke(this);
        }

        private void OnDestroy() { }  // OnDisable always runs before OnDestroy — cleanup handled there

        // ── Validation (CONTRACT) ─────────────────────────────────────────────
        private void OnValidate()
        {
            if (anomalyToTrigger == null)
                Debug.LogWarning(
                    $"[ASM:{name}] anomalyToTrigger is unassigned. This ASM " +
                    "will never activate (BroadcastTrialBegan only matches when the " +
                    "trial's anomaly == anomalyToTrigger).", this);

            if (triggers == null) return;
            for (int i = 0; i < triggers.Count; i++)
            {
                var t = triggers[i];
                if (t == null) continue;
                if (t.conditions == null) continue;
                for (int c = 0; c < t.conditions.Count; c++)
                {
                    var entry = t.conditions[c];
                    if (entry?.script != null && entry.script is not IAnomalyCondition)
                        Debug.LogWarning(
                            $"[ASM:{name}] Trigger {i} condition {c}: '{entry.script.GetType().Name}' " +
                            "does not implement IAnomalyCondition; it will be ignored at runtime.", this);
                }
            }
        }

        // ── Self-heal (ISelfHealing) ─────────────────────────────────────────
        public string SelfHealLabel => name;

        /// <summary>
        /// Reconciles internal state that can drift if AnomalyAction subclasses
        /// fail to call CompleteAnomalyAction(), or if an action is destroyed
        /// without unregistering. Returns true if any repair was made.
        /// </summary>
        public bool RunSelfHeal()
        {
            bool healed = false;

            // Drift case: _activeActions contains nulls (destroyed monobehaviour
            // references). Strip them and reconcile _pendingActions.
            int nullsRemoved = _activeActions.RemoveWhere(a => a == null);
            if (nullsRemoved > 0)
            {
                Contract.Healed(
                    () => false, // we already detected drift
                    () => _pendingActions = _activeActions.Count,
                    $"removed {nullsRemoved} destroyed AnomalyAction(s); reset _pendingActions to {_activeActions.Count}",
                    this);
                healed = true;
            }

            // Drift case: _pendingActions counter does not match _activeActions.Count.
            // This happens when CompleteAnomalyAction is called twice or skipped.
            healed |= !Contract.Healed(
                () => _pendingActions == _activeActions.Count,
                () => _pendingActions = _activeActions.Count,
                $"_pendingActions ({_pendingActions}) drifted from _activeActions.Count ({_activeActions.Count})",
                this);

            // Drift case: stuck in Triggered with zero pending and no actions —
            // means SetAnomalyState should have advanced to Completed but didn't.
            if (_currentAnomalyState == AnomalyState.Triggered && _pendingActions == 0 && _activeActions.Count == 0)
            {
                Contract.Healed(
                    () => false,
                    () => SetAnomalyState(AnomalyState.Completed),
                    "stuck in Triggered with no pending actions; advanced to Completed",
                    this);
                healed = true;
            }

            return healed;
        }

        // ── Trial Activation ───────────────────────────────────────────────────

        private void ActivateAnomaly(AnomalyDefinition activeAnomaly)
        {
            _pendingActions = 0;
            _activeActions.Clear();

            // Fire onExit for every currently active trigger before resetting.
            // Without this, external components wired to onExit (e.g. StopEvaluating,
            // ResetTrigger on CollisionDetector) never run and are left in a stale state.
            ExitAllActiveTriggers();

            bool isSelected = activeAnomaly != null && activeAnomaly == anomalyToTrigger;
            SetAnomalyState(isSelected ? AnomalyState.Active : AnomalyState.Disabled);
        }

        private void ExitAllActiveTriggers()
        {
            foreach (int i in _enteredTriggers)
            {
                if (i < triggers.Count)
                    triggers[i].onExit?.Invoke();
            }
            _enteredTriggers.Clear();
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

            bool isSelected = _currentTrialAnomaly != null && _currentTrialAnomaly == anomalyToTrigger;
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
                    return;
                }
            }

            AnomalyState prev = _currentAnomalyState;
            _currentAnomalyState = newState;
            OnAnomalyStateChanged?.Invoke(this, prev, newState, DateTime.Now);
            EvaluateTriggers();
        }

        // FIX (S-2): switch from interleaved evaluate-then-invoke to
        // snapshot-then-invoke (transactional semantics).
        //
        // Why the change:
        //   The previous loop computed (passes, wasActive) for trigger[i],
        //   committed _enteredTriggers, and invoked the UnityEvent — all
        //   inline before moving to trigger[i+1]. If trigger[i].onEnter
        //   cascaded back into SetAnomalyState → EvaluateTriggers (via
        //   AnomalyStateManager.TriggerAnomaly listeners, which the project
        //   does wire up), the inner pass would re-enter this method with a
        //   mutated _enteredTriggers AND mutated trigger evaluation state.
        //   The outer pass would then continue iterating with no awareness
        //   that decisions for triggers[i+1..n] might have changed underneath.
        //
        // The new code:
        //   1. Compute all enter/exit decisions for THIS call's snapshot of
        //      _currentStateIndex / _currentAnomalyState.
        //   2. Commit all _enteredTriggers mutations.
        //   3. Invoke UnityEvents from the local plan.
        //
        //   A re-entrant call gets its own fresh local plan and operates on
        //   the already-committed _enteredTriggers, so neither call's
        //   iteration corrupts the other's.
        //
        // Behavior change to be aware of:
        //   The OLD behavior, by accident, gave cascading state-changes the
        //   chance to influence later triggers' evaluations within the same
        //   call. The NEW behavior is transactional: each EvaluateTriggers
        //   call commits the decision set it computed at entry, even if a
        //   re-entrant cascade later changes the underlying state. If a
        //   cascade is meant to affect more triggers, the cascade itself
        //   will re-enter EvaluateTriggers and the second pass will pick up
        //   those new decisions. Net result is the same in steady state;
        //   the difference is observable only mid-cascade and only if a
        //   listener is sensitive to the firing order.
        //
        // Allocation:
        //   `plan` is allocated per call. EvaluateTriggers fires on GSM and
        //   anomaly state changes — order of 10s of times per trial — so
        //   the cost is negligible.
        private void EvaluateTriggers()
        {
            var plan = new System.Collections.Generic.List<(int idx, bool enter)>();

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger    = triggers[i];
                bool passes    = trigger.Evaluate(_currentStateIndex, _currentAnomalyState);
                bool wasActive = _enteredTriggers.Contains(i);

                if (passes && !wasActive)
                {
                    _enteredTriggers.Add(i);
                    plan.Add((i, true));
                }
                else if (!passes && wasActive)
                {
                    _enteredTriggers.Remove(i);
                    plan.Add((i, false));
                }
            }

            // Invoke UnityEvents from the local plan. A re-entrant call gets
            // its own plan list and its own fully-committed _enteredTriggers,
            // so it cannot corrupt this iteration.
            for (int p = 0; p < plan.Count; p++)
            {
                var (idx, enter) = plan[p];
                if (idx >= triggers.Count) continue;  // defense: triggers list mutated
                var t = triggers[idx];
                if (enter) t.onEnter?.Invoke();
                else       t.onExit?.Invoke();
            }
        }

        // ── Action Tracking ────────────────────────────────────────

        /// <summary>Register an async action before Execute(). One-shots should not call this.</summary>
        public void RegisterAction(AnomalyAction action)
        {
            // CONTRACT: precondition — action must not be null.
            Contract.Require(action != null, "RegisterAction with null action", this);
            if (action == null) return;

            if (_activeActions.Add(action))
                _pendingActions++;

            // INVARIANT: counter always matches set size after a registration.
            Contract.Invariant(_pendingActions == _activeActions.Count,
                $"RegisterAction left _pendingActions ({_pendingActions}) != Count ({_activeActions.Count})", this);
        }

        /// <summary>Signal that an async action has finished.</summary>
        public void CompleteAnomaly(AnomalyAction action)
        {
            if (!_activeActions.Remove(action)) return;
            _pendingActions = Mathf.Max(0, _pendingActions - 1);

            // INVARIANT: counter always matches set size after a completion.
            Contract.Invariant(_pendingActions == _activeActions.Count,
                $"CompleteAnomaly left _pendingActions ({_pendingActions}) != Count ({_activeActions.Count})", this);

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

        /// <summary>
        /// Immediately moves to Completed regardless of pending actions.
        /// Use when the completion condition is met externally (e.g. collision,
        /// gesture) and you don't need to wait for AnomalyActions to finish.
        /// </summary>
        public void CompleteAnomalyNow()
        {
            if (_currentAnomalyState != AnomalyState.Triggered) return;
            SetAnomalyState(AnomalyState.Completed);
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