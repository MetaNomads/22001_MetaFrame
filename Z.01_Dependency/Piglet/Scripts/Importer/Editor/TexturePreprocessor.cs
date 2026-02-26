#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Piglet
{
    /// <summary>
    /// <para>
    /// This class uses a `AssetPostprocessor.OnPreprocessTexture` [1]
    /// callback to override Unity's default texture import settings.
    /// This ensures that texture assets created during an
    /// Editor glTF import correctly reflect the sampler settings from
    /// the glTF file (e.g. point or bilinear filtering mode?), as well as
    /// the settings provided in the TextureLoadingFlags (e.g. sRGB or linear color
    /// space?).
    /// </para>
    /// <para>
    /// Note: Unfortunately, Unity does not provide any straightforward way to
    /// override the default texture import settings while importing a
    /// texture with `AssetDatabase.ImportAsset`, which is why we need
    /// to use the `AssetPostprocessor.OnPreprocessTexture` callback.
    /// An alternative approach to override the default texture settings is to
    /// simply re-import the texture a second time using `TextureImporter.SaveImport`.
    /// That method works fine but slows down the Editor glTF import,
    /// since every texture needs to be imported twice.
    /// </para>
    /// <para>
    /// [1]: https://docs.unity3d.com/ScriptReference/AssetPostprocessor.OnPreprocessTexture.html
    /// </para>
    /// </summary>
    public class TexturePreprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Maps asset paths (e.g. "Assets/MyModel/Textures/grass.png") to
        /// texture importer settings.
        /// </summary>
        static public Dictionary<string, TextureImporterSettings> _textureImporterSettings;

        /// <summary>
        /// Specify custom texture import settings for the given asset path
        /// (e.g. "Assets/MyModel/Textures/grass.png"). In order for these settings
        /// to take effect, this method needs to be called before importing the
        /// texture with `AssetDatabase.ImportAsset`.
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="textureImporterSettings"></param>
        static public void SetTextureImporterSettings(
            string assetPath, TextureImporterSettings textureImporterSettings)
        {
            if (_textureImporterSettings == null)
                _textureImporterSettings = new Dictionary<string, TextureImporterSettings>();

            _textureImporterSettings[assetPath] = textureImporterSettings;
        }

        /// <summary>
        /// Clear the stored texture importer settings for all asset paths.
        /// </summary>
        static public void ClearTextureImporterSettings()
        {
            _textureImporterSettings?.Clear();
        }

        /// <summary>
        /// <para>
        /// Unity callback method that is invoked immediately before
        /// importing/re-importing a texture asset.
        /// </para>
        /// <para>
        /// We use this callback to override Unity's default texture importer
        /// settings, so that we can correctly respect the texture
        /// settings that were specified in the glTF file (e.g. point/bilinear
        /// sampling mode.
        /// </para>
        /// </summary>
        public void OnPreprocessTexture()
        {
            if (_textureImporterSettings == null ||
                !_textureImporterSettings.TryGetValue(assetPath,
                    out var textureImporterSettings))
            {
                return;
            }

            var textureImporter = assetImporter as TextureImporter;

            // Note:
            //
            // I originally wanted to override the texture import settings by doing
            // the following:
            //
            //     textureImporter.SetTextureImportSettings(textureImporterSettings);
            //
            // However, for some reason that causes texture imports to fail with
            // "Texture could not be created" errors. Probably there is some
            // setting in my `textureImporterSettings` object that is not properly
            // initialized.
            //
            // That's okay, though. It is much safer/sturdier to only change the specific
            // fields that I need to override, as done below.

            textureImporter.textureType = textureImporterSettings.textureType;
            textureImporter.sRGBTexture = textureImporterSettings.sRGBTexture;

            textureImporter.filterMode = textureImporterSettings.filterMode;
            textureImporter.wrapModeU = textureImporterSettings.wrapModeU;
            textureImporter.wrapModeV = textureImporterSettings.wrapModeV;
        }
    }
}
#endif