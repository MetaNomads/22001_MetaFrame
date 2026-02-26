// Understanding the use of the many `DRACO_*_OR_NEWER` constants
// in this file can be confusing. Here are some reminders about
// what is going on:
//
// * The `DRACO_*_OR_NEWER` constants are "version defines" [1] that
// indicate which version of the "Draco for Unity" or "DracoUnity"
// package is installed in the current Unity project, if
// any. Installation of these packages is optional, and is only needed
// if you want to load glTF files that contain Draco-compressed
// meshes. The `DRACO_*_OR_NEWER` constants are defined in the
// `Assets/Piglet/Piglet.asmdef` file.
//
// * The "Draco for Unity" package (`com.unity.cloud.draco`) and the
// "DracoUnity" package (`com.atteneder.draco`) packages are mostly
// the same code. "Draco for Unity" is the Unity team's fork
// of @atteneder's original "DracoUnity" project, and since "Draco for
// Unity" is developed/hosted/maintained by Unity, it is the recommended
// option going forward. In fact, if you developing a WebGL app with
// Unity 2022 or newer, you *must* use "Draco for Unity", because
// WebGL builds with the original "DracoUnity" package will fail with
// compile errors [2].
//
// * Piglet will continue to support the legacy "DracoUnity" package
// indefinitely, so that people's existing projects don't break when
// upgrading to Piglet 1.3.9 or newer. But if you are starting a new
// project, you should use the "Draco for Unity" package instead,
// because it is officially supported by the Unity team.
//
// [1]: https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols
// [2]: https://github.com/atteneder/DracoUnity/issues/55

#if DRACO_FOR_UNITY_5_0_0_OR_NEWER || DRACO_UNITY_1_4_0_OR_NEWER

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Note: In "DracoUnity" 2.0.0, all classes were moved into the
// "Draco" namespace, whereas previously they lived in the default
// (global) namespace. We also need to include the `using Draco`
// line if we are using the "Draco for Unity" package, because "Draco
// for Unity" is a fork of "DracoUnity" 4.1.0.
#if DRACO_FOR_UNITY_5_0_0_OR_NEWER || DRACO_UNITY_2_0_0_OR_NEWER
using Draco;
#endif

namespace Piglet
{
	/// <summary>
	/// Utility methods for the DracoUnity package:
	/// https://github.com/atteneder/DracoUnity
	/// </summary>
	public static class DracoUnityUtil
	{
		/// <summary>
		/// Load a Unity mesh from Draco-compressed mesh data,
		/// using the DracoUnity package. This method
		/// is a simple wrapper around DracoUnity that hides API
		/// differences between DracoUnity 1.4.0 and DracoUnity 2.0.0+.
		/// </summary>
		/// <param name="dracoData">
		/// Byte array containing Draco-compressed mesh data.
		/// </param>
		/// <param name="weightsId">
		/// The Draco ID of the WEIGHTS_0 mesh attribute. This ID mapping
		/// is provided by the "attributes" object of the
		/// "KHR_draco_mesh_compression" glTF extension.
		/// </param>
		/// <param name="jointsId">
		/// The Draco ID for the JOINTS_0 mesh attribute. This ID mapping
		/// is provided by the "attributes" object of the
		/// "KHR_draco_mesh_compression" glTF extension.
		/// </param>
		/// <returns>
		/// IEnumerable over Unity Mesh.
		/// </returns>
		public static IEnumerable<Mesh> LoadDracoMesh(
			byte[] dracoData, int weightsId = -1, int jointsId = -1)
		{
			using (var dracoDataNative = new NativeArray<byte>(dracoData, Allocator.Persistent))
			{
				yield return null;

				foreach (var result in LoadDracoMesh(dracoDataNative, weightsId, jointsId))
					yield return result;
			}
		}

