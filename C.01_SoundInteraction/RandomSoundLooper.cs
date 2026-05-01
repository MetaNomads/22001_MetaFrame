using UnityEngine;
using System.Collections;

public class RandomWaitLoop : MonoBehaviour
{
    public AudioSource audioSource; // Reference to the AudioSource component
    public float minWaitTime = 1.0f; // Minimum wait time in seconds
    public float maxWaitTime = 5.0f; // Maximum wait time in seconds

    private float randomWaitTime;
    private float randomStartTime;

    void Start()
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource is not assigned.");
            return;
        }

        // Start the coroutine
        StartCoroutine(WaitForRandomTimeAndLoop());
    }

    private IEnumerator WaitForRandomTimeAndLoop()
    {
        while (true)
        {
            // FIX (T3-2): clip-null guard. The Start() check covers `audioSource`
            // but not `audioSource.clip`. A clip-less AudioSource that becomes
            // playing somehow (or a clip nulled mid-scene) would NRE on .clip.length.
            if (audioSource == null || audioSource.clip == null) { yield return null; continue; }

            // Check if audio is playing
            if (audioSource.isPlaying)
            {
                // Calculate a random wait time
                randomWaitTime = Random.Range(minWaitTime, maxWaitTime);

                // Wait for the random time
                yield return new WaitForSeconds(randomWaitTime);

                // Check again if audio is still playing
                if (audioSource.isPlaying && audioSource.clip != null)
                {
                    // Set a random start time within the audio clip length
                    randomStartTime = Random.Range(0f, audioSource.clip.length);

                    // Set the audio clip's time to the random start time
                    audioSource.time = randomStartTime;
                }
            }
            else
            {
                // Wait a bit before checking again if the audio is not playing
                yield return new WaitForSeconds(3f);
            }
        }
    }
}
