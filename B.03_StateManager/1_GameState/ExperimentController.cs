using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{

public class ExperimentController : MonoBehaviour
{
    [SerializeField] private ExperimentSequencer sequencer;

    public void Step() => sequencer.Advance();
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

        GUI.color = new Color(0.5f, 1f, 0.6f);
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