using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    // ── State Slot ────────────────────────────────────────────────────────────────
    // Lives on the GSM. Pairs a StateDefinition asset with scene-bound callbacks
    // and transition rules.

    [Serializable]
    public class StateSlot
    {
        [Tooltip("Drag a StateDefinition asset here.")]
        public StateDefinition definition;

        [Tooltip("Which states are allowed to transition into this one.\n" +
                 "Leave empty to allow from any state.")]
        public List<StateDefinition> allowedFrom = new();

        public UnityEvent onEnter;
        public UnityEvent onExit;

        public string DisplayName =>
            definition != null ? definition.displayName : "(unassigned)";
    }

    // ── StateIndex Attribute ──────────────────────────────────────────────────────

    /// <summary>
    /// Tag an int field with this to render it as a named-state dropdown
    /// pulled from the scene's GameStateManager.
    /// </summary>
    public class StateIndexAttribute : PropertyAttribute { }

    // ── GameStateManager ──────────────────────────────────────────────────────────

    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager instance;

        [SerializeField] private List<StateSlot>   slots            = new();
        [SerializeField] private StateDefinition   idleState;

        // ── Runtime ────────────────────────────────────────────────────────────────

        private int _currentIndex = -1;

        // ── Public Accessors ───────────────────────────────────────────────────────

        public int               CurrentStateIndex      => _currentIndex;
        public StateDefinition   CurrentStateDefinition => SlotAt(_currentIndex)?.definition;
        public int               CurrentStateBit        => IndexToBit(_currentIndex);
        public int               SlotCount              => slots.Count;

        /// <summary>Read-only slot list, used by property drawers.</summary>
        public IReadOnlyList<StateSlot> Slots => slots;

        // ── C# Events ─────────────────────────────────────────────────────────────

        /// <summary>Fires after every successful transition. Carries from index, to index,
        /// and the exact DateTime the transition occurred — generated at source for precision.</summary>
        public event Action<int, int, DateTime> OnStateChanged;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogWarning("[GSM] Duplicate GameStateManager destroyed.");
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            if (idleState != null)
                ForceState(idleState);
            else if (slots.Count > 0)
                ForceState(0);
        }

        // ── Transition API — by StateDefinition ───────────────────────────────────

        /// <summary>
        /// Request a transition to the slot whose definition matches <paramref name="to"/>.
        /// Validates allowedFrom rules; blocks and logs an error on violation.
        /// </summary>
        public void RequestTransition(StateDefinition to)
        {
            int toIndex = IndexOf(to);
            if (toIndex < 0)
            {
                Debug.LogError($"[GSM] RequestTransition: '{to?.displayName}' not found in slots.");
                return;
            }
            RequestTransition(toIndex);
        }

        /// <summary>
        /// Request a transition by slot index.
        /// </summary>
        public bool RequestTransition(int toIndex)
        {
            if (!IsValidIndex(toIndex))
            {
                Debug.LogError($"[GSM] RequestTransition: index {toIndex} out of range.");
                return false;
            }

            var slot = slots[toIndex];

            if (slot.allowedFrom != null && slot.allowedFrom.Count > 0)
            {
                var currentDef = SlotAt(_currentIndex)?.definition;
                if (!slot.allowedFrom.Contains(currentDef))
                {
                    Debug.LogError(
                        $"[GSM] Transition BLOCKED: {StateName(_currentIndex)} → {slot.DisplayName}. " +
                        $"Allowed from: [{string.Join(", ", slot.allowedFrom.ConvertAll(d => d?.displayName ?? "null"))}]");
                    return false;
                }
            }

            ApplyTransition(toIndex);
            return true;
        }

        /// <summary>Bypass rules and force a transition to the matching slot.</summary>
        public void ForceState(StateDefinition to)
        {
            int i = IndexOf(to);
            if (i < 0) { Debug.LogError($"[GSM] ForceState: '{to?.displayName}' not found."); return; }
            ApplyTransition(i);
        }

        /// <summary>Bypass rules and force a transition by slot index.</summary>
        public void ForceState(int toIndex)
        {
            if (!IsValidIndex(toIndex)) { Debug.LogError($"[GSM] ForceState: index {toIndex} out of range."); return; }
            ApplyTransition(toIndex);
        }

        /// <summary>Resets to the idleState asset if assigned, otherwise falls back to slot 0.
        /// Mirrors the Start() behaviour — useful for restart buttons wired through the event system.</summary>
        public void ResetToIdleState()
        {
            if (idleState != null)
                ForceState(idleState);
            else if (slots.Count > 0)
                ForceState(0);
            else
                Debug.LogWarning("[GSM] ResetToIdleState: no idleState set and no slots available.");
        }

        private void ApplyTransition(int toIndex)
        {
            int      fromIndex = _currentIndex;
            DateTime now       = DateTime.Now;

            SlotAt(fromIndex)?.onExit?.Invoke();

            _currentIndex = toIndex;
            slots[toIndex].onEnter?.Invoke();

            OnStateChanged?.Invoke(fromIndex, toIndex, now);
            Debug.Log($"[GSM] {StateName(fromIndex)} → {StateName(toIndex)}");
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        public static int IndexToBit(int index) => index >= 0 ? 1 << index : 0;

        public string StateName(int index) => SlotAt(index)?.DisplayName ?? $"[{index}]";

        public string[] StateNameArray()
        {
            var names = new string[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                names[i] = slots[i].DisplayName;
            return names;
        }

        public int IndexOf(StateDefinition def)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].definition == def) return i;
            return -1;
        }

        private StateSlot   SlotAt(int index)     => IsValidIndex(index) ? slots[index] : null;
        private bool        IsValidIndex(int index) => index >= 0 && index < slots.Count;
    }

    // ── Editor ────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR

    [CustomEditor(typeof(GameStateManager))]
    public class GameStateManagerEditor : Editor
    {
        private SerializedProperty _slotsProp;
        private SerializedProperty _idleStateProp;
        private List<bool>         _foldouts        = new();
        private int                _pendingMoveFrom = -1;
        private int                _pendingMoveTo   = -1;
        private int                _pendingRemove   = -1;

        private void OnEnable()
        {
            _slotsProp        = serializedObject.FindProperty("slots");
            _idleStateProp = serializedObject.FindProperty("idleState");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", "slots", "idleState");

            EditorGUILayout.Space(6);

            // Idle state — asset picker
            EditorGUILayout.PropertyField(_idleStateProp,
                new GUIContent("Idle State", "Which state to enter on Start."));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            while (_foldouts.Count < _slotsProp.arraySize) _foldouts.Add(false);
            while (_foldouts.Count > _slotsProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

            for (int i = 0; i < _slotsProp.arraySize; i++)
                DrawSlotBlock(i);

            // Deferred move / remove
            if (_pendingRemove >= 0)
            {
                _slotsProp.DeleteArrayElementAtIndex(_pendingRemove);
                _foldouts.RemoveAt(_pendingRemove);
                _pendingRemove = -1;
            }
            else if (_pendingMoveFrom >= 0)
            {
                _slotsProp.MoveArrayElement(_pendingMoveFrom, _pendingMoveTo);
                (_foldouts[_pendingMoveFrom], _foldouts[_pendingMoveTo]) =
                    (_foldouts[_pendingMoveTo], _foldouts[_pendingMoveFrom]);
                _pendingMoveFrom = _pendingMoveTo = -1;
            }

            EditorGUILayout.Space(2);
            GUI.color = new Color(0.4f, 0.75f, 1f);
            if (GUILayout.Button("+ Add State", GUILayout.Height(26)))
            {
                _slotsProp.arraySize++;
                _foldouts.Add(false);
            }
            GUI.color = Color.white;

            serializedObject.ApplyModifiedProperties();
        }

        // ── Slot Block ─────────────────────────────────────────────────────────────

        private void DrawSlotBlock(int index)
        {
            var slot       = _slotsProp.GetArrayElementAtIndex(index);
            var defProp    = slot.FindPropertyRelative("definition");
            var allowProp  = slot.FindPropertyRelative("allowedFrom");
            var onEnter    = slot.FindPropertyRelative("onEnter");
            var onExit     = slot.FindPropertyRelative("onExit");

            var   defAsset = defProp.objectReferenceValue as StateDefinition;
            string header  = defAsset != null ? defAsset.displayName : $"(unassigned slot {index})";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header row ─────────────────────────────────────────
            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);

            const float BTN_W  = 24f;
            const float GAP    = 2f;
            const float FOLD_W = 20f;

            float right      = headerRect.xMax;
            Rect  removeRect = new Rect(right - BTN_W,           headerRect.y, BTN_W, headerRect.height);
            Rect  downRect   = new Rect(right - BTN_W*2 - GAP,   headerRect.y, BTN_W, headerRect.height);
            Rect  upRect     = new Rect(right - BTN_W*3 - GAP*2, headerRect.y, BTN_W, headerRect.height);
            Rect  foldRect   = new Rect(headerRect.x, headerRect.y, FOLD_W, headerRect.height);
            Rect  labelRect  = new Rect(headerRect.x + FOLD_W + GAP, headerRect.y,
                                        upRect.x - headerRect.x - FOLD_W - GAP * 2, headerRect.height);

            if (GUI.Button(foldRect, _foldouts[index] ? "▼" : "▶", EditorStyles.label))
                _foldouts[index] = !_foldouts[index];

            EditorGUI.LabelField(labelRect, header, EditorStyles.boldLabel);

            GUI.enabled = index > 0;
            if (GUI.Button(upRect,   "▲")) { _pendingMoveFrom = index; _pendingMoveTo = index - 1; }

            GUI.enabled = index < _slotsProp.arraySize - 1;
            if (GUI.Button(downRect, "▼")) { _pendingMoveFrom = index; _pendingMoveTo = index + 1; }

            GUI.enabled = true;
            GUI.color   = new Color(1f, 0.5f, 0.5f);
            if (GUI.Button(removeRect, "✕")) _pendingRemove = index;
            GUI.color = Color.white;

            // ── Body ───────────────────────────────────────────────
            if (_foldouts[index])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);

                // Asset picker
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(defProp, new GUIContent("State Definition"));
                bool defChanged = EditorGUI.EndChangeCheck();

                // Only show the rest once an asset is assigned
                if (defProp.objectReferenceValue != null)
                {
                    EditorGUILayout.Space(4);

                    // Allowed From — checklist of other assigned slots
                    EditorGUILayout.LabelField(
                        new GUIContent("Allowed From",
                            "Which states may transition into this one.\nEmpty = any state allowed."),
                        EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    bool anyOther = false;

                    for (int other = 0; other < _slotsProp.arraySize; other++)
                    {
                        if (other == index) continue;

                        var otherDefProp = _slotsProp.GetArrayElementAtIndex(other)
                                                     .FindPropertyRelative("definition");
                        var otherDef = otherDefProp.objectReferenceValue as StateDefinition;
                        if (otherDef == null) continue;

                        anyOther = true;
                        bool included = ObjectListContains(allowProp, otherDef);
                        bool toggled  = EditorGUILayout.Toggle(otherDef.displayName, included);

                        if (toggled != included)
                        {
                            if (toggled) AddObjectToList(allowProp, otherDef);
                            else         RemoveObjectFromList(allowProp, otherDef);
                        }
                    }

                    if (!anyOther)
                        EditorGUILayout.HelpBox(
                            "Assign State Definitions to other slots to configure transition rules.",
                            MessageType.None);

                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(onEnter, new GUIContent("On Enter"));
                    EditorGUILayout.PropertyField(onExit,  new GUIContent("On Exit"));

                    // ── Play-mode test buttons ──────────────────────
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Test", EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginHorizontal();

                    bool inPlayMode = Application.isPlaying;
                    var  gsm        = GameStateManager.instance;

                    GUI.enabled = inPlayMode && gsm != null;

                    GUI.color = new Color(0.5f, 1f, 0.6f);
                    if (GUILayout.Button("Force Enter", GUILayout.Height(22)))
                        gsm.ForceState(defAsset);

                    GUI.color = new Color(1f, 0.85f, 0.4f);
                    if (GUILayout.Button("Force Exit to Idle", GUILayout.Height(22)))
                        gsm.ResetToIdleState();

                    GUI.color   = Color.white;
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();

                    if (!inPlayMode)
                    {
                        EditorGUILayout.HelpBox("Enter Play Mode to use test buttons.", MessageType.None);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Assign a State Definition asset to configure this slot.\n" +
                        "Create one via right-click → MetaFrame / State Definition.",
                        MessageType.Info);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── List Helpers ───────────────────────────────────────────────────────────

        private static bool ObjectListContains(SerializedProperty list, UnityEngine.Object obj)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == obj) return true;
            return false;
        }

        private static void AddObjectToList(SerializedProperty list, UnityEngine.Object obj)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
        }

        private static void RemoveObjectFromList(SerializedProperty list, UnityEngine.Object obj)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == obj)
                {
                    list.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }
    }

    // ── StateIndex Property Drawer ────────────────────────────────────────────────

    [CustomPropertyDrawer(typeof(StateIndexAttribute))]
    public class StateIndexDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var gsm = GetGSM();
            if (gsm == null)
            {
                EditorGUI.HelpBox(position, "No GameStateManager in scene.", MessageType.Warning);
                return;
            }

            string[] names = gsm.StateNameArray();
            if (names.Length == 0)
            {
                EditorGUI.HelpBox(position, "No states defined in GameStateManager.", MessageType.Warning);
                return;
            }

            int current = Mathf.Clamp(property.intValue, 0, names.Length - 1);
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUI.Popup(position, label.text, current, names);
            if (EditorGUI.EndChangeCheck())
                property.intValue = chosen;
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
    }

#endif
}