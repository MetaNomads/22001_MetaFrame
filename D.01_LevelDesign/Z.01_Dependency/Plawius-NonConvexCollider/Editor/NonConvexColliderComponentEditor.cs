using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Plawius.NonConvexCollider.Editor
{
    [CustomEditor(typeof(NonConvexColliderComponent))]
    [CanEditMultipleObjects]
    public class NonConvexColliderComponentEditor : UnityEditor.Editor
    {
        SerializedProperty m_algorithm;
        SerializedProperty m_commonParameters;
        SerializedProperty m_parameters;
        SerializedProperty m_coacdParameters;

        // Common Parameters (Shared by all algorithms)
        SerializedProperty m_common_maxConvexHulls;
        SerializedProperty m_common_maxVerticesPerHull;

        // V-HACD Parameters
        SerializedProperty m_parameters_resolution;
        SerializedProperty m_parameters_concavity;
        SerializedProperty m_parameters_planeDownsampling;
        SerializedProperty m_parameters_convexhullApproximation;
        SerializedProperty m_parameters_alpha;
        SerializedProperty m_parameters_beta;
        SerializedProperty m_parameters_pca;
        SerializedProperty m_parameters_mode;
        SerializedProperty m_parameters_minVolumePerCH;

        // CoACD Parameters
        SerializedProperty m_coacd_threshold;
        SerializedProperty m_coacd_preprocessMode;
        SerializedProperty m_coacd_prepResolution;
        SerializedProperty m_coacd_sampleResolution;
        SerializedProperty m_coacd_mctsNodes;
        SerializedProperty m_coacd_mctsIteration;
        SerializedProperty m_coacd_mctsMaxDepth;
        SerializedProperty m_coacd_pca;
        SerializedProperty m_coacd_merge;
        SerializedProperty m_coacd_extrude;
        SerializedProperty m_coacd_extrudeMargin;
        SerializedProperty m_coacd_seed;

        SerializedProperty m_asset;
        SerializedProperty m_isTrigger;
        SerializedProperty m_material;

        SerializedProperty m_colliders;
        SerializedProperty m_showColliders;

        [SerializeField] private int vhacdPresetIndex = 2;  // 0=Super Fast, 1=Fast, 2=Default, 3=Custom
        [SerializeField] private int coacdPresetIndex = 1;   // 0=Low, 1=Default, 2=High, 3=Custom

        readonly GUIContent[] modeOptions = {
            new GUIContent("Voxel"),
            new GUIContent("Tetrahedron")
        };

        readonly GUIContent[] vhacdPresetOptions = {
            new GUIContent("Super Fast"),
            new GUIContent("Fast"),
            new GUIContent("Default"),
            new GUIContent("Custom")
        };

        readonly GUIContent[] coacdPresetOptions = {
            new GUIContent("Low"),
            new GUIContent("Default"),
            new GUIContent("High"),
            new GUIContent("Custom")
        };

        void OnEnable()
        {

            m_algorithm = serializedObject.FindProperty("Algorithm");
            m_commonParameters = serializedObject.FindProperty("CommonParams");
            m_parameters = serializedObject.FindProperty("Params");
            m_coacdParameters = serializedObject.FindProperty("CoACDParams");

            // Common Parameters
            m_common_maxConvexHulls = m_commonParameters.FindPropertyRelative("maxConvexHulls");
            m_common_maxVerticesPerHull = m_commonParameters.FindPropertyRelative("maxVerticesPerHull");

            // V-HACD Parameters
            m_parameters.FindPropertyRelative("maxConvexHulls");
            m_parameters_resolution = m_parameters.FindPropertyRelative("resolution");
            m_parameters_concavity = m_parameters.FindPropertyRelative("concavity");
            m_parameters_planeDownsampling = m_parameters.FindPropertyRelative("planeDownsampling");
            m_parameters_convexhullApproximation = m_parameters.FindPropertyRelative("convexhullApproximation");
            m_parameters_alpha = m_parameters.FindPropertyRelative("alpha");
            m_parameters_beta = m_parameters.FindPropertyRelative("beta");
            m_parameters_pca = m_parameters.FindPropertyRelative("pca");
            m_parameters_mode = m_parameters.FindPropertyRelative("mode");
            m_parameters.FindPropertyRelative("maxNumVerticesPerCH");
            m_parameters_minVolumePerCH = m_parameters.FindPropertyRelative("minVolumePerCH");

            // CoACD Parameters
            m_coacd_threshold = m_coacdParameters.FindPropertyRelative("threshold");
            m_coacdParameters.FindPropertyRelative("maxConvexHull");
            m_coacd_preprocessMode = m_coacdParameters.FindPropertyRelative("preprocessMode");
            m_coacd_prepResolution = m_coacdParameters.FindPropertyRelative("prepResolution");
            m_coacd_sampleResolution = m_coacdParameters.FindPropertyRelative("sampleResolution");
            m_coacd_mctsNodes = m_coacdParameters.FindPropertyRelative("mctsNodes");
            m_coacd_mctsIteration = m_coacdParameters.FindPropertyRelative("mctsIteration");
            m_coacd_mctsMaxDepth = m_coacdParameters.FindPropertyRelative("mctsMaxDepth");
            m_coacd_pca = m_coacdParameters.FindPropertyRelative("pca");
            m_coacd_merge = m_coacdParameters.FindPropertyRelative("merge");
            m_coacd_extrude = m_coacdParameters.FindPropertyRelative("extrude");
            m_coacd_extrudeMargin = m_coacdParameters.FindPropertyRelative("extrudeMargin");
            m_coacd_seed = m_coacdParameters.FindPropertyRelative("seed");

            m_asset = serializedObject.FindProperty("m_colliderAsset");
            m_isTrigger = serializedObject.FindProperty("m_isTrigger");
            m_material   = serializedObject.FindProperty("m_material");

            m_colliders = serializedObject.FindProperty("m_colliders");
            m_showColliders = serializedObject.FindProperty("m_showColliders");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            try
            {
                var objects = serializedObject.targetObjects;
                
                var nonConvexColliders = objects.Select(o => o as NonConvexColliderComponent)
                                                .Where(c => c != null)
                                                .ToArray();
                
                var gameObjects = nonConvexColliders.Select(c => c.gameObject).ToArray();
                
                var renderingMeshThis = gameObjects.Select(go => go.GetComponent<MeshFilter>()) // can be only one one the gameobject
                                                   .Where(mf => mf != null)
                                                   .Select(mf => mf.sharedMesh)
                                                   .Where(mesh => mesh != null)
                                                   .ToArray();
                
                var meshColliderThis = gameObjects.Select(go => go.GetComponents<MeshCollider>())
                                                  .Where(mf => mf.Length == 1)
                                                  .ToArray();

                var renderingMeshes = gameObjects.SelectMany(go => go.GetComponentsInChildren<MeshFilter>())
                                                 .Where(mf => mf != null)
                                                 .Select(mf => mf.sharedMesh)
                                                 .Where(mesh => mesh != null)
                                                 .ToArray();

                var colliders = gameObjects.SelectMany(go => go.GetComponentsInChildren<Collider>())
                                           .Where(c => c != null)
                                           .ToArray();
                
                var meshColliders = gameObjects.SelectMany(go => go.GetComponentsInChildren<MeshCollider>())
                                               .ToArray();

                var enabledMeshColliders = meshColliders.Where(c => c != null && c.enabled && c.sharedMesh != null)
                                                        .ToArray();
                
                var disabledOrBrokenMeshColliders = meshColliders.Where(c => c != null && (!c.enabled || c.sharedMesh == null))
                                                                 .ToArray();
                var disabledColliders = colliders.Where(c => c != null && !c.enabled)
                                                 .ToArray();

                var disabledOrBrokenCollidersCount = disabledColliders.Length + disabledOrBrokenMeshColliders.Length;
                
                foreach (var coll in nonConvexColliders)
                {
                    if (coll.Params.resolution == 0)
                        coll.Params = Parameters.Default();    
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_asset, true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var coll in nonConvexColliders)
                    {
                        coll.SetPhysicsCollider(m_asset.objectReferenceValue as NonConvexColliderAsset);
                        EditorUtility.SetDirty(coll.gameObject);
                    }
                    serializedObject.UpdateIfRequiredOrScript();    // m_collider will be invalid
                }

                // Algorithm selection
                EditorGUILayout.Space();

                bool forceUpdatePreset = false;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_algorithm, new GUIContent("Decomposition Algorithm"));
                if (EditorGUI.EndChangeCheck())
                {
                    forceUpdatePreset = true;
                    
                    serializedObject.ApplyModifiedProperties();
                }

                // Show common parameters for all algorithms
                EditorGUILayout.Space();
                DrawCommonParametersGUI();
                EditorGUILayout.Space();

                var algorithm = (DecompositionAlgorithm)m_algorithm.enumValueIndex;

                // Show algorithm-specific parameters
                if (algorithm == DecompositionAlgorithm.VHACD)
                {
                    DrawParametersGUI(m_parameters, forceUpdatePreset);
                }
                else if (algorithm == DecompositionAlgorithm.CoACD_v2)
                {
                    DrawCoACDParametersGUI(m_coacdParameters, forceUpdatePreset);
                }

                using (var _ = new UnityExtensions.DisableGroup(renderingMeshThis.Length == 0))
                {
                    var title = "Generate using render mesh on this GameObject";
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var coll in nonConvexColliders)
                        {
                            UnityExtensions.GenerateCollidersFromRenderingMeshThis(coll);
                            EditorUtility.SetDirty(coll);
                        }
                        EditorGUIUtility.ExitGUI();
                    }
                }
                
                using (var _ = new UnityExtensions.DisableGroup(renderingMeshes.Length == 0))
                {
                    var title = "Generate using render meshes combined";
                    if (objects.Length == 1)
                    {
                        title += " (" + renderingMeshes.Length + " found)";
                    }
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var coll in nonConvexColliders)
                        {
                            UnityExtensions.GenerateCollidersFromRenderingMesh(coll);
                            EditorUtility.SetDirty(coll);
                        }
                        EditorGUIUtility.ExitGUI();
                    }
                }
                
                using (var _ = new UnityExtensions.DisableGroup(meshColliderThis.Length == 0))
                {
                    var title = "Generate using enabled MeshCollider on this GameObject";
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var coll in nonConvexColliders)
                        {
                            UnityExtensions.GenerateCollidersFromMeshCollidersThis(coll);
                            EditorUtility.SetDirty(coll);
                        }
                        EditorGUIUtility.ExitGUI();
                    }
                }

                using (var _ = new UnityExtensions.DisableGroup(enabledMeshColliders.Length == 0))
                {
                    var title = "Generate using enabled MeshColliders combined";
                    if (objects.Length == 1)
                    {
                        title += " (" + enabledMeshColliders.Length + " found)";
                    }
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var coll in nonConvexColliders)
                        {
                            UnityExtensions.GenerateCollidersFromMeshColliders(coll);
                            EditorUtility.SetDirty(coll);
                        }
                        EditorGUIUtility.ExitGUI();
                    }
                }

                using (var _ = new UnityExtensions.DisableGroup(colliders.Length == 0))
                {
                    var title = "Delete all Colliders (including children)";
                    if (objects.Length == 1)
                    {
                        title += " (" + colliders.Length + " found)";
                    }
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var coll in nonConvexColliders)
                        {
                            coll.SetPhysicsCollider(null);
                            EditorUtility.SetDirty(coll);
                        }
                        foreach (var c in colliders)
                        {
                            if (c == null) continue;
                            var go = c.gameObject;
                            GameObject.DestroyImmediate(c, true);
                            EditorUtility.SetDirty(go);
                        }
                        serializedObject.UpdateIfRequiredOrScript();    // m_collider will be invalid
                        EditorGUIUtility.ExitGUI();
                    }
                }

                using (var _ = new UnityExtensions.DisableGroup(disabledOrBrokenCollidersCount == 0))
                {
                    var title = "Delete all disabled/empty Colliders";
                    if (objects.Length == 1)
                    {
                        title += " (" + disabledOrBrokenCollidersCount + " found)";
                    }
                    if (GUILayout.Button(new GUIContent(title)))
                    {
                        foreach (var c in disabledOrBrokenMeshColliders)
                        {
                            if (c == null) continue;
                            var go = c.gameObject;
                            GameObject.DestroyImmediate(c, true);
                            EditorUtility.SetDirty(go);
                        }
                        foreach (var c in disabledColliders)
                        {
                            if (c == null) continue;
                            var go = c.gameObject;
                            GameObject.DestroyImmediate(c, true);
                            EditorUtility.SetDirty(go);
                        }
                        EditorGUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Has {m_colliders.arraySize} convex colliders in control", MessageType.Info);
                EditorGUI.BeginChangeCheck();
                m_showColliders.boolValue = EditorGUILayout.Toggle("Show Colliders", m_showColliders.boolValue);
                EditorGUILayout.Space();

                m_isTrigger.boolValue = EditorGUILayout.Toggle("Is Trigger", m_isTrigger.boolValue);
                
#if UNITY_6000_0_OR_NEWER
                m_material.objectReferenceValue = EditorGUILayout.ObjectField("Material", m_material.objectReferenceValue, typeof(PhysicsMaterial), false);
#else
                m_material.objectReferenceValue = EditorGUILayout.ObjectField("Material", m_material.objectReferenceValue, typeof(PhysicMaterial), false);
#endif

                
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var t in serializedObject.targetObjects)
                    {
                        var nonConvexCollider = t as NonConvexColliderComponent;
                        if (nonConvexCollider == null) continue;
                        EditorUtility.SetDirty(nonConvexCollider.gameObject);
                    }

                    if (serializedObject != null)
                        serializedObject.ApplyModifiedProperties();
                    
                    foreach (var t in serializedObject.targetObjects)
                    {
                        var nonConvexCollider = t as NonConvexColliderComponent;
                        if (nonConvexCollider)
                        {
                            nonConvexCollider.SyncState();
                            EditorUtility.SetDirty(nonConvexCollider.gameObject);
                        }
                    }
                }
            }
            finally
            {
                if (serializedObject != null)
                    serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawCommonParametersGUI()
        {
            EditorGUILayout.LabelField("Common Parameters (Shared by All Algorithms)", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_common_maxConvexHulls, new GUIContent("Max Convex Hulls",
                "Maximum number of convex pieces to generate. Shared by all algorithms.\n\n" +
                "Higher values = more detailed decomposition but more collision objects.\n" +
                "• 16-32: Simple shapes\n" +
                "• 64: Balanced (default)\n" +
                "• 128+: Complex shapes\n" +
                "Range: 1-256"));

            // Show warning if CoACD is selected and merge is disabled
            var algorithm = (DecompositionAlgorithm)m_algorithm.enumValueIndex;
            if (algorithm == DecompositionAlgorithm.CoACD_v2 && !m_coacd_merge.boolValue)
            {
                EditorGUILayout.HelpBox("Warning: 'Merge Hulls' is disabled in CoACD settings. The 'Max Convex Hulls' limit will be ignored.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(m_common_maxVerticesPerHull, new GUIContent("Max Vertices Per Hull",
                "Maximum vertices in each convex hull. Shared by all algorithms.\n\n" +
                "Lower values = simpler collision shapes, better performance.\n" +
                "Unity has a hard limit of 256 triangles per convex collider.\n" +
                "• 32-64: Simple hulls, better performance\n" +
                "• 64: Balanced (default)\n" +
                "• 128+: Detailed hulls, closer fit\n" +
                "Range: 4-256"));
        }

        private void DrawParametersGUI(SerializedProperty m_parameters, bool forceUpdatePreset)
        {
            EditorGUILayout.LabelField("V-HACD Specific Parameters", EditorStyles.boldLabel);

            // Preset selector
            EditorGUI.BeginChangeCheck();
            vhacdPresetIndex = EditorGUILayout.Popup(new GUIContent("Preset"), vhacdPresetIndex, vhacdPresetOptions);
            if (EditorGUI.EndChangeCheck() || forceUpdatePreset)
            {
                var nonConvexColliders = serializedObject.targetObjects.Select(o => o as NonConvexColliderComponent)
                                                        .Where(c => c != null)
                                                        .ToArray();

                foreach (var coll in nonConvexColliders)
                {
                    Parameters preset;
                    if (vhacdPresetIndex == 0) // Super Fast
                        preset = Parameters.SuperFast();
                    else if (vhacdPresetIndex == 1) // Fast
                        preset = Parameters.Fast();
                    else if (vhacdPresetIndex == 2) // Default
                        preset = Parameters.Default();
                    else
                        continue; // Custom - do nothing

                    // Update all parameter levels
                    coll.CommonParams = new CommonParameters
                    {
                        maxConvexHulls = preset.maxConvexHulls,
                        maxVerticesPerHull = preset.maxNumVerticesPerCH
                    };
                    coll.Params = preset;
                }
                serializedObject.Update();
            }

            // Always show parameters
            EditorGUILayout.PropertyField(m_parameters_resolution, new GUIContent("Resolution",
                "Voxel grid resolution for decomposition.\n\n" +
                "Higher resolution = more accurate convex hulls but slower processing.\n" +
                "• 100,000: Fast, lower detail\n" +
                "• 400,000: Balanced (default)\n" +
                "• 2,000,000+: High detail, slow\n" +
                "Range: 10,000-64,000,000"));
            EditorGUILayout.PropertyField(m_parameters_concavity, true);
            EditorGUILayout.PropertyField(m_parameters_planeDownsampling, true);
            EditorGUILayout.PropertyField(m_parameters_convexhullApproximation, true);
            EditorGUILayout.PropertyField(m_parameters_alpha, true);
            EditorGUILayout.PropertyField(m_parameters_beta, true);
            m_parameters_pca.boolValue = EditorGUILayout.Toggle(new GUIContent("Normalization", "Enable / disable normalizing the mesh before applying the convex decomposition. Default = False"), m_parameters_pca.boolValue);
            m_parameters_mode.intValue = EditorGUILayout.Popup(new GUIContent("Mode", "Voxel mode is default"), m_parameters_mode.intValue, modeOptions);
            EditorGUILayout.PropertyField(m_parameters_minVolumePerCH, true);
        }

        private void DrawCoACDParametersGUI(SerializedProperty m_coacdParams, bool forceUpdatePreset)
        {
            EditorGUILayout.LabelField("CoACD Specific Parameters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("CoACD uses a tree search algorithm to find optimal decomposition. Lower threshold = more pieces but better fit.", MessageType.Info);

            // Preset selector
            EditorGUI.BeginChangeCheck();
            coacdPresetIndex = EditorGUILayout.Popup(new GUIContent("Preset"), coacdPresetIndex, coacdPresetOptions);
            if (EditorGUI.EndChangeCheck() || forceUpdatePreset)
            {
                var nonConvexColliders = serializedObject.targetObjects.Select(o => o as NonConvexColliderComponent)
                                                        .Where(c => c != null)
                                                        .ToArray();

                foreach (var coll in nonConvexColliders)
                {
                    CoACDParameters preset;
                    if (coacdPresetIndex == 0) // Low
                        preset = CoACDParameters.LowResolution();
                    else if (coacdPresetIndex == 1) // Default
                        preset = CoACDParameters.Default();
                    else if (coacdPresetIndex == 2) // High
                        preset = CoACDParameters.HighResolution();
                    else
                        continue; // Custom - do nothing

                    // Update common params from preset
                    coll.CommonParams = new CommonParameters
                    {
                        maxConvexHulls = preset.maxConvexHull,
                        maxVerticesPerHull = preset.maxChVertex
                    };
                    coll.CoACDParams = preset;
                }
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(m_coacd_threshold, new GUIContent("Threshold",
                "Controls when to stop splitting. Lower values create more pieces for better accuracy. Higher values create fewer, simpler pieces.\n\n" +
                "• 0.01-0.03: Very detailed (many pieces)\n" +
                "• 0.05: Balanced (recommended)\n" +
                "• 0.1-1.0: Simplified (fewer pieces)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preprocessing", EditorStyles.boldLabel);

            m_coacd_preprocessMode.intValue = EditorGUILayout.Popup(
                new GUIContent("Preprocess Mode",
                    "Repairs non-manifold meshes before decomposition.\n\n" +
                    "• Auto: Enable if mesh has issues\n" +
                    "• Force On: Always repair (slower but safer)\n" +
                    "• Force Off: Skip repair (faster, but may fail on bad meshes)"),
                m_coacd_preprocessMode.intValue,
                new GUIContent[] {
                    new GUIContent("Auto (Recommended)"),
                    new GUIContent("Force On"),
                    new GUIContent("Force Off")
                });

            EditorGUILayout.PropertyField(m_coacd_prepResolution, new GUIContent("Prep Resolution",
                "Voxel resolution used during manifold repair. Higher = more accurate repair but slower.\n\n" +
                "Only used when preprocessing is enabled."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Search Parameters (MCTS - Quality vs Speed)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These control the tree search algorithm. Higher values = better results but slower processing.", MessageType.None);

            EditorGUILayout.PropertyField(m_coacd_sampleResolution, new GUIContent("Sample Resolution",
                "Number of samples for measuring distance between mesh and convex hull.\n\n" +
                "Higher = more accurate quality measurement but slower."));

            EditorGUILayout.PropertyField(m_coacd_mctsNodes, new GUIContent("MCTS Nodes",
                "How many cutting planes to try at each step (breadth).\n\n" +
                "• 10-15: Fast, lower quality\n" +
                "• 20: Balanced (recommended)\n" +
                "• 30-40: Slow, higher quality"));

            EditorGUILayout.PropertyField(m_coacd_mctsIteration, new GUIContent("MCTS Iterations",
                "Number of search iterations to refine the decomposition.\n\n" +
                "• 60-100: Fast, basic quality\n" +
                "• 150: Balanced (recommended)\n" +
                "• 300+: Slow, best quality"));

            EditorGUILayout.PropertyField(m_coacd_mctsMaxDepth, new GUIContent("MCTS Max Depth",
                "Maximum decomposition depth (how many times to split).\n\n" +
                "• 2: Shallow splits (few pieces)\n" +
                "• 3: Balanced (recommended)\n" +
                "• 4-7: Deep splits (many pieces)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Post-Processing", EditorStyles.boldLabel);

            m_coacd_pca.boolValue = EditorGUILayout.Toggle(new GUIContent("PCA Preprocessing",
                "Align mesh to principal axes before decomposition.\n\n" +
                "May improve results for asymmetric objects. Usually leave off."),
                m_coacd_pca.boolValue);

            m_coacd_merge.boolValue = EditorGUILayout.Toggle(new GUIContent("Merge Hulls",
                "Combine adjacent convex pieces if they're similar enough.\n\n" +
                "Reduces final piece count. Recommended to keep enabled."),
                m_coacd_merge.boolValue);

            m_coacd_extrude.boolValue = EditorGUILayout.Toggle(new GUIContent("Extrude",
                "Slightly expand convex hulls to ensure they fully contain the original mesh.\n\n" +
                "Prevents collision gaps but makes hulls slightly larger. Usually not needed."),
                m_coacd_extrude.boolValue);

            using (var _ = new UnityExtensions.DisableGroup(!m_coacd_extrude.boolValue))
            {
                EditorGUILayout.PropertyField(m_coacd_extrudeMargin, new GUIContent("  Extrude Margin",
                    "How much to expand hulls (percentage of bounding box size)."));
            }

            EditorGUILayout.PropertyField(m_coacd_seed, new GUIContent("Random Seed",
                "Seed for random number generator. Use 0 for random results each time, or set a number for reproducible results."));
        }

    }
}