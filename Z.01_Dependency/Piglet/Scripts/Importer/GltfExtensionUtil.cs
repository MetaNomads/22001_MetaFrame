using Piglet.GLTF.Schema;
using UnityEngine;
using Material = Piglet.GLTF.Schema.Material;
using Texture = Piglet.GLTF.Schema.Texture;

namespace Piglet {

    /// <summary>
    /// Provides methods for checking if particular
    /// glTF extensions (e.g. `KHR_draco_mesh_compression`)
    /// are supported by Piglet.
    /// </summary>
    public static class GltfExtensionUtil
    {
        /// <summary>
        /// <para>
        /// Return true if the `KHR_draco_mesh_compression` extension
        /// is supported, or false otherwise. The
        /// `KHR_draco_mesh_compression` extension is supported if either the
        /// "Draco for Unity" or "DracoUnity" package is installed,
        /// and the installed package version is compatible with the
        /// current version of Unity.
        /// </para>
        /// <para>
        /// Note: The "Draco for Unity" and "DracoUnity" packages are
        /// mostly the same code. "Draco for Unity" is the Unity
        /// team's fork of the original "DracoUnity" package
        /// by @atteneder. At the time of writing (May 1st, 2024), the
        /// main difference between the two packages is that "Draco
        /// for Unity" supports WebGL builds across a wider range of
        /// Unity versions (Unity 2020.3 or newer), whereas the
        /// original "DracoUnity" package requires the user to be
        /// careful about installing a "DracoUnity" version that is
        /// compatible with the current Unity version, otherwise
        /// compile errors will occur during WebGL builds. For
        /// further details about this issue, see the discussion at
        /// https://github.com/atteneder/DracoUnity/issues/55.
        /// </para>
        /// </summary>
        public static bool IsDracoSupported()
        {
            // Version compatibility table:
            //
            // Unity Version          "DracoUnity" Version  "Draco for Unity" Version
            // -------------          ---------------------  ------------------------
            // 2019.2 or older        *not supported*        *not supported*
            // 2019.3 through 2020.2  1.1.0 through 3.3.2    *not supported*
            // 2020.3 through 2021.1  1.1.0 through 3.3.2    5.0.2 through 5.1.3
            // 2021.2 or 2022.1       4.0.0 through 4.1.0    5.0.2 through 5.1.3
            // 2022.2 or newer        *not supported*        5.0.2 through 5.1.3
            //
            // Note 1: The main reason for this version-matching
            // insanity is that the WASM binaries in the Draco for
            // Unity / DracoUnity package need to be compiled with the
            // same version of `emscripten` that Unity uses to perform
            // WebGL build, otherwise compile errors will result. The
            // new "Draco for Unity" package (which is the Unity team's
            // fork of the original "DracoUnity" package) solves the
            // problem by bundling multiple versions of the WASM
            // binaries, each compiled with a different version of
            // `emscripten`.
            //
            // Note 2: Even though DracoUnity is not officially
            // supported in Unity 2022.2+, we still permit it here,
            // because in practice it works fine for all platforms
            // except WebGL. Instead, we issue a warning at build
            // time, advising the user to install the "Draco for
            // Unity" package instead. This build-time check is
            // implemented in
            // `Assets/Piglet/Editor/BuildPreprocessor/PigletBuildPreprocessor.cs`.

#if UNITY_2022_2_OR_NEWER
    #if DRACO_FOR_UNITY_5_0_0_OR_NEWER
            return true;
    #elif DRACO_UNITY_4_0_0_OR_NEWER
            Debug.LogWarning(
                "DracoUnity isn't officially supported in Unity 2022.2+, "+
                "and you are likely to encounter compile errors during WebGL builds. You "+
                "are recommended to uninstall the DracoUnity package and install the new"+
                "\"Draco for Unity\" package instead. See \"Installing Draco for Unity\" "+
                "in the Piglet manual for further details.");
            return true;
    #else
            return false;
    #endif
#elif UNITY_2021_2_OR_NEWER
    #if DRACO_FOR_UNITY_5_0_0_OR_NEWER || DRACO_UNITY_4_0_0_OR_NEWER
            return true;
    #else
            return false;
    #endif
#elif UNITY_2020_3_OR_NEWER
    #if DRACO_FOR_UNITY_5_0_0_OR_NEWER || (DRACO_UNITY_1_4_0_OR_NEWER && !DRACO_UNITY_4_0_0_OR_NEWER)
            return true;
    #else
            return false;
    #endif
#elif UNITY_2019_3_OR_NEWER
    #if DRACO_UNITY_1_4_0_OR_NEWER && !DRACO_UNITY_4_0_0_OR_NEWER
            return true;
    #else
            return false;
    #endif
#else
            return false;
#endif
        }

