using System;
using UnityEngine;

/// <summary>
/// A component that allows the user to select one of the materials
/// variants defined in the glTF file, either programmatically or in
/// the Editor via the Inspector. This component is automatically
/// attached to the root GameObject of the model if the the glTF file
/// uses the `KHR_materials_variants` extension.
/// </summary>
public class MaterialsVariantsSelector : MonoBehaviour
{
    /// <summary>
    /// An ordered array of materials variants names (e.g. "Yellow
    /// Sneaker", "Red Sneaker").
    /// </summary>
    [SerializeField, HideInInspector]
    public string[] VariantNames;

    /// <summary>
    /// The index of the currently selected materials variant.
    /// </summary>
    [SerializeField, HideInInspector]
    private int _variantIndex;

    /// <summary>
    /// <para>
    /// The index of the currently selected materials variant.
    /// </para>
    /// <para>
    /// Assigning a new value to this property will change the active
    /// materials variant. Valid values are in the range [0,
    /// VariantNames.Length - 1]. By convention, the maximum index
    /// (i.e. VariantNames.Length - 1) is always the special
    /// "default" element, which resets all materials to their default
    /// state.
    /// </para>
    /// </summary>
    public int VariantIndex
    {
        get
        {
            return _variantIndex;
        }

        set
        {
            SelectVariant(value);
        }
    }

    /// <summary>
    /// <para>
    /// Initialize the state of the `MaterialsVariantsSelector`
    /// component on the root GameObject of the glTF model.
    /// </para>
    /// <para>
    /// This method is automatically called by Piglet when importing a
    /// model, if the model uses the `KHR_materials_variants`
    /// extension. The end user will probably never need to call this
    /// method directly.
    /// </para>
    /// </summary>
    /// <param name="variantNames">
    /// An ordered list of materials variants names (e.g. "Yellow
    /// Sneaker", "Red Sneaker").
    /// </param>
    public void Init(string[] variantNames)
    {
        VariantNames = variantNames;

        // Note: By convention, the last element of the variants array
        // is always the special "default" element. This corresponds
        // to the initial state of the model when it is first loaded,
        // and no materials variant has been selected.

        _variantIndex = VariantNames.Length - 1;
    }

    /// <summary>
    /// Select the materials variant by its index in the glTF file.
    /// </summary>
    private void SelectVariant(int variantIndex)
    {
        if (variantIndex < 0 || variantIndex >= VariantNames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(variantIndex));
        }

        // Avoid unnecessary work if the variant is already selected.
        if (variantIndex == _variantIndex)
            return;

        foreach (var mappings in GetComponentsInChildren<MaterialsVariantsMappings>())
        {
            mappings.SelectVariant(variantIndex);
        }

        _variantIndex = variantIndex;
    }

    /// <summary>
    /// <para>
    /// Reset all materials in the model to their initial default
    /// state, when no materials variant has been selected.
    /// </para>
    /// <para>
    /// Note: This is a convenience method that merely sets
    /// `VariantIndex = VariantNames.Length - 1`. VariantNames.Length
    /// - 1 always corresponds to the special "default" element, which
    /// resets all materials in the model to their default state.
    /// </para>
    /// </summary>
    public void ResetMaterials()
    {
        SelectVariant(VariantNames.Length - 1);
    }
}
