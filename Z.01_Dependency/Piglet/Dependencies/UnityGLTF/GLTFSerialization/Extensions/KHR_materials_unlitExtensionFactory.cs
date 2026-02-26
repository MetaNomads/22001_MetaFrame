using Piglet.Newtonsoft.Json.Linq;

namespace Piglet.GLTF.Schema
{
	/// <summary>
	/// Parses JSON for the KHR_materials_unlit glTF extension
	/// and loads it into an equivalent C# class (KHR_materials_unlitExtension).
	///
	/// Unlike the factory classes for other glTF extensions (e.g.
	/// `KHR_materials_pbrSpecularGlossiness`, this class does not
	/// need to any JSON parsing, because the `KHR_materials_unlit` does
	/// not contain any JSON properties. It is merely a tag object that
	/// indicates that the material should be rendered as unlit.
	///
    /// For details/examples of the KHR_materials_unlit extension, see:
    /// https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_unlit
	/// </summary>
    public class KHR_materials_unlitExtensionFactory : ExtensionFactory
    {
		public const string EXTENSION_NAME = "KHR_materials_unlit";

		public KHR_materials_unlitExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		/// <summary>
		/// Parse JSON for KHR_materials_unlit glTF extension and load it
		/// into an equivalent C# class (`KHR_materials_unlitExtension`).
		/// </summary>
		/// <param name="root">
		/// C# object hierarchy mirroring entire JSON content of glTF file
		/// (everything except extensions).
		/// </param>
		/// <param name="extensionToken">
		/// Root JSON token for KHR_materials_unlit extension.
		/// </param>
		/// <returns>
		/// C# object (`KHR_materials_unlitExtension`) that mirrors
		/// the JSON object for the KHR_materials_unlit extension.
		/// </returns>
        public override Extension Deserialize(GLTFRoot root, JProperty extensionToken)
        {
	        return new KHR_materials_unlitExtension();
        }
    }
}