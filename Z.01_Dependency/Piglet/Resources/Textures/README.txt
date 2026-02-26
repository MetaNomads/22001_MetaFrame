RuntimeDefaultNormalTexture.png is used as the default normal texture
during runtime glTF imports, in cases where a glTF material does not
specify a normal texture. This texture is needed because the
channel-encoding of normal textures is different during Editor glTF imports
and runtime glTF imports.

During Editor imports, Piglet imports normal textures using
`AssetDatabase.ImportAsset`, and sets the texture type to "Normal
map". This settings causes Unity to encode the texture in DXT5nm
format, with the x coordinate in the alpha channel and the y
coordinate in the green channel. (The z-coordinate is never stored,
but since the normals are unit-length, the z coordinate can be
calculated from the x and y coordinates.) In the shader code
(e.g. [1]), the `UnpackNormal` function moves the x and y coordinates
back to the red/green channels and fills in the z coordinate on the
blue channel.

During runtime glTF imports, Piglet imports normal textures using
`UnityWebRequestTexture` and leaves the x/y/z coordinates in the
red/green/blue channels. Therefore there is a separate if/else case in
the shader code for runtime glTF imports, where the `UnpackNormal`
function is not used.

In cases where glTF materials don't specify a normal texture, the
shader would fall back to using the default value for `_normalTexture`,
which is "bump". However, the "bump" texture is encoded in DXT5nm, and
so it will not produce correct result when used during a runtime glTF import.
We solve this problem issue by explicitly setting the normal texture to
`RuntimeDefaultNormalTexture` during runtime imports in
`GltfImporter.LoadMaterial` [2].

Finally, you may notice that the sRGB flag is set in the texture
import settings for `RuntimeDefaultNormalTexture`, even though normal
textures are typically linear. In fact,
`RuntimeDefaultNormalTexture` *is* linear, but we need to
set the sRGB flag for a non-obvious reason, as explained by the following
chain of reasoning:

(1) Piglet uses `UnityWebRequestTexture` to load textures during
runtime glTF imports.
(2) `UnityWebRequestTexture` assumes that the input image is
sRGB-encoded, and performs an automatic sRGB -> linear conversion on
the color values.
(3) In order to correct the linear textures (including normal textures!),
the Piglet shaders contain code to reverse the unwanted sRGB -> linear
conversion performed by `UnityWebRequestTexture` in step (2).
(4) In order to use `RuntimeDefaultNormalTexture` with the Piglet
shaders, it needs to be encoded in the same way as normal textures
that are loaded from a glTF file, which is to say it needs to have an
incorrect sRGB -> linear conversion applied to it (just like
`UnityWebRequestTexture` would do). That is why we set the sRGB flag
on `RuntimeDefaultNormalTexture`.

[1]: Assets/Piglet/Resources/Shaders/Standard/MetallicRoughness.cginc
[2]: Assets/Piglet/Scripts/Importer/GltfImporter.cs