        /// <summary>
        /// <para>
        /// Return true if the `KHR_materials_basisu` extension
        /// is supported, or false otherwise. The
        /// `KHR_materials_basisu` extension is supported if either the
        /// "KTX for Unity" or "KtxUnity" package is installed,
        /// and the installed package version is compatible with the
        /// current version of Unity.
        /// </para>
        /// <para>
        /// Note: The "KTX for Unity" and "KtxUnity" packages are
        /// mostly the same code. "KTX for Unity" is the Unity
        /// team's fork of the original "KtxUnity" package
        /// by @atteneder. At the time of writing (May 1st, 2024), the
        /// main difference between the two packages is that "KTX
        /// for Unity" supports WebGL builds across a wider range of
        /// Unity versions (Unity 2020.3 or newer), whereas the
        /// original "KtxUnity" package requires the user to be
        /// careful about installing a "KtxUnity" version that is
        /// compatible with the current Unity version, in order
        /// to avoid compile errors during WebGL builds. For
        /// further details, see the related discussion
        /// about DracoUnity, which has the same issue as KtxUnity:
        /// https://github.com/atteneder/DracoUnity/issues/55.
        /// </para>
        /// </summary>
        public static bool IsKtx2Supported()
        {
            // Version compatibility table:
            //
            // Unity Version          "KtxUnity" Version     "KTX for Unity" Version
            // -------------          ---------------------  ------------------------
            // 2019.2 or older        *not supported*        *not supported*
            // 2019.3 through 2020.2  0.9.1 through 1.1.2    *not supported*
            // 2020.3 through 2021.1  0.9.1 through 1.1.2    3.2.2 through 3.4.0
            // 2021.2 or 2022.1       2.0.0 through 2.2.3    3.2.2 through 3.4.0
            // 2022.2 or newer        *not supported*        3.2.2 through 3.4.0
            //
            // Note 1: The main reason for this version-matching
            // insanity is that the WASM binaries in the KTX for
            // Unity / KtxUnity package need to be compiled with the
            // same version of `emscripten` that Unity uses to perform
            // WebGL build, otherwise compile errors will result. The
            // new "KTX for Unity" package (which is the Unity team's
            // fork of the original "KtxUnity" package) solves the
            // problem by bundling multiple versions of the WASM
            // binaries, each compiled with a different version of
            // `emscripten`.
            //
            // Note 2: Even though KtxUnity is not officially
            // supported in Unity 2022.2+, we still permit it here,
            // because in practice it works fine for all platforms
            // except WebGL. Instead, we issue a warning at build
            // time, advising the user to install the "KTX for
            // Unity" package instead. This build-time check is
            // implemented in
            // `Assets/Piglet/Editor/BuildPreprocessor/PigletBuildPreprocessor.cs`.
#if UNITY_2022_2_OR_NEWER
    #if KTX_FOR_UNITY_3_2_2_OR_NEWER
            return true;
    #elif KTX_UNITY_2_0_0_OR_NEWER
            Debug.LogWarning(
                "KtxUnity isn't officially supported in Unity 2022.2+, "+
                "and you are likely to encounter compile errors during WebGL builds. You "+
                "are recommended to uninstall the KtxUnity package and install the new"+
                "\"KTX for Unity\" package instead. See \"Installing KTX for Unity\" "+
                "in the Piglet manual for further details.");
            return true;
    #else
            return false;
    #endif
#elif UNITY_2021_2_OR_NEWER
    #if KTX_FOR_UNITY_3_2_2_OR_NEWER || KTX_UNITY_2_0_0_OR_NEWER
            return true;
    #else
            return false;
    #endif
#elif UNITY_2020_3_OR_NEWER
    #if KTX_FOR_UNITY_3_2_2_OR_NEWER || (KTX_UNITY_0_9_1_OR_NEWER && !KTX_UNITY_2_0_0_OR_NEWER)
            return true;
    #else
            return false;
    #endif
#elif UNITY_2019_3_OR_NEWER
    #if KTX_UNITY_0_9_1_OR_NEWER && !KTX_UNITY_2_0_0_OR_NEWER
            return true;
    #else
            return false;
    #endif
#else
            return false;
#endif
        }

