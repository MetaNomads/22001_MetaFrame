using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    // ── Classification Axes ───────────────────────────────────────────────────────

    public enum AnomalyDomain
    {
        Normal,
        Temporal,
        Spatial,
        Auditory,
        Visual,
        Tactile,
    }

    public enum AnomalyType
    {
        Normal,
        Metaphysical,
        Nomological,
        Conventional,
    }

    // ── AnomalyDefinition ─────────────────────────────────────────────────────────

    [CreateAssetMenu(menuName = "Anomaly/Anomaly Definition", fileName = "Anomaly_New")]
    public class AnomalyDefinition : ScriptableObject
    {
        [HideInInspector] public string      id;
        [HideInInspector] public string      anomalyName;
        [HideInInspector] public AnomalyDomain domain;
        [HideInInspector] public AnomalyType   type;
        [HideInInspector] public string      description;

        public override string ToString() =>
            string.IsNullOrEmpty(anomalyName) ? id : anomalyName;
    }

    // ── Editor ────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [CustomEditor(typeof(AnomalyDefinition))]
    public class AnomalyDefinitionEditor : Editor
    {
        private static readonly string[] DomainAbbr = { "NRM", "TMP", "SPA", "AUD", "VIS", "TAC" };
        private static readonly string[] TypeAbbr   = { "NRM", "MET", "NOM", "CON" };

        private SerializedProperty _id;
        private SerializedProperty _anomalyName;
        private SerializedProperty _domain;
        private SerializedProperty _type;
        private SerializedProperty _description;

        private string _idNumberInput = "";

        private void OnEnable()
        {
            _id          = serializedObject.FindProperty("id");
            _anomalyName = serializedObject.FindProperty("anomalyName");
            _domain      = serializedObject.FindProperty("domain");
            _type        = serializedObject.FindProperty("type");
            _description = serializedObject.FindProperty("description");

            // Extract number from existing ID on load
            string prefix = GetPrefix();
            string current = _id.stringValue;
            if (!string.IsNullOrEmpty(current) && current.StartsWith(prefix))
                _idNumberInput = current.Substring(prefix.Length);
            else
                _idNumberInput = "";
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Classification (drives the prefix) ────────────────
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_domain, new GUIContent("Domain"));
            EditorGUILayout.PropertyField(_type,   new GUIContent("Type"));
            bool classChanged = EditorGUI.EndChangeCheck();
            if (classChanged) serializedObject.ApplyModifiedProperties();

            // ── ID row: [locked prefix] [editable number] ─────────
            string prefix = GetPrefix();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("ID");

            // Locked prefix
            GUI.enabled = false;
            EditorGUILayout.TextField(prefix, GUILayout.Width(90));
            GUI.enabled = true;

            // Editable number
            EditorGUI.BeginChangeCheck();
            _idNumberInput = EditorGUILayout.TextField(_idNumberInput);
            if (EditorGUI.EndChangeCheck() || classChanged)
            {
                _id.stringValue = string.IsNullOrWhiteSpace(_idNumberInput)
                    ? ""
                    : $"{prefix}{_idNumberInput.Trim()}";
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.EndHorizontal();

            // Validation
            string fullId = _id.stringValue;
            if (string.IsNullOrEmpty(fullId))
            {
                EditorGUILayout.HelpBox("Enter a number or identifier after the prefix.", MessageType.Warning);
            }
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnomalyDefinition"))
                {
                    string path  = AssetDatabase.GUIDToAssetPath(guid);
                    var    other = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(path);
                    if (other == null || other == target) continue;
                    if (other.id == fullId)
                    {
                        EditorGUILayout.HelpBox(
                            $"ID \"{fullId}\" is already used by \"{path}\".",
                            MessageType.Error);
                        break;
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_anomalyName, new GUIContent("Display Name"));

            // ── Description ────────────────────────────────────────

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Description");
            var textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _description.stringValue = EditorGUILayout.TextArea(
                _description.stringValue,
                textAreaStyle,
                GUILayout.MinHeight(48));

            serializedObject.ApplyModifiedProperties();
        }

        private string GetPrefix()
        {
            string domainAbbr = DomainAbbr[_domain.intValue];
            string typeAbbr   = TypeAbbr[_type.intValue];
            return $"{domainAbbr}_{typeAbbr}_";
        }
    }
#endif
}