		/// <summary>
		/// Load a Unity mesh from Draco-compressed mesh data,
		/// using the DracoUnity package. This method
		/// is a simple wrapper around DracoUnity that hides API
		/// differences between DracoUnity 1.4.0 and DracoUnity 2.0.0+.
		/// </summary>
		/// <param name="dracoData">
		/// NativeArray<byte> containing the Draco-compressed mesh data.
		/// </param>
		/// <param name="weightsId">
		/// The Draco ID of the WEIGHTS_0 mesh attribute. This ID mapping
		/// is provided by the "attributes" object of the
		/// "KHR_draco_mesh_compression" glTF extension.
		/// </param>
		/// <param name="jointsId">
		/// The Draco ID for the JOINTS_0 mesh attribute. This ID mapping
		/// is provided by the "attributes" object of the
		/// "KHR_draco_mesh_compression" glTF extension.
		/// </param>
		/// <returns>
		/// IEnumerable over Unity Mesh.
		/// </returns>
		public static IEnumerable<Mesh> LoadDracoMesh(
			NativeArray<byte> dracoData, int weightsId = -1, int jointsId = -1)
		{
			Mesh mesh = null;

#if DRACO_FOR_UNITY_5_0_0_OR_NEWER

			// Notes:
			//
			// * `DecodeSettings.ConvertSpace` tells the "Draco for
			// Unity" decoder to translate the mesh vertex positions
			// from glTF's right-handed coordinates to Unity's
			// left-handed coordinates. The original "DracoUnity"
			// package did this coordinate translation by default, so
			// we want to enable in "Draco for Unity" too, so that the
			// Draco decoding behaves identically, regardless of
			// whether the "Draco for Unity" or "DracoUnity" package
			// is being used for the decoding.
			//
			// * `DecodeSettings.ForceUnityVertexLayout` makes some
			// kind of change to the memory layout of the decoded
			// vertex buffer. I don't know exactly what the difference
			// is, but the Unity documentation [1] says that I need to
			// enable the `ForceUnityVertexLayout` option in order to
			// use "blend shapes" (a.k.a. "morph targets") with the
			// decoded mesh. Since Piglet supports glTF models with
			// blend shapes / morph targets, I am following the
			// documentation's advice and enabling the option.
			//
			// [1]: https://docs.unity3d.com/Packages/com.unity.cloud.draco@5.1/api/Draco.DecodeSettings.html

			const DecodeSettings decodeSettings
				= DecodeSettings.ConvertSpace | DecodeSettings.ForceUnityVertexLayout;

			// Note:
			//
			// The Draco format has built-in vertex attribute types
			// for common attributes like `Position` and `Color`, but
			// it does not have built-in attribute types for
			// blend shape weights or blend shapes indices.
			//
			// Happily, Draco allows defining/embedding custom
			// attributes via "generic attributes". We just have to
			// supply the mapping, as we are doing below.
			//
			// For further info, see the following section of the
			// "Draco for Unity" documentation:
			// https://docs.unity3d.com/Packages/com.unity.cloud.draco@5.1/manual/use-case-decoding.html#attribute-assignment-via-draco-identifier

			var attributeIdMap = new Dictionary<VertexAttribute, int>();

			if (weightsId != -1)
				attributeIdMap.Add(VertexAttribute.BlendWeight, weightsId);

			if (jointsId != -1)
				attributeIdMap.Add(VertexAttribute.BlendIndices, jointsId);

			var task = DracoDecoder.DecodeMesh(dracoData, decodeSettings, attributeIdMap);

			while (!task.IsCompleted)
				yield return null;

			if (task.IsFaulted)
				throw task.Exception;

			mesh = task.Result;

#elif DRACO_UNITY_2_0_0_OR_NEWER

			// Note: DracoUnity 2.x/3.x provides a convenience
			// method for decoding a mesh directly from a C# byte[]
			// (rather than a NativeArray<byte>), but according to
			// my testing, that method does not work correctly.
			// See: https://github.com/atteneder/DracoUnity/issues/21

			var dracoLoader = new DracoMeshLoader();
			var task = dracoLoader.ConvertDracoMeshToUnity(
				dracoData, true, false, weightsId, jointsId);

			while (!task.IsCompleted)
				yield return null;

			if (task.IsFaulted)
				throw task.Exception;

			mesh = task.Result;

#else // DRACO_UNITY_1_4_0_OR_NEWER

			// Set up Draco decompression task and completion callback
			//
			// Note: `DecodeMeshSkinned` is my own modified version of
			// DracoUnity's main `DracoMeshLoader.DecodeMesh` method. The
			// only difference is that `DecodeMeshSkinned` accepts
			// additional `jointsId` and `weightsId` arguments, in
			// order to support skinned meshes.

			var dracoLoader = new DracoMeshLoader();
			dracoLoader.onMeshesLoaded += _mesh => mesh = _mesh;
			var dracoTask = dracoLoader.DecodeMeshSkinned(
				dracoData, jointsId, weightsId);

			// perform Draco decompression

			while (dracoTask.MoveNext())
				yield return null;

#endif

			yield return null;

			// Workaround: Fix DracoUnity bug where texture coords are
			// upside-down (vertically flipped).

			foreach (var _ in MeshUtil.VerticallyFlipTextureCoords(mesh))
				yield return null;

			// Correct for orientation changes introduced in DracoUnity 3.0.0.
			//
			// Starting in DracoUnity 3.0.0, DracoUnity changed how it
			// performs its conversion from right-handed coordinates (glTF) to
			// left-handed coordinates (Unity). Instead of negating the
			// Z-coordinate of each vertex, it now negates the X-coordinate instead.
			// This has the effect rotating the model by 180 degrees around the
			// Y-axis, so that the front face of the model now looks down the positive
			// Z-axis rather than the negative Z-axis. See [1] and [2] for further
			// explanation/visualization.
			//
			// Here we simply reverse the changes, so that Piglet continues to load
			// models with the same orientation as previously.
			//
			// Note: The same fix also needs to be applied to the "Draco for Unity"
			// package, because "Draco for Unity" is a fork of "DracoUnity"
			// 4.1.0. That is why I test for both `DRACO_FOR_UNITY_5_0_0_OR_NEWER`
			// and `DRACO_UNITY_3_0_0_OR_NEWER` below.
			//
			// [1]: https://github.com/atteneder/DracoUnity/releases/tag/v3.0.0
			// [2]: https://github.com/atteneder/glTFast/blob/main/Documentation%7E/glTFast.md#coordinate-system-conversion-change

#if DRACO_FOR_UNITY_5_0_0_OR_NEWER || DRACO_UNITY_3_0_0_OR_NEWER

			var vertices = mesh.vertices;
			var normals = mesh.normals;

			for (var i = 0; i < vertices.Length; ++i)
			{
				vertices[i] = new Vector3(-vertices[i].x, vertices[i].y, -vertices[i].z);
				normals[i] = new Vector3(-normals[i].x, normals[i].y, -normals[i].z);
			}

			mesh.vertices = vertices;
			mesh.normals = normals;

#endif

			yield return mesh;
		}
	}
}
#endif
