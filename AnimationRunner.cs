using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

public class AnimationRunner : MonoBehaviour
{
    private Animator animator; // Reference to the Animator component

    void Start()
    {
        // Get the Animator component attached to the same GameObject
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Animator component is missing on the AnimationRunner!");
        }

        // Find the ProjectileFirer in the scene and subscribe to its event
        ProjectileFirer projectileFirer = FindObjectOfType<ProjectileFirer>();
        if (projectileFirer != null)
        {
            projectileFirer.OnProjectileFiredEvent.AddListener(OnProjectileFired);
        }
        else
        {
            Debug.LogWarning("No ProjectileFirer found in the scene!");
        }
    }

    /// <summary>
    /// This method is called when the ProjectileFirer broadcasts the "OnProjectileFired" event.
    /// </summary>
    void OnProjectileFired()
    {
        if (animator != null)
        {
            animator.SetTrigger("Fire"); // Trigger the "Fire" animation
        }

        // Print a message to indicate the event was detected
        Debug.Log("Projectile is fired is detected");
        PlaySoundEffect(); // Play the sound effect when the projectile is fired
    }
    void Update()
{
    if (Input.GetKeyDown(KeyCode.P))
    {
        if (animator != null)
        {
            animator.SetTrigger("Fire"); // Trigger the "Fire" animation
        }

        // Print a message to indicate the key press was detected
        Debug.Log("P key pressed, triggering Fire animation");
    }
}
// Add play sound effect when projectile is fired
[SerializeField]
private AudioSource audioSource; // Assignable in the Inspector

[SerializeField]
private AudioClip fireSoundEffect; // Assignable in the Inspector

void PlaySoundEffect()
{
    if (audioSource == null)
    {
        // Attempt to find an AudioSource component in the scene
        audioSource = FindObjectOfType<AudioSource>();
    }

    if (audioSource != null && fireSoundEffect != null)
    {
        // Play the sound effect as a one-shot, allowing it to overlap with any currently playing sound
        audioSource.PlayOneShot(fireSoundEffect);
    }
    else
    {
        Debug.LogWarning("AudioSource component is missing in the scene or AudioClip is not assigned!");
    }
}
}


