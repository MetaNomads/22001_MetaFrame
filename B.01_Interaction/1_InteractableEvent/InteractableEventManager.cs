using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaNomads.Interaction
{
    [Serializable]
    public class InteractableEvent
    {
        public string eventName = "New Event";
        [Tooltip("If true, this event will not be exited when another event is entered.")]
        public bool persistent = false;
        public UnityEvent onEnter;
        public UnityEvent onExit;
    }

    // A manager to trigger named interactable events from code or inspector
    public class InteractableEventManager : MonoBehaviour
    {
        public string Interactable;

        [Header("Custom Events")]
        [SerializeField] private List<InteractableEvent> events = new List<InteractableEvent>();

        // Tracks whichever event is currently in Enter state (-1 = none)
        private int activeEventIndex = -1;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Trigger Enter by name. Auto-exits any currently active non-persistent event first.</summary>
        public void TriggerEnter(string eventName)
        {
            var entry = Find(eventName);
            if (entry == null) return;
            TriggerEnter(events.IndexOf(entry));
        }

        /// <summary>Trigger Exit by name.</summary>
        public void TriggerExit(string eventName)
        {
            var entry = Find(eventName);
            if (entry == null) return;
            TriggerExit(events.IndexOf(entry));
        }

        /// <summary>Trigger Enter by index. Auto-exits any currently active non-persistent event first.</summary>
        public void TriggerEnter(int index)
        {
            var entry = Find(index);
            if (entry == null) return;

            // Force exit the currently active event only if it is not persistent
            if (activeEventIndex != -1 && activeEventIndex != index)
            {
                var active = events[activeEventIndex];
                if (!active.persistent)
                {
                    active.onExit?.Invoke();
                    activeEventIndex = -1;
                }
            }

            activeEventIndex = index;
            entry.onEnter?.Invoke();
        }

        /// <summary>Trigger Exit by index.</summary>
        public void TriggerExit(int index)
        {
            var entry = Find(index);
            if (entry == null) return;

            if (activeEventIndex == index)
                activeEventIndex = -1;

            entry.onExit?.Invoke();
        }

        /// <summary>Exits the currently active event only if it is not persistent.</summary>
        public void TriggerExitCurrent()
        {
            if (activeEventIndex == -1) return;
            if (events[activeEventIndex].persistent) return;
            TriggerExit(activeEventIndex);
        }

        /// <summary>Force exits the currently active event regardless of persistence.</summary>
        public void ForceExitCurrent()
        {
            if (activeEventIndex == -1) return;
            TriggerExit(activeEventIndex);
        }

        /// <summary>Returns all registered event names.</summary>
        public List<string> GetEventNames()
        {
            var names = new List<string>();
            foreach (var e in events) names.Add(e.eventName);
            return names;
        }

        /// <summary>Returns the name of the currently active (entered) event, or null if none.</summary>
        public string GetActiveEventName()
        {
            if (activeEventIndex == -1) return null;
            return events[activeEventIndex].eventName;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private InteractableEvent Find(string eventName)
        {
            var entry = events.Find(e =>
                string.Equals(e.eventName, eventName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                Debug.LogWarning($"[InteractableManager] No event found with name: '{eventName}'", this);

            return entry;
        }

        private InteractableEvent Find(int index)
        {
            if (index < 0 || index >= events.Count)
            {
                Debug.LogWarning($"[InteractableManager] Event index {index} is out of range.", this);
                return null;
            }

            return events[index];
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InteractableEventManager))]
    public class InteractableStateManagerEditor : Editor
    {
        private SerializedProperty interactableProp;
        private SerializedProperty eventsProp;

        private readonly List<bool> foldouts = new();

        private void OnEnable()
        {
            interactableProp = serializedObject.FindProperty("Interactable");
            eventsProp = serializedObject.FindProperty("events");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(interactableProp);

            DrawEventBlocks();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEventBlocks()
        {
            while (foldouts.Count < eventsProp.arraySize)
                foldouts.Add(true);

            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                var eventProp = eventsProp.GetArrayElementAtIndex(i);

                var nameProp = eventProp.FindPropertyRelative("eventName");
                var persistentProp = eventProp.FindPropertyRelative("persistent");
                var onEnterProp = eventProp.FindPropertyRelative("onEnter");
                var onExitProp = eventProp.FindPropertyRelative("onExit");

                string header = string.IsNullOrWhiteSpace(nameProp.stringValue)
                    ? "New Event"
                    : nameProp.stringValue;

                if (persistentProp.boolValue)
                    header += " [P]";

                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.BeginHorizontal();

                foldouts[i] = EditorGUILayout.Foldout(
                    foldouts[i],
                    header,
                    true,
                    EditorStyles.foldoutHeader
                );

                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    eventsProp.DeleteArrayElementAtIndex(i);
                    foldouts.RemoveAt(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                if (foldouts[i])
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(nameProp, new GUIContent("Event Name"));
                    EditorGUILayout.PropertyField(persistentProp);

                    EditorGUILayout.Space(4);

                    EditorGUILayout.PropertyField(onEnterProp);
                    EditorGUILayout.PropertyField(onExitProp);

                    // Debug buttons in play mode
                    GUI.enabled = Application.isPlaying;

                    EditorGUILayout.BeginHorizontal();

                    GUI.color = new Color(0.4f, 0.9f, 0.5f);
                    if (GUILayout.Button("▶ Enter"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ((InteractableEventManager)target).TriggerEnter(i);
                    }

                    GUI.color = new Color(1f, 0.7f, 0.4f);
                    if (GUILayout.Button("◀ Exit"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ((InteractableEventManager)target).TriggerExit(i);
                    }

                    GUI.color = Color.white;

                    EditorGUILayout.EndHorizontal();

                    GUI.enabled = true;

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            EditorGUILayout.Space();

            GUI.color = new Color(0.4f, 0.9f, 0.5f);

            if (GUILayout.Button("+ Add Event"))
            {
                eventsProp.InsertArrayElementAtIndex(eventsProp.arraySize);
                foldouts.Add(true);
            }

            GUI.color = Color.white;
        }
    }

#endif
}