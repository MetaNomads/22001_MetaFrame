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
            oculusColorVisual =
                GetComponent<InteractableColorVisual>();

        block = new MaterialPropertyBlock();

        colorID = Shader.PropertyToID(colorProperty);

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        toggle.onValueChanged.AddListener(OnToggleChanged);

        // 初始化
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
            // :white_check_mark: 关掉 Oculus 控制
            if (oculusColorVisual != null)
                oculusColorVisual.enabled = false;

            PlayOn();
        }
        else
        {
            // :white_check_mark: 还给 Oculus 控制权
            if (oculusColorVisual != null)
                oculusColorVisual.enabled = true;

            StopCustom();
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

        targetRenderer.GetPropertyBlock(block);

        block.SetColor(colorID, c);

        targetRenderer.SetPropertyBlock(block);
    }
}