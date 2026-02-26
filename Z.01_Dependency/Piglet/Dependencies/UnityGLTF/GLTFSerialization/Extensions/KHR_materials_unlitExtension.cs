using Piglet.Newtonsoft.Json.Linq;

namespace Piglet.GLTF.Schema
{
    /// <summary>
    /// C# class representing the `KHR_materials_unlit` glTF extension.
    ///
    /// This C# class is empty because the `KHR_materials_unlit` extension
    /// does not contain any JSON properties. It is merely a tag object to
    /// indicate that the material should be rendered as unlit.
    ///
    /// For details/examples of the KHR_materials_unlit extension, see:
    /// https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_unlit
     /// </summary>
    public class KHR_materials_unlitExtension : Extension
    {
        public JProperty Serialize()
        {
            throw new System.NotImplementedException();
        }
    }
}