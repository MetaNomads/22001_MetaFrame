using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{

    public class ExperimentController : MonoBehaviour
    {
        [SerializeField] private ExperimentSequencer sequencer;

        public ExperimentSequencer Sequencer => sequencer;

        // =========================================================================
        // Step — generic "advance one beat" entry point.
        //
        // Returns void to match the original signature so existing UnityEvent
        // wiring in the inspector (state onEnter/onExit, button onClick, etc.)
        // keeps working. Survey gating + recording now happens in
        // SurveyControl.OnContinuePressed() before it calls Step().
        //
        // Safe to call from anywhere — outside a survey period this just
        // advances the GSM/sequencer normally, the same way it always did.
        // =========================================================================

        public void Step()
        {
            if (sequencer == null)
            {
                Debug.LogError("[ExperimentController] No sequencer assigned.");
                return;
            }
            sequencer.Advance();
        }

        // =========================================================================
        // ForceStep — same as Step but ignores GSM allowedFrom transition rules.
        //
        // Use this when the experiment needs to advance regardless of which
        // interaction state the player is currently in (e.g. skipping a trial
        // mid-task, or advancing from at_source / in_hand without completing
        // the physical sequence).
        //
        // Implementation: temporarily clears all allowedFrom lists on every GSM
        // slot so RequestTransition() accepts any current state, then restores
        // them after Advance() returns. All sequencer events (OnTrialEnded,
        // OnTrialBegan, etc.) still fire normally — only the guard check is
        // skipped.
        // =========================================================================

        public void ForceStep()
        {
            AdvanceForced();
        }

        // FIX (S-5): re-entrancy guard. AdvanceForced mutates the GSM's
        // allowedFrom lists in place, runs Advance(), and restores them in a
        // finally block. While Advance() is running, the lists are EMPTY —
        // any nested code path (a UnityEvent listener that calls Step() or
        // ForceStep() from inside an onEnter/onExit) would observe this empty
        // state and silently bypass guards that should still apply to it.
        // The simplest defense is to refuse the nested call.
        private bool _inAdvanceForced;

        private bool AdvanceForced()
        {
            if (sequencer == null) return false;

            // FIX (S-5): refuse nested AdvanceForced/Step from inside an
            // already-running ForceStep. Letting it through would corrupt
            // the slots' allowedFrom contents and/or run Advance() twice
            // with overlapping state mutations.
            if (_inAdvanceForced)
            {
                Debug.LogWarning(
                    "[ExperimentController] Nested ForceStep / AdvanceForced call " +
                    "detected (likely from a UnityEvent listener fired by Advance()'s " +
                    "transitions). Ignored — call ForceStep again on the next frame " +
                    "if the second advance is intentional.");
                return false;
            }

            var gsm = sequencer.GSM;
            if (gsm == null) return sequencer.Advance();

            // Save and clear all allowedFrom constraints so every RequestTransition
            // call inside Advance() succeeds regardless of current GSM state.
            var slots = gsm.Slots;
            var saved = new System.Collections.Generic.List<StateDefinition>[slots.Count];

            for (int i = 0; i < slots.Count; i++)
            {
                saved[i] = new System.Collections.Generic.List<StateDefinition>(slots[i].allowedFrom);
                slots[i].allowedFrom.Clear();
            }

            bool result;
            _inAdvanceForced = true;
            try { result = sequencer.Advance(); }
            finally
            {
                // Always restore — even if Advance() throws.
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].allowedFrom.Clear();
                    slots[i].allowedFrom.AddRange(saved[i]);
                }
                _inAdvanceForced = false;
            }

            return result;
        }

        // =========================================================================
        // Session info — readable string for the editor label
        // =========================================================================

        public string GetCurrentSessionInfo()
        {
            if (sequencer == null) return "No sequencer assigned.";

            var session = sequencer.CurrentSession;
            if (session == null) return "No session active — call InitExperiment() first.";

            string gsmState = sequencer.GSM != null
                ? sequencer.GSM.CurrentStateDefinition?.displayName ?? "Unknown"
                : "No GSM";

            int sessionIdx = sequencer.CurrentSessionIndex;
            string label = session.sessionLabel;
            int trial = sequencer.CurrentTrialIndex + 1;
            int total = session.TrialCount;

            return $"GSM: {gsmState}   |   Session {sessionIdx}: {label}   |   Trial {trial}/{total}";
        }

        // =========================================================================
        // Jump helpers — called by the editor buttons
        // =========================================================================

        /// <summary>
        /// Forces the sequencer to the idle state immediately before the given
        /// 1-based session number. The very next Step() starts that session's
        /// first trial.
        /// </summary>
        public void JumpToSession(int sessionNumber)
        {
            sequencer?.JumpToSessionIdle(sessionNumber);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ExperimentController))]
    public class ExperimentControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (ExperimentController)target;
            bool inPlayMode = Application.isPlaying;

            // ── Current Session Info ──────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Session Info", EditorStyles.boldLabel);

            if (inPlayMode)
            {
                Repaint();
                string info = controller.GetCurrentSessionInfo();
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live session info.", MessageType.None);
            }

            // ── Step Control ──────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            GUI.enabled = inPlayMode;

            EditorGUILayout.BeginHorizontal();

            GUI.color = new Color(0.5f, 1f, 0.6f);
            if (GUILayout.Button("▶  Step", GUILayout.Height(28)))
                controller.Step();

            GUI.color = new Color(1f, 0.75f, 0.35f);
            if (GUILayout.Button("⚡  Force Step", GUILayout.Height(28)))
                controller.ForceStep();

            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Step / Force Step now advance the experiment only — they do NOT " +
                "gate-check or record survey answers. Survey logic lives in " +
                "SurveyControl.OnContinuePressed(). Force Step also bypasses GSM " +
                "allowedFrom rules.",
                MessageType.None);

            // ── Jump to Session ───────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Jump to Session (sets idle state before session)", EditorStyles.boldLabel);

            // Determine how many real sessions exist at runtime; fall back to 3 in edit mode.
            int sessionCount = 3;
            if (inPlayMode && ExperimentSequencer.instance != null)
            {
                var resolved = ExperimentSequencer.instance.resolvedSequences;
                // resolvedSequences[0] is the tutorial; real sessions start at index 1.
                sessionCount = resolved != null ? Mathf.Max(resolved.Count - 1, 1) : 3;
            }

            EditorGUILayout.BeginHorizontal();
            for (int s = 1; s <= sessionCount; s++)
            {
                int sessionNumber = s;

                string label = inPlayMode && ExperimentSequencer.instance != null
                               && ExperimentSequencer.instance.resolvedSequences != null
                               && s < ExperimentSequencer.instance.resolvedSequences.Count
                    ? $"Session {s}\n{ExperimentSequencer.instance.resolvedSequences[s].sessionLabel}"
                    : $"Session {s}";

                GUI.color = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button(label, GUILayout.Height(36)))
                    controller.JumpToSession(sessionNumber);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;

            if (!inPlayMode)
                EditorGUILayout.HelpBox("Enter Play Mode to use controls.", MessageType.None);
        }
    }
#endif

} // namespace MetaFrame.State
