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
    public class InteractableManager : MonoBehaviour
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

        /// <summary>Add a new event at runtime.</summary>
        public void AddEvent(InteractableEvent newEvent)
        {
            if (newEvent == null) return;
            events.Add(newEvent);
        }

        /// <summary>Remove an event by name at runtime.</summary>
        public bool RemoveEvent(string eventName)
        {
            int removed = events.RemoveAll(e =>
                string.Equals(e.eventName, eventName, StringComparison.OrdinalIgnoreCase));
            return removed > 0;
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
    [CustomEditor(typeof(InteractableManager))]
    public class InteractableManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (InteractableManager)target;
            var names   = manager.GetEventNames();

            if (names.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trigger Events", EditorStyles.boldLabel);

            string active = manager.GetActiveEventName();

            // Rebuild event list via serialized object to read persistent flag
            var so = new SerializedObject(manager);
            var eventsProp = so.FindProperty("events");

            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                bool isActive = name == active;
                bool isPersistent = eventsProp.GetArrayElementAtIndex(i).FindPropertyRelative("persistent").boolValue;

                EditorGUILayout.BeginHorizontal();

                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;

                string label = isActive ? $"● {name}" : $"○ {name}";
                if (isPersistent) label += " [P]";
                EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(180));

                if (GUILayout.Button($"Trigger Enter {name}"))
                    manager.TriggerEnter(name);

                // Only show Exit button if not persistent (persistent events are excluded from auto-exit)
                GUI.enabled = !isPersistent || isActive;
                if (GUILayout.Button($"Trigger Exit {name}"))
                    manager.TriggerExit(name);
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }

            if (active != null)
            {
                EditorGUILayout.Space();

                bool activePersistent = eventsProp
                    .GetArrayElementAtIndex(manager.GetEventNames().IndexOf(active))
                    .FindPropertyRelative("persistent").boolValue;

                if (activePersistent)
                {
                    EditorGUILayout.HelpBox($"{active} is persistent — it won't auto-exit. Use Force Exit to override.", MessageType.Info);
                    if (GUILayout.Button($"Force Exit Current ({active})"))
                        manager.ForceExitCurrent();
                }
                else
                {
                    if (GUILayout.Button($"Exit Current ({active})"))
                        manager.TriggerExitCurrent();
                }
            }
        }
    }
#endif
}