using UnityEngine;

// Attach this script to your projectile prefab.
// Handles impact effects, damage, and self-destruction.
[RequireComponent(typeof(Rigidbody))] // Ensure a Rigidbody is attached
public class TurretProjectile : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 5f; // How long the projectile exists before being destroyed
    [SerializeField] private float damageAmount = 10f; // How much damage this projectile deals on impact

    [Header("Effects")]
    [SerializeField] private GameObject impactEffectPrefab; // Optional prefab to spawn on impact (e.g., explosion)

    private bool hasImpacted = false; // Flag to prevent multiple impacts

    void Start()
    {
        // Destroy the projectile after its lifetime
        Destroy(gameObject, lifetime);

        // Optional: Add a slight random torque or initial rotation if desired
        // Rigidbody rb = GetComponent<Rigidbody>();
        // if (rb != null)
        // {
        //     rb.angularVelocity = Random.insideUnitSphere * 5f;
        // }
    }

    // Called when the projectile physically collides with another object
    void OnCollisionEnter(Collision collision)
    {
        // Prevent multiple impact events from the same collision (e.g., hitting multiple colliders on one object)
        if (hasImpacted) return;
        hasImpacted = true;

        // --- Handle Impact ---

        // Optional: Instantiate impact effect at the collision point
        if (impactEffectPrefab != null)
        {
            ContactPoint contact = collision.contacts[0]; // Get the first contact point
            Instantiate(impactEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal)); // Spawn effect facing away from surface
        }

        // --- Apply Damage ---
        // Find a component that can receive damage on the hit object.
        // This is a common pattern; you might have an interface like IDamageable
        // or a specific script like 'Health' or 'EnemyController'.
        // For this example, let's look for a component named 'Damageable'
        // Replace 'Damageable' with the actual name of your damage-receiving script/interface.
        // Example using a hypothetical IDamageable interface:
        /*
        IDamageable damageableObject = collision.gameObject.GetComponent<IDamageable>();
        if (damageableObject != null)
        {
            damageableObject.TakeDamage(damageAmount);
        }
        */

        // Simple example checking for a specific script (replace 'EnemyHealth' with your script)
        /*
        EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
             enemyHealth.TakeDamage(damageAmount);
        }
        */

        // For a simpler example, you could check the tag of the hit object
        if (collision.gameObject.CompareTag("Enemy")) // Replace "Enemy" with the tag of objects that should take damage
        {
             // Apply damage to the object. This requires a script on the enemy
             // that has a method like TakeDamage(float amount).
             // Example:
             // EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
             // if (enemyHealth != null)
             // {
             //      enemyHealth.TakeDamage(damageAmount);
             // }
             Debug.Log($"Projectile hit object with tag 'Enemy'. Apply {damageAmount} damage here.", collision.gameObject);
             // Note: You need to implement the damage logic on the enemy/target object itself.
        }
        else
        {
             // Hit something else (ground, obstacle, etc.) - maybe just explode or stop.
             Debug.Log($"Projectile hit object: {collision.gameObject.name}", collision.gameObject);
        }


        // --- Clean up ---
        // Destroy the projectile itself after impact
        Destroy(gameObject);
    }

    // Optional: Use OnTriggerEnter instead if your projectile is a trigger
    /*
    void OnTriggerEnter(Collider other)
    {
         // Logic similar to OnCollisionEnter, but for triggers
         if (hasImpacted) return;
         hasImpacted = true;

         // ... handle damage, effects ...

         Destroy(gameObject);
    }
    */

    // Optional: Add a public method if the turret needs to set properties after instantiation
    /*
    public void SetDamage(float damage)
    {
        damageAmount = damage;
    }
    */
}