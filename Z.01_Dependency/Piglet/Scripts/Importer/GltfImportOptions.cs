using System;

namespace Piglet
{
    /// <summary>
    /// Options controlling the behavior of the glTF
    /// importer, such as automatically scaling the
    /// model to a given size.
    /// </summary>
    [Serializable]
    public class GltfImportOptions
    {
        /// <summary>
        /// Automatically show the model after a successful glTF
        /// import by calling SetActive(true) on the root GameObject
        /// (i.e. the scene object). Piglet hides the model
        /// during a glTF import so that the user never
        /// sees the model in a partially reconstructed state.
        /// An application may wish to set this option to false and
        /// handle calling SetActive(true) itself, so that it can
        /// perform additional processing before revealing the model
        /// (e.g. adding colliders).
        /// </summary>
        public bool ShowModelAfterImport;

        /// <summary>
        /// If true, automatically scale the imported glTF
        /// model to the size given by `AutoScaleSize`.
        /// More precisely, the model is uniformly scaled
        /// up or down in size such that the longest
        /// dimension of its world-space axis-aligned
        /// bounding box is equal to `AutoScaleSize`.
        /// </summary>
        public bool AutoScale;

        /// <summary>
        /// If `AutoScale` is true, the imported glTF model
        /// is automatically scaled to this size.
        /// More precisely, the model is uniformly scaled
        /// up or down in size such that the longest
        /// dimension of its world-space axis-aligned
        /// bounding box is equal to `AutoScaleSize`.
        /// </summary>
        public float AutoScaleSize;

        /// <summary>
        /// If true, import animations from glTF file
        /// as Unity AnimationClips.
        /// </summary>
        public bool ImportAnimations;

        /// <summary>
        /// Controls the type of animation clip that
        /// Piglet will create when importing animation
        /// clips (Legacy or Mecanim).
        /// </summary>
        public AnimationClipType AnimationClipType;

        /// <summary>
        /// <para>
        /// This option is true by default, and causes Piglet to call
        /// AnimationClip.EnsureQuaternionContinuity [1] after loading an
        /// animation clip.
        /// </para>
        /// <para>
        /// Most of the time `EnsureQuaternionContinuity` does the right thing,
        /// but in some circumstances it introduces unwanted wobble
        /// in rotation animations.
        /// </para>
        /// <para>
        /// This option is only relevant for glTF files that contain animations with
        /// rotations.
        /// </para>
        /// <para>
        /// [1]: https://docs.unity3d.com/ScriptReference/AnimationClip.EnsureQuaternionContinuity.html
        /// </para>
        /// </summary>
        public bool EnsureQuaternionContinuity;

        /// <summary>
        /// <para>
        /// Enable creation of mipmaps for PNG/JPG textures
        /// during runtime glTF imports.
        /// </para>
        /// <para>
        /// This option set to false by default because there is
        /// an extra performance cost for creating mipmaps for PNG/JPG
        /// during runtime glTF imports. The wallclock time for loading
        /// textures is approximately doubled, and stalling of the
        /// main Unity thread (i.e. frame rate drops) is more likely.
        /// </para>
        /// <para>
        /// This option has no effect for KTX2 textures. Mipmaps
        /// are always created for KTX2 textures, since KtxUnity is
        /// able to create them without any additional overhead.
        /// </para>
        /// <para>
        /// Likewise, this option has no effect on Editor glTF imports,
        /// because mipmaps are always created during Editor glTF
        /// imports.
        /// </para>
        /// </summary>
        public bool CreateMipmaps;

        /// The password to use, in the case that the
        /// input file is a password-protected zip file.
        /// If the input file is not a zip file,
        /// this field will be ignored.
        /// </summary>
        public string ZipPassword;

        /// <summary>
        /// Default constructor, which sets the default
        /// values for the various glTF import options.
        /// </summary>
        public GltfImportOptions()
        {
            ShowModelAfterImport = true;
            AutoScale = false;
            AutoScaleSize = 1f;
            ImportAnimations = true;
            AnimationClipType = AnimationClipType.Mecanim;
            EnsureQuaternionContinuity = true;
            CreateMipmaps = false;
            ZipPassword = null;
        }
    }
}
