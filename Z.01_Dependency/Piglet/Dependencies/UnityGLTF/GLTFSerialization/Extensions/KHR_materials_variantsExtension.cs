using Piglet.Newtonsoft.Json.Linq;

namespace Piglet.GLTF.Schema
{
    /// <summary>
    /// C# class mirroring the JSON content of the `KHR_materials_variants`
    /// glTF extension. For details/examples about this glTF extension, see:
    /// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_variants/README.md
    /// </summary>
    public class KHR_materials_variantsExtension : Extension
    {
        /// <summary>
        /// Declares the materials variants as a list of names (e.g.
        /// "Yellow Sneaker", "Red Sneaker"). This array directly
        /// mirrors the `variants` array in the JSON content.
        /// </summary>
        public readonly string[] Variants;

        /// <summary>
        /// Specifies which material variants map to a given
        /// material index, on a per-mesh-primitive basis.
        /// </summary>
        [System.Serializable]
        public struct Mapping
        {
            public int Material;
            public int[] Variants;
        }

        /// <summary>
        /// Maps materials variants to material indices, on a
        /// per-mesh-primitive basis.
        /// </summary>
        public readonly Mapping[] Mappings;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="variants">
        /// Declares the available material variants as a list of
        /// names.
        /// </param>
        /// <param name="mappings">
        /// Maps variants to materials on a per-mesh-primitive basis.
        /// </param>
        public KHR_materials_variantsExtension(
            string[] variants, Mapping[] mappings)
        {
            Variants = variants;
            Mappings = mappings;
        }

        public JProperty Serialize()
        {
            throw new System.NotImplementedException();
        }
    }
}