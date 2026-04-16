using UnityEngine;
using MetaFrame.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{

    public class ExperimentController : MonoBehaviour
    {
        [SerializeField] private ExperimentSequencer sequencer;
        [SerializeField] private ExperimentDataRecorder recorder;
        [SerializeField] private SurveyControl surveyControl;

        private void OnEnable() { }
        private void OnDisable() { }

        // =========================================================================
        // Step — always called by the physical button
        //
        // Order:
        //   1. Gate check     — block if survey incomplete
        //   2. Push()         — snapshot toggle values while panel is visible
        //   3. Capture()      — commit to _currentTrial NOW, before Advance() fires
        //                       OnTrialEnded which nulls _currentTrial inside
        //                       ExperimentDataRecorder — after that CaptureSurvey()
        //                       silently exits because _currentTrial is null
        //   4. Advance()      — GSM transitions; OnTrialEnded fires inside here
        //   5. ClearSelection — reset toggles and visuals only on success
        // =========================================================================

        public void Step()
        {
            if (surveyControl != null && !surveyControl.CanProceed()) return;

            surveyControl?.Push();
            surveyControl?.Capture(recorder);

            if (!sequencer.Advance()) return;

            surveyControl?.ClearSelection();
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
            if (surveyControl != null && !surveyControl.CanProceed()) return;

            surveyControl?.Push();
            surveyControl?.Capture(recorder);

            if (!AdvanceForced()) return;

            surveyControl?.ClearSelection();
        }

        private bool AdvanceForced()
        {
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
            try { result = sequencer.Advance(); }
            finally
            {
                // Always restore — even if Advance() throws.
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].allowedFrom.Clear();
                    slots[i].allowedFrom.AddRange(saved[i]);
                }
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
            sequencer.JumpToSessionIdle(sessionNumber);
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
                // FIX: was EditorUtility.SetDirty(target) which marks the object as
                // modified on every inspector repaint, triggering undo system recording
                // every frame. Repaint() achieves the same continuous label refresh
                // without touching the serialization or undo stack.
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
                "Force Step advances regardless of the current GSM state — " +
                "use it to skip a trial mid-task (e.g. still at_source or in_hand).",
                inPlayMode ? MessageType.None : MessageType.None);

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
                // Capture loop variable for the lambda.
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