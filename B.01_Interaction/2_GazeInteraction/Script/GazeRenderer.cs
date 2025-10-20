using UnityEngine;
using MetaFrame.Data;

public class GazeRenderer : MonoBehaviour
{
    [Header("Gaze Ray Settings")]
    [Tooltip("Check to enable gaze tracking and visualization")]
    public bool isEnabled = true;
    public float gazeRayLength = 500f;
    public Transform gazeDebugSphere;

    [Header("Smoothing Settings")]
    [Tooltip("Amount of smoothing applied to gaze direction (0 = no smoothing, higher = more smooth)")]
    [Range(0f, 0.95f)]
    public float smoothingFactor = 0.7f;

    [Tooltip("Minimum distance change required to update line position")]
    public float positionThreshold = 0.01f;

    [Header("Data Source Reference")]
    [Tooltip("Reference to the OVREyes component")]
    public OVREyes oVREyes;

    private LineRenderer lineRenderer;
    private Vector3 smoothedGazeDirection;
    private Vector3 smoothedEyeOrigin;
    private bool isInitialized = false;

    void Start()
    {
        if (!isEnabled)
        {
            if (gazeDebugSphere != null)
            {
                gazeDebugSphere.gameObject.SetActive(false);
            }
            return;
        }

        // Create LineRenderer
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            GameObject lineObj = new GameObject("GazeRayLine");
            lineObj.transform.SetParent(this.transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();
        }

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;

        Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = Color.cyan;
        lineRenderer.material = lineMat;
    }

    void Update()
    {
        if (!isEnabled) return;

        if (oVREyes == null)
        {
            Debug.LogWarning("Eye data source not assigned or data not available.");
            return;
        }

        // Get eye positions to calculate the origin point
        Vector3 leftEyePos = oVREyes.GetEyeTransform(OVREyes.Eye.Left).position;
        Vector3 rightEyePos = oVREyes.GetEyeTransform(OVREyes.Eye.Right).position;
        Vector3 eyeOrigin = (leftEyePos + rightEyePos) * 0.5f;

        // Get combined gaze direction
        Vector3 combinedGazeDirection = oVREyes.GetGazeVector(OVREyes.Eye.Combined);

        // Validate gaze direction
        if (combinedGazeDirection.magnitude < 0.01f)
        {
            Debug.LogWarning("Invalid gaze direction detected, skipping frame.");
            return;
        }

        // Initialize smoothed values on first valid frame
        if (!isInitialized)
        {
            smoothedGazeDirection = combinedGazeDirection;
            smoothedEyeOrigin = eyeOrigin;
            isInitialized = true;
        }

        // Apply smoothing using lerp
        smoothedGazeDirection = Vector3.Lerp(smoothedGazeDirection, combinedGazeDirection, 1f - smoothingFactor).normalized;
        smoothedEyeOrigin = Vector3.Lerp(smoothedEyeOrigin, eyeOrigin, 1f - smoothingFactor);

        // Only update if change is significant enough
        float directionChange = Vector3.Angle(smoothedGazeDirection, combinedGazeDirection);
        float positionChange = Vector3.Distance(smoothedEyeOrigin, eyeOrigin);

        if (directionChange < 0.5f && positionChange < positionThreshold)
        {
            // Skip minor updates to reduce jitter
            return;
        }

        // Calculate end point along gaze direction
        Vector3 endPoint = smoothedEyeOrigin + smoothedGazeDirection * gazeRayLength;

        // Update line renderer
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, smoothedEyeOrigin);
            lineRenderer.SetPosition(1, endPoint);
        }

        // Update debug sphere
        if (gazeDebugSphere != null)
        {
            gazeDebugSphere.position = endPoint;
            gazeDebugSphere.rotation = Quaternion.LookRotation(smoothedGazeDirection);
        }
    }
}