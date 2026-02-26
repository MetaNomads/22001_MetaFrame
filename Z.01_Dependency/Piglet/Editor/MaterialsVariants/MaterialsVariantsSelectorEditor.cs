using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// A custom Inspector for the MaterialsVariantsSelector component,
/// that allows the user to select the active materials variant via a
/// drop-down menu (rather typing the materials variant index into a
/// text field).
/// </summary>
[CustomEditor(typeof(MaterialsVariantsSelector))]
public class MaterialsVariantsSelectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var variants = (MaterialsVariantsSelector)target;

        // Note:
        //
        // For some reason, the drop-down menu doesn't work correctly
        // in the Inspector panel for a prefab asset on disk, so I
        // just gray it out in this case. (The problem is that
        // changing the drop-down selection does not immediately
        // update the model's materials, and I don't understand why.)
        //
        // It is still possible to change the materials variant for a
        // prefab asset by double-clicking it in the Project Window
        // to open it in "Prefab Mode", and then changing the
        // drop-down selection from there. In Prefab Mode,
        // `EditorUtility.IsPersistent(variants.gameObject)` returns
        // false, presumably because the Editor is now working on a
        // temporary in-memory copy of the prefab.
        //
        // Implementing a custom Inspector GUI that works correctly
        // with prefab assets turns out to be surprisingly
        // complicated. For example, see the discussion in this
        // thread:
        // https://forum.unity.com/threads/custom-editor-new-prefabs-how-to-setup-overrides.675310/
        //
        // Nonetheless, I felt that being able to interactively preview
        // materials variants in the Editor was worth the trouble.

        var isPrefabAsset = EditorUtility.IsPersistent(variants.gameObject);
        GUI.enabled = !isPrefabAsset;

        var label = new GUIContent("Materials Variants");

        EditorGUI.BeginChangeCheck();

        var variantIndex = EditorGUILayout.Popup(
            label, variants.VariantIndex, variants.VariantNames);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(variants, "Change Materials Variant");

            variants.VariantIndex = variantIndex;

            // Note:
            //
            // This method call is needed to ensure that changes to a
            // prefab instance are saved before entering Play Mode.
            //
            // It appears to be a harmless no-op in the case that the
            // target GameObject is not a prefab instance.

            PrefabUtility.RecordPrefabInstancePropertyModifications(variants);
        }

        GUI.enabled = true;
    }
}