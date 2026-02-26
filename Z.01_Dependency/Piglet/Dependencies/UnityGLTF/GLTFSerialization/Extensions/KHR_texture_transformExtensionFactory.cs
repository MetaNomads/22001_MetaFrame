using Piglet.Newtonsoft.Json.Linq;
using Piglet.GLTF.Extensions;
using Piglet.GLTF.Math;

namespace Piglet.GLTF.Schema
{
	public class KHR_texture_transformExtensionFactory : ExtensionFactory
	{
		public const string EXTENSION_NAME = "KHR_texture_transform";
		public const string OFFSET = "offset";
		public const string SCALE = "scale";
		public const string TEXCOORD = "texCoord";

		public KHR_texture_transformExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		public override Extension Deserialize(GLTFRoot root, JProperty extensionToken)
		{
			Vector2 offset = new Vector2(KHR_texture_transformExtension.OFFSET_DEFAULT);
			Vector2 scale = new Vector2(KHR_texture_transformExtension.SCALE_DEFAULT);
			int texCoord = KHR_texture_transformExtension.TEXCOORD_DEFAULT;

			if (extensionToken != null)
			{
				JToken offsetToken = extensionToken.Value[OFFSET];
				offset = offsetToken != null ? offsetToken.DeserializeAsVector2() : offset;

				JToken scaleToken = extensionToken.Value[SCALE];
				scale = scaleToken != null ? scaleToken.DeserializeAsVector2() : scale;

				JToken texCoordToken = extensionToken.Value[TEXCOORD];
				texCoord = texCoordToken != null ? texCoordToken.DeserializeAsInt() : texCoord;
			}

			return new KHR_texture_transformExtension(offset, scale, texCoord);
		}
	}
}
