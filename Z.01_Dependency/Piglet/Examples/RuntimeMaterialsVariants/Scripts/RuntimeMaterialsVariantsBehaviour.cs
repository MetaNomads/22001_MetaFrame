using Piglet;
using UnityEngine;

/// <summary>
/// This MonoBehaviour provides a minimal example of switching
/// materials variants at runtime, for glTF models that use the
/// `KHR_materials_variants` extension.
/// </summary>
public class RuntimeMaterialsVariantsBehaviour : MonoBehaviour
{
    /// <summary>
    /// The currently running glTF import task.
    /// </summary>
    private GltfImportTask _task;

    /// <summary>
    /// Root GameObject of the imported glTF model.
    /// </summary>
    private GameObject _model;

    /// <summary>
    /// MonoBehaviour on the root GameObject of the imported
    /// model, which allows the user to select the active materials
    /// variant.
    /// </summary>
    private MaterialsVariantsSelector _variantsSelector;

    /// <summary>
    /// Unity callback that is invoked before the first frame.
    /// Create the glTF import task and register a callback
    /// method to be invoked when the glTF import completes.
    /// </summary>
    void Start()
    {
        // The default size of the shoe model is too small.
        //
        // Uniformly scale the model such that the longest
        // dimension of its world-space axis-aligned bounding
        // box becomes 4.0 units.

        var importOptions = new GltfImportOptions();
        importOptions.AutoScale = true;
        importOptions.AutoScaleSize = 4.0f;

        // Note: To import a local .gltf/.glb/.zip file, you may
        // instead pass an absolute file path to GetImportTask
        // (e.g. "C:/Users/Joe/Desktop/piggleston.glb"), or a byte[]
        // array containing the raw byte content of the file.

        _task = RuntimeGltfImporter.GetImportTask(
            "https://awesomesaucelabs.github.io/piglet-webgl-demo/StreamingAssets/shoe.glb",
            importOptions);

        // Method to be invoked when the glTF import successfully
        // completes.

        _task.OnCompleted = OnComplete;
    }

    /// <summary>
    /// Callback that is invoked by the glTF import task
    /// after it has successfully completed.
    /// </summary>
    /// <param name="importedModel">
    /// the root GameObject of the imported glTF model
    /// </param>
    private void OnComplete(GameObject importedModel)
    {
        _model = importedModel;
        _variantsSelector = _model.GetComponent<MaterialsVariantsSelector>();
   }

    /// <summary>
    /// Unity callback that is invoked after every frame.
    /// Here we call MoveNext() to advance execution
    /// of the glTF import task. Once the model has been successfully
    /// imported, we auto-spin the model about the y-axis.
    /// </summary>
    void Update()
    {
        // advance execution of glTF import task
        _task.MoveNext();

        // spin model about y-axis
        if (_model != null)
            _model.transform.Rotate(0.0f, 0.25f, 0.0f);
    }

    void OnGUI()
    {
        // Add some buttons along the top of the screen, which allow
        // the user to select the active materials variant.
        //
        // Note: `_variantsSelector` will be null until the model has
        // been successfully imported.

        if (_variantsSelector != null)
        {
            _variantsSelector.VariantIndex = GUI.Toolbar(
                new Rect(25, 25, 500, 30),
                _variantsSelector.VariantIndex,
                _variantsSelector.VariantNames);
        }
    }
}
