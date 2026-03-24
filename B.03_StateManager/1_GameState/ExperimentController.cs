using UnityEngine;
using MetaFrame.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{

public class ExperimentController : MonoBehaviour
{
    [SerializeField] private ExperimentSequencer    sequencer;
    [SerializeField] private ExperimentDataRecorder recorder;
    [SerializeField] private SurveyControl          surveyControl;

    [Tooltip("When unchecked, confirmation gates (tutorial / break panels) are bypassed for development.\n" +
             "The survey gate (detection + confidence + plausibility) is ALWAYS enforced regardless of this flag.")]
    [SerializeField] private bool requireGate = true;

    // =========================================================================
    // Step — always called by the physical button
    //
    // Gate logic:
    //   • Survey panels  — gate is ALWAYS enforced; incomplete data must never
    //                      be recorded regardless of the requireGate flag.
    //   • Other panels   — gate is enforced only when requireGate is true.
    //
    // ClearSelection is only called after we have confirmed we can proceed,
    // so a blocked step never wipes the participant's current answers.
    // =========================================================================

    public void Step()
    {
        if (surveyControl != null)
        {
            bool isSurvey = surveyControl.CurrentGate == SurveyControl.GateType.Survey;

            // Survey gate: always checked — requireGate does not bypass it.
            // Other gates: only checked when requireGate is true.
            if (isSurvey || requireGate)
            {
                if (!surveyControl.CanProceed())
                    return;
            }

            // We are cleared to proceed — record data before clearing UI state.
            if (isSurvey)
            {
                surveyControl.PushToRecorder();
                recorder?.CaptureSurvey();
            }

            surveyControl.ClearSelection();
        }

        sequencer.Advance();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ExperimentController))]
public class ExperimentControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var  controller = (ExperimentController)target;
        bool inPlayMode = Application.isPlaying;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
        GUI.enabled = inPlayMode;
        GUI.color   = new Color(0.5f, 1f, 0.6f);

        if (GUILayout.Button("▶  Step", GUILayout.Height(28)))
            controller.Step();

        GUI.color   = Color.white;
        GUI.enabled = true;

        if (!inPlayMode)
            EditorGUILayout.HelpBox("Enter Play Mode to use controls.", MessageType.None);
    }
}
#endif

} // namespace MetaFrame.State