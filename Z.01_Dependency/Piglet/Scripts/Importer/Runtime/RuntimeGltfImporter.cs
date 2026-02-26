using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using Animation = Piglet.GLTF.Schema.Animation;

namespace Piglet {
    public class RuntimeGltfImporter : GltfImporter
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RuntimeGltfImporter(Uri uri, byte[] data,
            GltfImportOptions importOptions,
            ProgressCallback progressCallback)
            : base(uri, data, importOptions, progressCallback)
        {}

        /// <summary>
        /// Returns an asynchronous glTF import task. The import task is
        /// advanced by calling MoveNext(), typically from the
        /// Update() method of a MonoBehaviour.
        ///
        /// At least one of the `uri` and `data` arguments must be
        /// non-null. If both are non-null, no data is read from
        /// the `uri` argument and `uri` is only used for resolving relative
        /// URIs that appear in the file provided by the `data` argument.
        /// In the case that the `data` argument is a .zip file, the `uri`
        /// argument is completely ignored and any relative URIs are resolved
        /// relative the location of the .gltf/.glb file inside the zip archive.
        /// </summary>
        /// <param name="uri">
        /// The URI of the input .gltf/.glb/.zip file.
        /// </param>
        /// <param name="data">
        /// The raw bytes of the input .gltf/.glb/.zip file.
        /// </param>
        /// <param name="importOptions">
        /// Options controlling glTF importer behaviour (e.g. should
        /// the imported model be automatically scaled to a certain size?).
        /// </param>
        /// <returns>
        /// an asyncronous glTF import task (GltfImportTask)
        /// </returns>
        static public GltfImportTask GetImportTask(Uri uri,
            byte[] data, GltfImportOptions importOptions = null)
        {
            GltfImportTask importTask = new GltfImportTask();

            if (importOptions == null)
                importOptions = new GltfImportOptions();

            RuntimeGltfImporter importer
                = new RuntimeGltfImporter(uri, data, importOptions,
                    (step, completed, total) =>
                        importTask.OnProgress?.Invoke(step, completed, total));

            importTask.AddTask("ReadUri", importer.ReadUri());
            importTask.AddTask("ParseFile", importer.ParseFile());
            importTask.AddTask("CheckRequiredGltfExtensions",
                importer.CheckRequiredGltfExtensions());
            importTask.AddTask("LoadBuffers", importer.LoadBuffers());
            importTask.AddTask("LoadTextures", importer.LoadTextures());
            importTask.AddTask("LoadMaterials", importer.LoadMaterials());
            importTask.AddTask("LoadMeshes", importer.LoadMeshes());
            importTask.AddTask("LoadScene", importer.LoadScene());
            importTask.AddTask("LoadMorphTargets", importer.LoadMorphTargets());
            importTask.AddTask("LoadSkins", importer.LoadSkins());
            importTask.AddTask("ScaleModel", importer.ScaleModel());
            importTask.AddTask("LoadAnimations", importer.LoadAnimations());
            importTask.AddTask("AddAnimationListToSceneObject",
                importer.AddAnimationComponentsToSceneObject);
            importTask.AddTask("ShowModel", importer.ShowModel());

            // note: the final subtask must return the
            // root GameObject for the imported model.
            importTask.AddTask("GetSceneObjectEnum", importer.GetSceneObjectEnum());

            // callbacks to clean up any imported game objects
            // when the user aborts the import or an exception
            // occurs
            importTask.OnAborted += importer.Clear;
            importTask.OnException += _ => importer.Clear();

            return importTask;
        }

        /// <summary>
        /// Returns an asynchronous glTF import task. The import task is
        /// advanced by calling MoveNext(), typically from the
        /// Update() callback of a MonoBehaviour.
        /// </summary>
        /// <param name="uri">
        /// The URI for the input .gltf/.glb/.zip file, which
        /// may be an HTTP(S) URL or a file URI
        /// (e.g. "file:///C:/Users/Joe/Desktop/model.glb").
        /// </param>
        /// <param name="importOptions">
        /// Options controlling glTF importer behaviour (e.g. should
        /// the imported model be automatically scaled to a certain size?).
        /// </param>
        /// <returns>
        /// an asyncronous glTF import task (GltfImportTask)
        /// </returns>
        public static GltfImportTask GetImportTask(
            Uri uri, GltfImportOptions importOptions = null)
        {
            return GetImportTask(uri, null, importOptions);
        }

        /// <summary>
        /// Returns an asynchronous glTF import task. The import task is
        /// advanced by calling MoveNext(), typically from the
        /// Update() callback of a MonoBehaviour.
        /// </summary>
        /// <param name="uri">
        /// The absolute URI for the input .gltf/.glb/.zip file,
        /// which may be an HTTP(S) URL or an absolute file path
        /// (e.g. "C:/Users/Joe/Desktop/model.glb").
        /// </param>
        /// <param name="importOptions">
        /// Options controlling glTF importer behaviour (e.g. should
        /// the imported model be automatically scaled to a certain size?).
        /// </param>
        /// <returns>
        /// an asyncronous glTF import task (GltfImportTask)
        /// </returns>
        public static GltfImportTask GetImportTask(
            string uri, GltfImportOptions importOptions = null)
        {
            return GetImportTask(new Uri(uri), null, importOptions);
        }

        /// <summary>
        /// Returns an asynchronous glTF import task. The import task is
        /// advanced by calling MoveNext(), typically from the
        /// Update() callback of a MonoBehaviour.
        /// </summary>
        /// <param name="data">
        /// the raw bytes of the input .gltf/.glb/.zip file
        /// </param>
        /// <param name="importOptions">
        /// Options controlling glTF importer behaviour (e.g. should
        /// the imported model be automatically scaled to a certain size?).
        /// </param>
        /// <returns>
        /// an asyncronous glTF import task (GltfImportTask)
        /// </returns>
        public static GltfImportTask GetImportTask(
            byte[] data, GltfImportOptions importOptions = null)
        {
            return GetImportTask(null, data, importOptions);
        }
    }
}
