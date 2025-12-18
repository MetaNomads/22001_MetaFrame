using UnityEngine;
using UnityEditor;
using System.Collections;


namespace MetaFrame.Utilities.Editor
{
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

    public class InspectorReadOnlyAttribute : PropertyAttribute
    {
    }
}