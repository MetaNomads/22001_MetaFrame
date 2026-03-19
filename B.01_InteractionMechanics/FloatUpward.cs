using UnityEngine;

public class FloatUpward : MonoBehaviour, ITrackableAction
{
    [Tooltip("The GameObject to move. Falls back to this GameObject if left empty.")]
    [SerializeField] private GameObject target;

    [SerializeField] private float distance = 1f;
    [SerializeField] private float duration = 1f;

    private Transform Target => target != null ? target.transform : transform;

    private Vector3       _startPosition;
    private Vector3       _targetPosition;
    private float         _elapsed;
    private bool          _running;
    private System.Action _onComplete;

    /// <summary>Start without tracking — fire and forget.</summary>
    public void Play() => Run(null);

    /// <summary>Start and call onComplete when finished. Used by CollisionTrigger.RunTracked().</summary>
    public void Run(System.Action onComplete)
    {
        _startPosition  = Target.position;
        _targetPosition = _startPosition + Vector3.up * distance;
        _elapsed        = 0f;
        _running        = true;
        _onComplete     = onComplete;
    }

    private void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        float t   = Mathf.Clamp01(_elapsed / duration);

        Target.position = Vector3.Lerp(_startPosition, _targetPosition, Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f)
        {
            Target.position = _targetPosition;
            _running        = false;
            _onComplete?.Invoke();
            _onComplete = null;
        }
    }
}