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

    [Tooltip("Uncheck to bypass all gates during development.")]
    [SerializeField] private bool requireGate = true;

    // =========================================================================
    // Step — always called by the physical button
    //
    //   CanProceed() is the single gate — it knows what each panel requires.
    //   Data is only recorded on Survey panels, not confirmation panels.
    // =========================================================================

    public void Step()
    {
        if (requireGate && surveyControl != null && !surveyControl.CanProceed())
            return;

        if (surveyControl != null && surveyControl.CurrentGate == SurveyControl.GateType.Survey)
        {
            surveyControl.PushToRecorder();
            recorder?.CaptureSurvey();
        }

        surveyControl?.ClearSelection();
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