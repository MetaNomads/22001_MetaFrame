using System.Collections.Generic;
using UnityEngine;
using Mapping = Piglet.GLTF.Schema.KHR_materials_variantsExtension.Mapping;

/// <summary>
/// <para>
/// For glTF models that use the `KHR_materials_variants` extension,
/// this component controls the active materials variant for an
/// individual mesh primitive.
/// </para>
/// <para>
/// Normally, the end user will not need to interact with this
/// component directly. Instead, use the `MaterialsVariantsSelector`
/// component on the root GameObject of the model, to change the
/// active materials variant for all mesh primitives in the model at
/// the same time.
/// </para>
/// </summary>
public class MaterialsVariantsMappings : MonoBehaviour
{
    /// <summary>
    /// The complete list of materials that were loaded when
    /// importing the glTF model.
    /// </summary>
    public List<Material> Materials;

    /// <summary>
    /// The index of the default material used by this mesh primitive.
    /// This is the material that will be used if no materials variant
    /// is selected (i.e. the default state).
    /// </summary>
    public int DefaultMaterialIndex;

    /// <summary>
    /// Maps materials variants to materials, on a per-primitive basis.
    /// </summary>
    public Mapping[] Mappings;

    /// <summary>
    /// Select the active materials variant for this mesh primitive.
    /// </summary>
    /// <param name="variantIndex">
    /// The index of the materials variant in the glTF file.
    /// </param>
    public void SelectVariant(int variantIndex)
    {
        var meshRenderer = GetComponent<MeshRenderer>();

        foreach (var mapping in Mappings)
        {
            foreach (var variant in mapping.Variants)
            {
                if (variantIndex == variant)
                {
                    meshRenderer.material = Materials[mapping.Material];
                    return;
                }
            }
        }

        meshRenderer.material = Materials[DefaultMaterialIndex];
    }
}
