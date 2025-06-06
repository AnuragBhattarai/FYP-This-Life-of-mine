using UnityEngine;
using UnityEngine.Events;

public class ProjectileFirer : MonoBehaviour
{
    public GameObject projectilePrefab; // The prefab to throw
    public float throwSpeed = 10f; // Speed of the thrown projectile
    public float bulletsPerSecond = 2f; // Number of bullets fired per second
    public float rayDistance = 20f; // Distance of the raycast
    public string targetTag = "Enemy"; // Tag to detect valid targets
    public float firingAngle = 30f; // Angle in degrees within which the projectile can fire

    [Tooltip("Offset for the projectile's spawn position relative to the firing object")]
    public Vector3 projectileSpawnOffset = new Vector3(0f, 0.5f, 1f); // Editable offset in the Inspector

    private float attackCooldown = 0f; // Tracks time since the last attack

    // UnityEvent to notify listeners when a projectile is fired
    public UnityEvent OnProjectileFiredEvent;

    void Start()
    {
        if (OnProjectileFiredEvent == null)
        {
            OnProjectileFiredEvent = new UnityEvent();
        }
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime; // Decrease cooldown over time

        // Perform a raycast directly forward
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            // Check if the hit object has the specified tag
            if (hit.collider.CompareTag(targetTag))
            {
                Debug.Log($"Ray hit: {hit.collider.name}");

                // Fire projectile if cooldown is complete
                if (attackCooldown <= 0f)
                {
                    FireProjectile();
                    attackCooldown = 1f / bulletsPerSecond; // Reset the cooldown based on bullets per second
                }
            }
        }
    }

    void FireProjectile()
    {
        if (projectilePrefab != null)
        {
            // Calculate the spawn position with the offset
            Vector3 spawnPosition = transform.position + transform.TransformDirection(projectileSpawnOffset);

            // Instantiate the projectile at the calculated position and rotation of this object
            GameObject projectile = Instantiate(projectilePrefab, spawnPosition, transform.rotation);
            Rigidbody rb = projectile.GetComponent<Rigidbody>();

            if (rb != null)
            {
                // Apply velocity to the projectile
                rb.linearVelocity = transform.forward * throwSpeed;

                Debug.Log("Projectile fired!");

                // Broadcast the event
                OnProjectileFiredEvent.Invoke();
            }
        }
        else
        {
            Debug.LogWarning("Projectile Prefab is not assigned!");
        }
    }

    public bool IsTargetInSight(GameObject target)
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            // Check if the raycast hit the target
            return hit.collider.gameObject == target;
        }
        return false;
    }
}
