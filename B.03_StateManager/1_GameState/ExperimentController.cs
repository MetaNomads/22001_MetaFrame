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

    private void OnEnable()  { }
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