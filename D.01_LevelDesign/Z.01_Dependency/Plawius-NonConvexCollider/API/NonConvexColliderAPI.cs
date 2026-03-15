using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Plawius.NonConvexCollider
{
    public class StopwatchScoped : IDisposable
    {
        private readonly string name;
        private readonly Stopwatch stopwatch;
        public StopwatchScoped(string name)
        {
            this.name = name;
            stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            var elapsed = stopwatch.ElapsedMilliseconds;
            if (elapsed > 1000)
                Debug.LogFormat("[{0}] took {1} seconds", name, elapsed / 1000.0);
            else
                Debug.LogFormat("[{0}] took {1} msec", name, elapsed);
        }
    }

    // Common parameters shared by all three decomposition algorithms
    [Serializable]
    public struct CommonParameters
    {
        /// Maximum number of convex hulls. Default = 64. Range = [1 .. 256]
        [Tooltip("Maximum number of convex pieces to generate. Shared by all algorithms.\n\n" +
            "Higher values = more detailed decomposition but more collision objects.\n" +
            "• 16-32: Simple shapes\n" +
            "• 64: Balanced (default)\n" +
            "• 128+: Complex shapes\n" +
            "Range: 1-256")]
        [UnityEngine.Range(1, 256)]
        public int maxConvexHulls;

        /// Maximum vertices per convex hull. Default = 64. Range = [4 .. 256]
        [Tooltip("Maximum vertices in each convex hull. Shared by all algorithms.\n\n" +
            "Lower values = simpler collision shapes, better performance.\n" +
            "Unity has a hard limit of 256 triangles per convex collider.\n" +
            "• 32-64: Simple hulls, better performance\n" +
            "• 64: Balanced (default)\n" +
            "• 128+: Detailed hulls, closer fit\n" +
            "Range: 4-256")]
        [UnityEngine.Range(4, 256)]
        public int maxVerticesPerHull;

        public static CommonParameters Default()
        {
            return new CommonParameters
            {
                maxConvexHulls = 32,
                maxVerticesPerHull = 64
            };
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + maxConvexHulls.GetHashCode();
                hash = hash * 31 + maxVerticesPerHull.GetHashCode();
                return hash;
            }
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Parameters
    {
        /// Maximum number of convex hulls. Default = 32. Range = [1 .. 128]
        [Tooltip("Maximum number of convex hulls. Default = 32. Range = [1 .. 128]")]
        [UnityEngine.Range(1, 128)]
        public int maxConvexHulls;

        /// Maximum number of voxels generated during the voxelization stage. Default = 50000. Range = [10000 .. 1000000]
        [Tooltip("Maximum number of voxels generated during the voxelization stage. Default = 50000. Range = [10000 .. 1000000]")]
        [UnityEngine.Range(10000, 1000000)]
        public int resolution; 
        
        /// Maximum concavity. Default = 0.0025. Range = [0.0 .. 1.0]
        [Tooltip("Maximum concavity. Default = 0.0025. Range = [0.0 .. 1.0]")]
        [UnityEngine.Range(0.0f, 1.0f)]
        public double concavity;
        
        /// Controls the granularity of the search for the "best" clipping plane. Default = 4. Range = [1 .. 16]
        [Tooltip("Controls the granularity of the search for the \"best\" clipping plane. Default = 4. Range = [1 .. 16]")]
        [UnityEngine.Range(1, 16)]
        public int planeDownsampling;
        
        /// Controls the precision of the convex - hull generation process during the clipping plane selection stage. Default = 4. Range = [1 .. 16]
        [Tooltip("Controls the precision of the convex - hull generation process during the clipping plane selection stage. Default = 4. Range = [1 .. 16]")]
        [UnityEngine.Range(1, 16)]
        public int convexhullApproximation;
        
        /// Controls the bias toward clipping along symmetry planes. Default = 0.05. Range = [0.0 .. 1.0]
        [Tooltip("Controls the bias toward clipping along symmetry planes. Default = 0.05. Range = [0.0 .. 1.0]")]
        [UnityEngine.Range(0.0f, 1.0f)]
        public double alpha;
        
        /// Controls the bias toward clipping along revolution axes. Default = 0.05. Range = [0.0 .. 1.0]
        [Tooltip("Controls the bias toward clipping along revolution axes. Default = 0.05. Range = [0.0 .. 1.0]")]
        [UnityEngine.Range(0.0f, 1.0f)]
        public double beta;
        
        /// Enable / disable normalizing the mesh before applying the convex decomposition. Default = 1. Range = [0 .. 1]
        [Tooltip("Enable / disable normalizing the mesh before applying the convex decomposition. Default = 1. Range = [0 .. 1]")]
        [UnityEngine.Range(0, 1)]
        public int pca;
        
        /// 0: voxel - based approximate convex decomposition, 1 : tetrahedron - based approximate convex decomposition. Default = 0. Range = [0 .. 1]
        [Tooltip("0: voxel - based approximate convex decomposition, 1 : tetrahedron - based approximate convex decomposition. Default = 0. Range = [0 .. 1]")]
        [UnityEngine.Range(0, 1)]
        public int mode;
        
        /// Controls the maximum number of triangles per convex - hull. Default = 64. Range = [4 .. 1024]
        [Tooltip("Controls the maximum number of triangles per convex - hull. Default = 64. Range = [4 .. 1024]")]
        [UnityEngine.Range(4, 1024)]
        public int maxNumVerticesPerCH;
        
        /// Controls the adaptive sampling of the generated convex - hulls. Default = 0.0001. Range = [0.0 .. 0.01]
        [Tooltip("Controls the adaptive sampling of the generated convex - hulls. Default = 0.0001. Range = [0.0 .. 0.01]")]
        [UnityEngine.Range(0.0f, 0.01f)]
        public double minVolumePerCH;

        public delegate void NativeCallbackDelegate(double overallProgress, double stageProgress, double operationProgress, 
                                                    IntPtr stage, IntPtr operation);

        [HideInInspector]
        public NativeCallbackDelegate callback;

        public static Parameters SuperFast()
        {
            return new Parameters
            {
                resolution = 10000,
                maxConvexHulls = 8,
                concavity = 0.0025,
                planeDownsampling = 4,
                convexhullApproximation = 4,
                alpha = 0.05,
                beta = 0.05,
                pca = 1,
                mode = 0,
                maxNumVerticesPerCH = 64,
                minVolumePerCH = 0.0001,
                callback = null
            };
        }

        public static Parameters Fast()
        {
            return new Parameters
            {
                resolution = 50000,
                maxConvexHulls = 32,
                concavity = 0.0025,
                planeDownsampling = 4,
                convexhullApproximation = 4,
                alpha = 0.05,
                beta = 0.05,
                pca = 1,
                mode = 0,
                maxNumVerticesPerCH = 64,
                minVolumePerCH = 0.0001,
                callback = null
            };
        }

        public static Parameters Default()
        {
            return new Parameters
            {
                resolution = 1000000,
                maxConvexHulls = 32,
                concavity = 0.0025,
                planeDownsampling = 4,
                convexhullApproximation = 4,
                alpha = 0.05,
                beta = 0.05,
                pca = 1,
                mode = 0,
                maxNumVerticesPerCH = 128,
                minVolumePerCH = 0.0001,
                callback = null
            };
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + maxConvexHulls.GetHashCode();
                hash = hash * 31 + resolution.GetHashCode();
                hash = hash * 31 + concavity.GetHashCode();
                hash = hash * 31 + planeDownsampling.GetHashCode();
                hash = hash * 31 + convexhullApproximation.GetHashCode();
                hash = hash * 31 + alpha.GetHashCode();
                hash = hash * 31 + beta.GetHashCode();
                hash = hash * 31 + pca.GetHashCode();
                hash = hash * 31 + mode.GetHashCode();
                hash = hash * 31 + maxNumVerticesPerCH.GetHashCode();
                hash = hash * 31 + minVolumePerCH.GetHashCode();
                return hash;
            }
        }
    }

    // CoACD Parameters (New v2)
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CoACDParameters
    {
        /// Concavity threshold for terminating the decomposition. Default = 0.05. Range = [0.01 .. 1.0]
        [Tooltip("Concavity threshold for terminating decomposition. Lower = more detailed. Default = 0.05")]
        [UnityEngine.Range(0.01f, 1.0f)]
        public double threshold;

        /// Maximum number of convex hulls. -1 for no limit. Default = -1. Range = [-1 .. 128]
        [Tooltip("Maximum number of convex hulls. -1 for no limit. Default = -1")]
        [UnityEngine.Range(-1, 128)]
        public int maxConvexHull;

        /// Preprocess mode: 0=auto, 1=on, 2=off. Default = 0 (auto)
        [Tooltip("Manifold preprocessing: 0=auto, 1=force on, 2=force off")]
        [UnityEngine.Range(0, 2)]
        public int preprocessMode;

        /// Resolution for manifold preprocessing. Default = 50. Range = [20 .. 100]
        [Tooltip("Resolution for manifold preprocessing. Default = 50")]
        [UnityEngine.Range(20, 100)]
        public int prepResolution;

        /// Sampling resolution for Hausdorff distance. Default = 2000. Range = [1000 .. 10000]
        [Tooltip("Sampling resolution for distance calculation. Default = 2000")]
        [UnityEngine.Range(1000, 10000)]
        public int sampleResolution;

        /// Max number of child nodes in MCTS. Default = 20. Range = [10 .. 40]
        [Tooltip("MCTS search breadth. Higher = slower but better. Default = 20")]
        [UnityEngine.Range(10, 40)]
        public int mctsNodes;

        /// Number of MCTS iterations. Default = 150. Range = [60 .. 2000]
        [Tooltip("MCTS iterations. Higher = slower but better. Default = 150")]
        [UnityEngine.Range(60, 2000)]
        public int mctsIteration;

        /// Max search depth in MCTS. Default = 3. Range = [1 .. 7]
        [Tooltip("MCTS search depth. Higher = slower but better. Default = 3")]
        [UnityEngine.Range(1, 7)]
        public int mctsMaxDepth;

        /// Enable PCA preprocessing. Default = false
        [Tooltip("Enable PCA preprocessing. Default = false")]
        [MarshalAs(UnmanagedType.I1)]
        public bool pca;

        /// Enable merge post-processing. Default = true
        [Tooltip("Merge similar convex hulls. Default = true")]
        [MarshalAs(UnmanagedType.I1)]
        public bool merge;

        /// Enable decimation of output. Default = true (REQUIRED for Unity)
        [Tooltip("Reduce triangle count in output meshes. STRONGLY RECOMMENDED: Unity has a 256 triangle limit per convex collider.")]
        [MarshalAs(UnmanagedType.I1)]
        public bool decimate;

        /// Max vertices per convex hull (when decimate is enabled). Default = 256
        [Tooltip("Max vertices per hull when decimation is enabled. Default = 256")]
        [UnityEngine.Range(32, 1024)]
        public int maxChVertex;

        /// Enable extrusion of neighboring hulls. Default = false
        [Tooltip("Extrude neighboring hulls. Default = false")]
        [MarshalAs(UnmanagedType.I1)]
        public bool extrude;

        /// Extrusion margin. Default = 0.01
        [Tooltip("Extrusion margin when extrude is enabled. Default = 0.01")]
        [UnityEngine.Range(0.001f, 0.1f)]
        public double extrudeMargin;

        /// Approximation mode: 0=convex hull, 1=box. Never use box
        private int apxMode;

        /// Random seed for reproducibility. Default = 0
        [Tooltip("Random seed. 0 = random each time")]
        public uint seed;

        public static CoACDParameters LowResolution()
        {
            return new CoACDParameters
            {
                threshold = 0.1,
                maxConvexHull = 16,
                preprocessMode = 0,
                prepResolution = 30,
                sampleResolution = 1000,
                mctsNodes = 15,
                mctsIteration = 100,
                mctsMaxDepth = 2,
                pca = false,
                merge = true,
                decimate = true,  // Enable to respect Unity's 256 polygon limit
                maxChVertex = 64,  // Low resolution = fewer vertices
                extrude = false,
                extrudeMargin = 0.01,
                apxMode = 0,
                seed = 0
            };
        }

        public static CoACDParameters Default()
        {
            return new CoACDParameters
            {
                threshold = 0.05,
                maxConvexHull = 32,
                preprocessMode = 0,
                prepResolution = 50,
                sampleResolution = 2000,
                mctsNodes = 20,
                mctsIteration = 150,
                mctsMaxDepth = 3,
                pca = false,
                merge = true,
                decimate = true,  // Enable by default to respect Unity's 256 polygon limit
                maxChVertex = 128,  // Conservative default to stay well under Unity's limit
                extrude = false,
                extrudeMargin = 0.01,
                apxMode = 0,
                seed = 0
            };
        }

        public static CoACDParameters HighResolution()
        {
            return new CoACDParameters
            {
                threshold = 0.025,
                maxConvexHull = 64,
                preprocessMode = 0,
                prepResolution = 80,
                sampleResolution = 5000,
                mctsNodes = 30,
                mctsIteration = 300,
                mctsMaxDepth = 4,
                pca = false,
                merge = true,
                decimate = true,  // Enable to respect Unity's 256 polygon limit
                maxChVertex = 200,
                extrude = false,
                extrudeMargin = 0.01,
                apxMode = 0,
                seed = 0
            };
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + threshold.GetHashCode();
                hash = hash * 31 + maxConvexHull.GetHashCode();
                hash = hash * 31 + preprocessMode.GetHashCode();
                hash = hash * 31 + prepResolution.GetHashCode();
                hash = hash * 31 + sampleResolution.GetHashCode();
                hash = hash * 31 + mctsNodes.GetHashCode();
                hash = hash * 31 + mctsIteration.GetHashCode();
                hash = hash * 31 + mctsMaxDepth.GetHashCode();
                hash = hash * 31 + pca.GetHashCode();
                hash = hash * 31 + merge.GetHashCode();
                hash = hash * 31 + decimate.GetHashCode();
                hash = hash * 31 + maxChVertex.GetHashCode();
                hash = hash * 31 + extrude.GetHashCode();
                hash = hash * 31 + extrudeMargin.GetHashCode();
                hash = hash * 31 + apxMode.GetHashCode();
                hash = hash * 31 + seed.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// API to use from scripts. Editor only!
    /// </summary>
    public static class API
    {
        public static Mesh[] GenerateConvexMeshes(Mesh nonconvexMesh)
        {
            return GenerateConvexMeshes(nonconvexMesh, Parameters.Default(), null);
        }

        private static Action<float, string> _currentProgressCallback = null;
        
        [MonoPInvokeCallback(typeof(Parameters.NativeCallbackDelegate))]
        private static void NativeCallback(double overallProgress, double stageProgress, double operationProgress,
                                       IntPtr stagePtr, IntPtr operationPtr)
        {
            var stage = Marshal.PtrToStringAnsi(stagePtr);
            var operation = Marshal.PtrToStringAnsi(operationPtr);
            
            var progress01 = (float) (overallProgress * 0.01);
            
            var title = (stage != operation) ? string.Format("{0}. {1}", stage, operation) : stage;
            if (_currentProgressCallback != null)
                _currentProgressCallback(progress01, title);
        }

        public static Mesh[] GenerateConvexMeshes(Mesh nonconvexMesh, Parameters parameters, Action<float, string> progressCallback = null)
        {
            using var _ = new StopwatchScoped("VHCAD");

            if (nonconvexMesh == null)
                throw new Exception("GenerateConvexMeshes called with nonconvexMesh == null");
            
            var progress = 0.0f;
            if (progressCallback != null)
                progressCallback(progress, "Initialization...");
            
            var ptriangles = nonconvexMesh.triangles;
            var ptrianglesLength = ptriangles.Length;
            var pvertices = nonconvexMesh.vertices;


            var outPoints = IntPtr.Zero;
            var outTriangles = IntPtr.Zero;
            var indexes = IntPtr.Zero;
            var indexesCnt = 0;
            
            progress = 0.1f;
            if (progressCallback != null)
                progressCallback(progress, "Generation is in progress, please wait...");

            parameters.callback = NativeCallback;

            _currentProgressCallback = progressCallback;

            var meshCount = -1;
            var gcVertices = GCHandle.Alloc(pvertices, GCHandleType.Pinned);
            var gcTriangles = GCHandle.Alloc(ptriangles, GCHandleType.Pinned);
            try
            {
                using var _2 = new StopwatchScoped("VHCAD interop");

                meshCount = Interop.GetMeshEx(gcVertices.AddrOfPinnedObject(), nonconvexMesh.vertexCount * 3,
                    gcTriangles.AddrOfPinnedObject(), ptrianglesLength,
                    out outPoints, out outTriangles, out indexes, out indexesCnt, parameters);
            }
            finally
            {
                if (gcVertices.IsAllocated) gcVertices.Free();
                if (gcTriangles.IsAllocated) gcTriangles.Free();
            }
            
            _currentProgressCallback = null;

            if (meshCount <= 0)
                throw new Exception("GenerateConvexMeshes failed, nothing is returned, please check your Mesh and/or your Parameters");

            if (meshCount > parameters.maxConvexHulls)
                throw new Exception("GenerateConvexMeshes failed, returned " + meshCount + " meshes returned, but maxConvexHulls == " + parameters.maxConvexHulls);

            var sanityCheck = (indexesCnt >= 0 && indexesCnt % 2 == 0) && (meshCount == indexesCnt / 2);
            if (sanityCheck == false)
                throw new Exception("GenerateConvexMeshes failed, data is corrupted");

            progress = 0.4f;
            if (progressCallback != null)
                progressCallback(progress, "Generation is done. Getting results...");
            var progressStep = (1.0f - progress);
            if (meshCount != 0)
                progressStep /= meshCount;

            try
            {
                var result = new Mesh[meshCount];
            
                var indxs = new int[indexesCnt];
                Marshal.Copy(indexes, indxs, 0, indexesCnt);

                var pointsStartIndex = 0;
                var trianglesStartIndex = 0;

                for (var meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    if (progressCallback != null)
                        progressCallback(progress, "Generation is done. Getting result " + (meshIndex + 1) + "/" + meshCount + "...");
                    progress += progressStep;

                    var pointCnt = indxs[meshIndex * 2];
                    var trianglesCnt = indxs[meshIndex * 2 + 1];

                    sanityCheck = (pointCnt >= 0) && (trianglesCnt >= 0) && (pointCnt % 3 == 0);
                    if (sanityCheck == false)
                        throw new Exception("GenerateConvexMeshes failed, one of the mesh data is corrupted");
            
                    var newPoints = new double[pointCnt];
                    var newTriangles = new int[trianglesCnt];

                    var pPoints = new IntPtr(outPoints.ToInt64() + sizeof(double) * pointsStartIndex);
                    Marshal.Copy(pPoints, newPoints, 0, pointCnt);
                    pointsStartIndex += pointCnt;
                
                    var pTriangles = new IntPtr(outTriangles.ToInt64() + sizeof(int) * trianglesStartIndex);
                    Marshal.Copy(pTriangles, newTriangles, 0, trianglesCnt);
                    trianglesStartIndex += trianglesCnt;

                    var tmp = new Vector3[pointCnt / 3];
                    for (var p = 0; p < (pointCnt / 3); p++)
                    {
                        tmp[p] = new Vector3((float)newPoints[p * 3], (float)newPoints[1 + p * 3], (float)newPoints[2 + p * 3]);
                    }

                    result[meshIndex] = new Mesh
                    {
                        vertices = tmp,
                        triangles = newTriangles,
                        name = "Generated convex submesh " + (meshIndex + 1)
                    };
                }
                
                if (progressCallback != null)
                    progressCallback(1.0f, "Generation is done. Cleaning up...");
                
                return result;
            }
            finally
            {
                Interop.ReleaseMemory(indexes);
                Interop.ReleaseMemory(outPoints);
                Interop.ReleaseMemory(outTriangles);
            }
        }

        // CoACD (New v2) Methods
        public static Mesh[] GenerateConvexMeshesCoACD(Mesh nonconvexMesh)
        {
            return GenerateConvexMeshesCoACD(nonconvexMesh, CoACDParameters.Default(), null);
        }

        public static Mesh[] GenerateConvexMeshesCoACD(Mesh nonconvexMesh, CoACDParameters parameters, Action<float, string> progressCallback = null)
        {
            using var _ = new StopwatchScoped("CoACD");

            if (nonconvexMesh == null)
                throw new Exception("GenerateConvexMeshesCoACD called with nonconvexMesh == null");

            var progress = 0.0f;
            if (progressCallback != null)
                progressCallback(progress, "Initialization...");

            var pvertices = nonconvexMesh.vertices;
            var ptriangles = nonconvexMesh.triangles;

            var outPoints = IntPtr.Zero;
            var outTriangles = IntPtr.Zero;
            var outCounts = IntPtr.Zero;
            var meshCount = 0;
            
            progress = 0.1f;
            if (progressCallback != null)
                progressCallback(progress, "CoACD decomposition in progress...");

            if (parameters.threshold < 0.01 || parameters.prepResolution == 0)
                parameters = CoACDParameters.Default();

            var result_code = 0;
            var gcVertices = GCHandle.Alloc(pvertices, GCHandleType.Pinned);
            var gcTriangles = GCHandle.Alloc(ptriangles, GCHandleType.Pinned);
            try
            {
                using var _2 = new StopwatchScoped("CoACD interop");

                // Use GetMeshExFloat with pinned Vector3[] - no float[] allocation needed
                // Unity's Vector3 is sequential layout of 3 floats, so it can be passed directly
                result_code = InteropCoACD.GetMeshExFloat(
                    gcVertices.AddrOfPinnedObject(), pvertices.Length,
                    gcTriangles.AddrOfPinnedObject(), ptriangles.Length / 3,
                    out outPoints, out outTriangles, out outCounts, out meshCount,
                    parameters);
            }
            finally
            {
                if (gcVertices.IsAllocated) gcVertices.Free();
                if (gcTriangles.IsAllocated) gcTriangles.Free();
            }

            if (result_code != 0)
                throw new Exception($"GenerateConvexMeshesCoACD failed with error code: {result_code}");

            if (meshCount <= 0)
                throw new Exception("GenerateConvexMeshesCoACD failed, no convex hulls generated");

            progress = 0.4f;
            if (progressCallback != null)
                progressCallback(progress, "Decomposition done. Getting results...");

            var progressStep = (1.0f - progress);
            if (meshCount != 0)
                progressStep /= meshCount;

            try
            {
                var result = new Mesh[meshCount];

                unsafe
                {
                    int* pCounts = (int*)outCounts.ToPointer();
                    double* pVertices = (double*)outPoints.ToPointer();
                    int* pTriangles = (int*)outTriangles.ToPointer();

                    var vertexOffset = 0;
                    var triangleOffset = 0;

                    for (var meshIndex = 0; meshIndex < meshCount; meshIndex++)
                    {
                        if (progressCallback != null)
                            progressCallback(progress, $"Getting result {meshIndex + 1}/{meshCount}...");
                        progress += progressStep;

                        var vertexCount = pCounts[meshIndex * 2 + 0];     // Number of vertices
                        var triangleCount = pCounts[meshIndex * 2 + 1];   // Number of triangles
                        var indexCount = triangleCount * 3;                // Number of indices (3 per triangle)

                        if (vertexCount <= 0 || triangleCount <= 0)
                            throw new Exception($"GenerateConvexMeshesCoACD failed, mesh {meshIndex} has invalid counts: vertices={vertexCount}, triangles={triangleCount}");

                        // Unity has a hard limit of 256 polygons per convex mesh collider
                        if (triangleCount > 256)
                        {
                            Debug.LogWarning($"CoACD convex hull {meshIndex + 1} has {triangleCount} triangles, which exceeds Unity's limit of 256. " +
                                           $"Enable 'Decimate' in CoACD parameters and reduce 'Max Vertices Per Hull' to fix this. " +
                                           $"Unity will use a partial hull which may not match your mesh exactly.");
                        }

                        // Allocate arrays for this hull
                        var hullVertices = new Vector3[vertexCount];
                        var hullTriangles = new int[indexCount];

                        // Read vertices directly using pointer arithmetic (no Marshal.Copy)
                        // pVertices points to start of all vertex data, vertexOffset tracks position
                        double* pCurrentVertex = pVertices + (vertexOffset * 3);
                        for (int i = 0; i < vertexCount; i++)
                        {
                            // Read 3 doubles per vertex, incrementing pointer as we go
                            hullVertices[i] = new Vector3(
                                (float)pCurrentVertex[0],
                                (float)pCurrentVertex[1],
                                (float)pCurrentVertex[2]);
                            pCurrentVertex += 3;
                        }

                        // Read triangle indices directly using pointer arithmetic (no Marshal.Copy)
                        int* pCurrentTriangle = pTriangles + (triangleOffset * 3);
                        for (int i = 0; i < indexCount; i++)
                        {
                            hullTriangles[i] = pCurrentTriangle[i];
                        }

                        result[meshIndex] = new Mesh
                        {
                            vertices = hullVertices,
                            triangles = hullTriangles,
                            name = $"CoACD convex hull {meshIndex + 1}"
                        };

                        vertexOffset += vertexCount;
                        triangleOffset += triangleCount;
                    }
                }

                if (progressCallback != null)
                    progressCallback(1.0f, "CoACD decomposition complete!");

                return result;
            }
            finally
            {
                InteropCoACD.ReleaseMemory(outCounts);
                InteropCoACD.ReleaseMemory(outPoints);
                InteropCoACD.ReleaseMemory(outTriangles);
            }
        }
    }
}