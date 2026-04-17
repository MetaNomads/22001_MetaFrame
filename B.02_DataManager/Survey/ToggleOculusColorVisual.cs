using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

/// <summary>
/// When Toggle is ON:
///   - Disable InteractableColorVisual
///   - Play custom ON color animation
/// When OFF:
///   - Re-enable Oculus color system
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ToggleOculusColorVisual : MonoBehaviour
{
    [System.Serializable]
    public class ColorState
    {
        public Color Color = Color.white;
        public AnimationCurve Curve =
            AnimationCurve.EaseInOut(0, 0, 1, 1);

        public float Time = 0.15f;
    }

    [Header("References")]
    public InteractableColorVisual oculusColorVisual;
    public Renderer targetRenderer;

    [Header("ON State")]
    public ColorState OnState = new ColorState();

    public string colorProperty = "_Color";

    private Toggle toggle;
    private MaterialPropertyBlock block;
    private Coroutine routine;
    private int colorID;
    private Color currentColor;

    // Set true by ClearSelection before t.isOn = false, false after.
    // Prevents OnToggleChanged(false) from cycling oculusColorVisual during
    // a reset — ClearPropertyBlock re-enables it safely after the loop.
    private bool _clearing = false;

    public void SetClearing(bool value) => _clearing = value;

    void Awake()
    {
        toggle = GetComponent<Toggle>();

        if (oculusColorVisual == null)
            oculusColorVisual = GetComponent<InteractableColorVisual>();

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        block   = new MaterialPropertyBlock();
        colorID = Shader.PropertyToID(colorProperty);

        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(block);
            if (block.HasColor(colorID))
                currentColor = block.GetColor(colorID);
            else if (targetRenderer.sharedMaterial != null &&
                     targetRenderer.sharedMaterial.HasProperty(colorID))
                currentColor = targetRenderer.sharedMaterial.GetColor(colorID);
            else
                currentColor = Color.white;
        }
    }

    void OnEnable()
    {
        // FIX: RemoveListener before AddListener so that repeated Enable/Disable
        // cycles (e.g. survey panel shown/hidden each session) never accumulate
        // duplicate listeners. Unity's AddListener does not deduplicate runtime
        // delegates — each extra copy fires OnToggleChanged an additional time,
        // spawning redundant coroutines and toggling oculusColorVisual.enabled
        // multiple times per interaction, causing a permanent per-session
        // framerate cost that compounds across sessions.
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
        toggle.onValueChanged.AddListener(OnToggleChanged);
        OnToggleChanged(toggle.isOn);
    }

    void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            if (oculusColorVisual != null)
                oculusColorVisual.enabled = false;
            PlayOn();
        }
        else
        {
            StopCustom();

            if (!_clearing)
            {
                // Normal mid-trial deselect — cycle oculusColorVisual so Oculus
                // drives Normal/hover state for this button.
                if (oculusColorVisual != null)
                {
                    oculusColorVisual.enabled = false;
                    oculusColorVisual.enabled = true;
                }
            }
            // _clearing=true: skip cycle. ClearPropertyBlock re-enables
            // oculusColorVisual after the full reset loop completes.
        }
    }

    // ================= Custom Color =================

    private void PlayOn()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(AnimateOn());
    }

    private void StopCustom()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }

        if (targetRenderer != null)
        {
            targetRenderer.SetPropertyBlock(null);

            if (targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(colorID))
                currentColor = targetRenderer.sharedMaterial.GetColor(colorID);
            else
                currentColor = Color.white;
        }
    }

    /// <summary>
    /// Called by ClearSelection after _clearing=true and isOn=false.
    /// Clears ON color and re-enables oculusColorVisual.
    /// Safe to call on active and inactive GameObjects.
    /// </summary>
    public void ClearPropertyBlock()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }

        if (targetRenderer != null)
        {
            targetRenderer.SetPropertyBlock(null);

            if (targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(colorID))
                currentColor = targetRenderer.sharedMaterial.GetColor(colorID);
            else
                currentColor = Color.white;
        }

        if (oculusColorVisual != null)
            oculusColorVisual.enabled = true;
    }

    private IEnumerator AnimateOn()
    {
        Color start = currentColor;
        float timer = 0f;

        while (timer < OnState.Time)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / OnState.Time);
            t = OnState.Curve.Evaluate(t);

            Color c = Color.Lerp(start, OnState.Color, t);
            ApplyColor(c);

            yield return null;
        }

        ApplyColor(OnState.Color);
    }

    private void ApplyColor(Color c)
    {
        currentColor = c;

        if (targetRenderer == null)
            return;

        block.SetColor(colorID, c);
        targetRenderer.SetPropertyBlock(block);
    }
}
