using UnityEngine;

public class projectile : MonoBehaviour
{
    [SerializeField] private int damage = 100; // Damage dealt by the projectile, editable in the Inspector
    [SerializeField] private float velocityDamping = 0.98f; // Damping factor to reduce horizontal velocity over time

    private Rigidbody rb;
    private Collider projectileCollider;

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

        if (projectileCollider != null)
        {
            // Ignore collisions with other projectiles of the same type
            GameObject[] allProjectiles = GameObject.FindGameObjectsWithTag(gameObject.tag);
            foreach (GameObject projectile in allProjectiles)
            {
                Collider otherCollider = projectile.GetComponent<Collider>();
                if (otherCollider != null && otherCollider != projectileCollider)
                {
                    Physics.IgnoreCollision(projectileCollider, otherCollider);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            // Gradually reduce the horizontal velocity (X and Z axes) while keeping the vertical velocity (Y axis) unaffected
            Vector3 velocity = rb.velocity;
            velocity.x *= velocityDamping;
            velocity.z *= velocityDamping;
            rb.velocity = velocity;

            // Perform a raycast to detect collisions for fast-moving projectiles
            RaycastHit hit;
            float distance = velocity.magnitude * Time.fixedDeltaTime; // Distance the projectile will travel in this frame
            if (Physics.Raycast(transform.position, velocity.normalized, out hit, distance))
            {
                Debug.Log($"Raycast hit {hit.collider.name}");
                HandleCollision(hit.collider.gameObject);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
        {
            HandleCollision(collision.gameObject);
        }
    }

    private void HandleCollision(GameObject hitObject)
    {
        string thisObjectName = gameObject.name; // Name of the projectile
        string otherObjectName = hitObject.name; // Name of the object it hit

        Debug.Log($"{thisObjectName} hit {otherObjectName}");

        // Stop the projectile's movement by setting its velocity to zero
        if (rb != null)
        {
            rb.velocity = Vector3.zero;

            // Add random rotation to all axes
            Vector3 randomAngularVelocity = new Vector3(
                Random.Range(-10f, 10f), // Random rotation around X-axis
                Random.Range(-10f, 10f), // Random rotation around Y-axis
                Random.Range(-10f, 10f)  // Random rotation around Z-axis
            );
            rb.angularVelocity = randomAngularVelocity;
        }

        // Disable all colliders on the projectile to prevent further collisions
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        // Check if the object it hit has a Health component
        Health health = hitObject.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage); // Reduce health by the specified damage amount
        }

        // Destroy the projectile after a delay of 0.5 seconds
        Destroy(gameObject, 0.5f);
    }
}