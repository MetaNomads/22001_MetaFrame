using System;
using Piglet.GLTF.Extensions;
using Piglet.Newtonsoft.Json.Linq;
using Mapping = Piglet.GLTF.Schema.KHR_materials_variantsExtension.Mapping;

namespace Piglet.GLTF.Schema
{
	/// <summary>
	/// Parses JSON data for the `KHR_materials_variants` glTF extension
	/// and loads it into an equivalent C# class (`KHR_materials_variantsExtension`).
	/// For details/examples of the `KHR_materials_variants` extension, see:
	/// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_variants/README.md
	/// </summary>
	public class KHR_materials_variantsExtensionFactory : ExtensionFactory
	{
		public const string EXTENSION_NAME = "KHR_materials_variants";

		public KHR_materials_variantsExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		/// <summary>
		/// Parse JSON for KHR_materials_variants glTF extension and load it
		/// into an equivalent C# class (`KHR_materials_variantsExtension`).
		/// </summary>
		/// <param name="root">
		/// C# object hierarchy mirroring entire JSON content of glTF file
		/// (everything except the glTF extensions).
		/// </param>
		/// <param name="extensionToken">
		/// Root JSON token for `KHR_materials_variants` extension.
		/// </param>
		/// <returns>
		/// C# object (`KHR_materials_variantsExtension`) mirroring
		/// JSON content of KHR_materials_variants extension.
		/// </returns>
		public override Extension Deserialize(GLTFRoot root, JProperty extensionToken)
		{
			if (extensionToken == null)
				return null;

			// Get the value of the "variants" JSON property, which is
			// used to declare the material variants as an ordered
			// list of human-readable names.
			//
			// This variants must be declared at the root of the glTF file.
			// Example from the `KHR_materials_variants` spec [1]:
			//
			// {
			//   "asset": {"version": "2.0", "generator": "Fancy 3D Tool" },
			//   "extensions": {
			//     "KHR_materials_variants": {
			//       "variants": [
			//         {"name": "Yellow Sneaker" },
			//         {"name": "Red Sneaker"    },
			//         {"name": "Black Sneaker"  },
			//         {"name": "Orange Sneaker" },
			//       ]
			//     }
			//   }
			// }
			//
			// [1]: https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_variants/README.md

			string[] variants = null;

			var variantsToken = extensionToken.Value["variants"];
			if (variantsToken != null)
			{
				var variantsArray = variantsToken as JArray;
				if (variantsArray == null)
				{
					throw new Exception(
						"KHR_materials_variants extension: 'variants' property must be an array of JSON objects");
				}

				variants = new string[variantsArray.Count + 1];

				variants[variantsArray.Count] = "default";

				for (var i = 0; i < variantsArray.Count; ++i)
				{
					var nameToken = variantsArray[i]["name"];
					if (nameToken == null)
					{
						throw new Exception(
							"KHR_materials_variants extension: 'name' property is missing from 'variants' array element");
					}

					variants[i] = nameToken.Value<string>();
				}
			}

			// Get the value of the "mappings" JSON property, which
			// is used on individual mesh primitives to define the
			// mapping from materials variants to material indices.
			//
			// Example from the `KHR_materials_variants` spec [1]:
			//
			// "meshes": [
			//   {
			//     "name": "shoelaces",
			//     "primitives": [
			//       {
			//         ...,
			//         "extensions": {
			//           "KHR_materials_variants" : {
			//             "mappings": [
			//               {
			//                 "material": 2,
			//                 "variants": [0, 3],
			//               },
			//               {
			//                 "material": 4,
			//                 "variants": [1],
			//               },
			//               {
			//                 "material": 5,
			//                 "variants": [2],
			//               },
			//             ],
			//           }
			//         }
			//       },
			//       // ... more primitives ...
			//     ]
			//   },
			//   // ... more meshes ...
			// ]
			//
			// [1]: https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_variants/README.md

			Mapping[] mappings = null;

			var mappingsToken = extensionToken.Value["mappings"];
			if (mappingsToken != null)
			{
				var mappingsArray = mappingsToken as JArray;
				if (mappingsArray == null)
				{
					throw new Exception(
						"KHR_materials_variants extension: 'mappings' property must be an array of JSON objects");
				}

				mappings = new Mapping[mappingsArray.Count];

				for (var i = 0; i < mappingsArray.Count; ++i)
				{
					var mappingObject = mappingsArray[i];

					var materialToken = mappingObject["material"];
					if (materialToken == null)
					{
						throw new Exception(
							"KHR_materials_variants extension: 'material' property is missing from 'mappings' array element");
					}

					Mapping mapping;

					mapping.Material = materialToken.DeserializeAsInt();

					var variantsArrayToken = mappingObject["variants"];
					var variantsArray = variantsArrayToken as JArray;
					if (variantsArray == null)
					{
						throw new Exception(
							"KHR_materials_variants extension: 'variants' property must be an array of integers");
					}

					mapping.Variants = new int[variantsArray.Count];

					for (var j = 0; j < variantsArray.Count; ++j)
					{
						mapping.Variants[j] = variantsArray[j].DeserializeAsInt();
					}

					mappings[i] = mapping;
				}
			}

			return new KHR_materials_variantsExtension(variants, mappings);
		}
	}
}