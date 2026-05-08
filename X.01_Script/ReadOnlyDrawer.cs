using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace MetaFrame.Utilities.Editor
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(InspectorReadOnlyAttribute))]
    public class InspectorReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif

    public class InspectorReadOnlyAttribute : PropertyAttribute
    {
    }
}