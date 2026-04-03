#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class LockSceneViewToCamera : MonoBehaviour
{
    public enum ProjectionMode { Perspective, Orthographic }

    [Tooltip("Lock the Scene view to this GameObject's position and rotation.")]
    public bool lockSceneView = false;

    [Tooltip("Perspective matches a normal camera. Orthographic removes depth/foreshortening.")]
    public ProjectionMode projection = ProjectionMode.Perspective;

    [Range(0.01f, 10f)]
    [Tooltip("Zoom level — smaller values zoom in closer.")]
    public float size = 0.01f;

    private void Update()
    {
        if (!lockSceneView) return;

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        bool ortho = projection == ProjectionMode.Orthographic;

        sceneView.in2DMode = false;
        sceneView.LookAt(
            transform.position + transform.forward,
            transform.rotation,
            size,
            ortho,
            true
        );
    }
}
#endif