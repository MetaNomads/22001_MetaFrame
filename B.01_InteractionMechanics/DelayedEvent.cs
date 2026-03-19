using UnityEngine;
using UnityEngine.Events;

public class DelayedEvent : MonoBehaviour
{
    [SerializeField] private float delay = 1f;

    [Space]
    public UnityEvent onStart;
    public UnityEvent onDelay;

    /// <summary>Play using the serialized delay value.</summary>
    public void Play()
    {
        onStart?.Invoke();
        StartCoroutine(WaitThenComplete(delay));
    }

    /// <summary>Play with a custom delay set directly from the Event system.</summary>
    public void Play(float customDelay)
    {
        onStart?.Invoke();
        StartCoroutine(WaitThenComplete(customDelay));
    }

    private System.Collections.IEnumerator WaitThenComplete(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        onDelay?.Invoke();
    }
}