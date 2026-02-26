// This shader is used to render a glTF material that uses the
// KHR_materials_unlit extension [1].
//
// [1]: https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_unlit

Shader "Piglet/UnlitMask"
{
    Properties
    {
        // The following property corresponds to the `material.doubleSided`
        // flag in glTF format [1].
        //
        // Possible values of this shader property are: 0 = Off, 1 = Front, 2 = Back
        // (see [2]). A value of 2 (Back) is used when `material.doubleSided` is false,
        // and 0 (Off) is used when `material.doubleSided` is true.
        //
        // [1]: https://www.khronos.org/registry/glTF/specs/2.0/glTF-2.0.html#_material_doublesided
        // [2]: https://docs.unity3d.com/ScriptReference/Rendering.CullMode.html

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Int) = 2

        // Corresponds to the `material.alphaCutoff` property described at:
        // https://www.khronos.org/registry/glTF/specs/2.0/glTF-2.0.html#_material_alphacutoff

        _alphaCutoff ("Alpha Cutoff", Float) = 0.0

        // The following properties correspond to metallic-roughness model properties
        // described at:
        //
        // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-pbrmetallicroughness

        _baseColorFactor ("Base Color Factor", Color) = (1, 1, 1, 1)
        _baseColorTexture ("Base Color Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" }
        Cull [_Cull]
        LOD 200

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Unlit alphatest:_alphaCutoff noambient
        #include "Unlit.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