        /// <summary>
        /// <para>
        /// Return true if the given glTF extension is supported
        /// by Piglet, or false otherwise.
        /// </para>
        /// <para>
        /// For a complete list of glTF extensions, see:
        /// https://github.com/KhronosGroup/glTF/tree/main/extensions
        /// </para>
        /// </summary>
        public static bool IsExtensionSupported(string extension)
        {
            switch (extension)
            {
                case "KHR_materials_pbrSpecularGlossiness":
                case "KHR_materials_unlit":
                case "KHR_texture_transform":
                case "KHR_materials_variants":
                    return true;

                case "KHR_draco_mesh_compression":
                    return IsDracoSupported();

                case "KHR_texture_basisu":
                    return IsKtx2Supported();

                default:
                    return false;
            }
        }

        /// <summary>
        /// Return the `KHR_texture_basisu` glTF extension for
        /// the given texture, if present. Otherwise, return null.
        /// </summary>
        public static KHR_texture_basisuExtension GetKtx2Extension(
            Texture texture)
        {
            Extension extension;
            if (texture.Extensions != null && texture.Extensions.TryGetValue(
                "KHR_texture_basisu", out extension))
            {
                return (KHR_texture_basisuExtension)extension;
            }
            return null;
        }

        /// <summary>
        /// Return the `KHR_materials_unlit` glTF extension for
        /// a material, if present. Otherwise, return null.
        /// </summary>
        public static KHR_materials_unlitExtension
            GetUnlitExtension(Material def)
        {
            Extension extension;
            if (def.Extensions != null && def.Extensions.TryGetValue(
                "KHR_materials_unlit", out extension))
            {
                return (KHR_materials_unlitExtension)extension;
            }
            return null;
        }

        /// <summary>
        /// Return the `KHR_materials_pbrSpecularGlossiness` glTF extension
        /// for a material, if present. Otherwise, return null.
        /// </summary>
        /// <param name="def"></param>
        /// <returns></returns>
        public static KHR_materials_pbrSpecularGlossinessExtension
            GetSpecularGlossinessExtension(Material def)
        {
            Extension extension;
            if (def.Extensions != null && def.Extensions.TryGetValue(
                "KHR_materials_pbrSpecularGlossiness", out extension))
            {
                return (KHR_materials_pbrSpecularGlossinessExtension)extension;
            }
            return null;
        }

        /// <summary>
        /// Return the `KHR_texture_transform` glTF extension for
        /// a texture, if present. Otherwise, return null.
        /// </summary>
        public static KHR_texture_transformExtension
            GetTextureTransformExtension(TextureInfo textureInfo)
        {
            Extension extension;
            if (textureInfo.Extensions != null && textureInfo.Extensions.TryGetValue(
                "KHR_texture_transform", out extension))
            {
                return (KHR_texture_transformExtension)extension;
            }
            return null;
        }

        /// <summary>
        /// Return the `KHR_draco_mesh_compression` glTF extension for
        /// a mesh primitive, if present. Otherwise, return null.
        /// </summary>
        public static KHR_draco_mesh_compressionExtension
            GetDracoExtension(MeshPrimitive meshPrimitive)
        {
            Extension extension;
            if (meshPrimitive.Extensions != null
                && meshPrimitive.Extensions.TryGetValue(
                    "KHR_draco_mesh_compression", out extension))
            {
                return (KHR_draco_mesh_compressionExtension)extension;
            }
            return null;
        }

        /// <summary>
        /// Return the `KHR_materials_variants` glTF extension at the
        /// root level of the glTF file, if present. Otherwise, return
        /// null.
        /// </summary>
        public static KHR_materials_variantsExtension
            GetMaterialsVariantsExtension(GLTFRoot root)
        {
            Extension extension;
            if (root.Extensions != null && root.Extensions.TryGetValue(
                "KHR_materials_variants", out extension))
            {
                return (KHR_materials_variantsExtension)extension;
            }

            return null;
        }

        /// <summary>
        /// Return the `KHR_materials_variants` glTF extension for the
        /// mesh primitive, if present. Otherwise, return null.
        /// </summary>
        public static KHR_materials_variantsExtension
            GetMaterialsVariantsExtension(MeshPrimitive meshPrimitive)
        {
            Extension extension;
            if (meshPrimitive.Extensions != null
                && meshPrimitive.Extensions.TryGetValue(
                    "KHR_materials_variants", out extension))
            {
                return (KHR_materials_variantsExtension)extension;
            }

            return null;
        }
    }
}