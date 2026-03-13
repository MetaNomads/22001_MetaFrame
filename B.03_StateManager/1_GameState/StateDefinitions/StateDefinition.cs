using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    [CreateAssetMenu(menuName = "MetaFrame/State Definition", fileName = "State_New")]
    public class StateDefinition : ScriptableObject
    {
        public string displayName = "New State";

        public override string ToString() =>
            string.IsNullOrEmpty(displayName) ? name : displayName;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(StateDefinition))]
    public class StateDefinitionEditor : Editor
    {
        private SerializedProperty _displayName;

        private void OnEnable() =>
            _displayName = serializedObject.FindProperty("displayName");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_displayName, new GUIContent("Display Name"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
