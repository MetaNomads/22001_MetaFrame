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

    void Awake()
    {
        toggle = GetComponent<Toggle>();

        if (oculusColorVisual == null)
            oculusColorVisual = GetComponent<InteractableColorVisual>();

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        block = new MaterialPropertyBlock();
        colorID = Shader.PropertyToID(colorProperty);

        // ✅ Initialize current color properly
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
        toggle.onValueChanged.AddListener(OnToggleChanged);

        // Sync visual with current state
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
            // Disable Oculus visual control
            if (oculusColorVisual != null)
                oculusColorVisual.enabled = false;

            PlayOn();
        }
        else
        {
            // Stop custom animation + clear override FIRST
            StopCustom();

            // Re-enable Oculus visual system
            if (oculusColorVisual != null)
            {
                oculusColorVisual.enabled = false; // force refresh
                oculusColorVisual.enabled = true;
            }
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
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (targetRenderer != null)
            targetRenderer.SetPropertyBlock(null);
    }

    /// <summary>
    /// Safe to call on inactive GameObjects.
    /// Clears the ON color from the renderer and nulls the coroutine reference.
    /// Does NOT touch oculusColorVisual.enabled — toggling that on an inactive
    /// object corrupts the Oculus color system.
    /// When the panel next becomes active, OnEnable fires OnToggleChanged(toggle.isOn)
    /// which performs the full OFF reset (re-enable Oculus) on a live object.
    /// </summary>
    public void ClearPropertyBlock()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (targetRenderer != null)
        {
            targetRenderer.SetPropertyBlock(null);

            // Resync currentColor to what the renderer will now actually show
            // (the material default). Without this, the next AnimateOn() starts
            // from the stale ON color even though the renderer has been reset,
            // causing a visual jump instead of a smooth transition.
            if (targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(colorID))
                currentColor = targetRenderer.sharedMaterial.GetColor(colorID);
            else
                currentColor = Color.white;
        }
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