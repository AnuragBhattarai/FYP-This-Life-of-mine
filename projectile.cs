using UnityEngine;

public class projectile : MonoBehaviour
{
    [SerializeField] private int damage = 100; // Damage dealt by the projectile, editable in the Inspector
    [SerializeField] private float expansionDuration = 1f; // Time (in seconds) for the explosion radius to expand
    [SerializeField] private float maxRadius = 5f; // Maximum radius of the explosion
    [SerializeField] private AudioClip hitSound; // Sound to play when the projectile hits something
    [SerializeField] private AudioSource audioSource; // AudioSource to play the sound

    private Rigidbody rb;
    private Collider projectileCollider;
    private bool isExpanding = false; // Tracks whether the projectile is in the explosion phase
    private float currentRadius = 0f; // Tracks the current radius of the explosion

    private void Start()
    {





        
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing on the projectile!");
        }
        else
        {
            // Enable Continuous Collision Detection for fast-moving projectiles
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (audioSource == null)
        {
            Debug.LogError("AudioSource is not assigned to the projectile!");
        }
    }

    private void FixedUpdate()
    {
        if (!isExpanding && rb != null)
        {
            // Perform a raycast to detect collisions for fast-moving projectiles
            RaycastHit hit;
            float distance = rb.linearVelocity.magnitude * Time.fixedDeltaTime; // Distance the projectile will travel in this frame
            if (Physics.Raycast(transform.position, rb.linearVelocity.normalized, out hit, distance))
            {
                Debug.Log($"Raycast hit {hit.collider.name}");
                HandleCollision(hit.collider.gameObject);
            }
        }
    }

    private void StartExplosion()
    {
        isExpanding = true;

        // Stop all movement and momentum
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Disable the projectile's collider
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        // Start the explosion process
        StartCoroutine(ExpandAndDamage());
    }

    private System.Collections.IEnumerator ExpandAndDamage()
    {
        float elapsedTime = 0f;

        while (elapsedTime < expansionDuration)
        {
            // Gradually increase the explosion radius
            currentRadius = Mathf.Lerp(0f, maxRadius, elapsedTime / expansionDuration);

            // Apply damage to all objects within the current radius
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, currentRadius);
            foreach (Collider hitCollider in hitColliders)
            {
                Health health = hitCollider.GetComponent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Destroy the projectile after the explosion
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isExpanding && collision != null)
        {
            HandleCollision(collision.gameObject);

            // Delay the sound by 2 seconds
            StartCoroutine(PlayHitSoundWithDelay());
        }
    }

    private void HandleCollision(GameObject hitObject)
    {
        string thisObjectName = gameObject.name; // Name of the projectile
        string otherObjectName = hitObject.name; // Name of the object it hit

        Debug.Log($"{thisObjectName} hit {otherObjectName}");

        // Start the explosion process
        StartExplosion();
    }

    private System.Collections.IEnumerator PlayHitSoundWithDelay()
    {
        yield return new WaitForSeconds(2f); // Wait for 2 seconds

        if (hitSound != null)
        {
            // Create a temporary GameObject to play the sound
            GameObject tempAudioObject = new GameObject("TempAudio");
            AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();

            // Configure the temporary AudioSource
            tempAudioSource.clip = hitSound;
            tempAudioSource.spatialBlend = 1f; // Set spatial blend to 3D sound (if applicable)
            tempAudioSource.priority = 128; // Set a medium priority to avoid overriding other important sounds
            tempAudioSource.Play();

            // Destroy the temporary GameObject after the sound finishes playing
            Destroy(tempAudioObject, hitSound.length);
        }
        else
        {
            Debug.LogWarning("hitSound is not assigned!");
        }
    }

    private void OnDrawGizmos()
    {
        if (isExpanding)
        {
            // Draw a wireframe sphere to visualize the explosion radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, currentRadius);
        }
    }
}