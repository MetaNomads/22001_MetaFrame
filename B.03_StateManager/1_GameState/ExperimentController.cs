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
                // Repaint continuously so the label stays up to date.
                EditorUtility.SetDirty(target);
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
            GUI.color = new Color(0.5f, 1f, 0.6f);

            if (GUILayout.Button("▶  Step", GUILayout.Height(28)))
                controller.Step();

            GUI.color = Color.white;

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