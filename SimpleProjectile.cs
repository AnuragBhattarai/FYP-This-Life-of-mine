using UnityEngine;

// Attach this script to your Projectile Prefab.
// The Prefab MUST have a Rigidbody component (with "Use Gravity" enabled)
// and a Collider component (e.g., SphereCollider, CapsuleCollider).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class SimpleProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f; // How many seconds before the projectile destroys itself automatically

    [Header("Effects (Optional)")]
    [SerializeField] private GameObject hitEffectPrefab; // e.g., Particle system or decal to spawn on impact

    void Awake()
    {
        // Ensure the Rigidbody uses gravity (the firing script calculates trajectory based on this)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) // Should always be true due to RequireComponent
        {
            rb.useGravity = true;
        }

        // Automatically destroy the projectile after a set time to prevent cluttering the scene
        Destroy(gameObject, maxLifetime);
    }

    // Called when this Collider/Rigidbody has begun touching another Rigidbody/Collider
    void OnCollisionEnter(Collision collision)
    {
        // --- Impact Handling ---

        // Optional: Instantiate a visual effect at the point of impact
        if (hitEffectPrefab != null)
        {
            // Get the first contact point information
            ContactPoint contact = collision.GetContact(0);
            // Instantiate the effect at the contact point, oriented outwards from the surface
            Instantiate(hitEffectPrefab, contact.point + contact.normal * 0.01f, Quaternion.LookRotation(contact.normal));
        }

        // Optional: Add damage logic here
        // Example: Check if the hit object has a specific tag or component
        // if (collision.gameObject.CompareTag("Enemy")) {
        //     collision.gameObject.SendMessage("TakeDamage", 10, SendMessageOptions.DontRequireReceiver);
        // }
        // Debug.Log($"Projectile hit: {collision.gameObject.name}");

        // Destroy the projectile itself immediately upon impact
        Destroy(gameObject);
    }

    // Note: If your projectile should pass *through* certain objects,
    // you might set its collider to be a Trigger and use OnTriggerEnter instead of OnCollisionEnter.
    // void OnTriggerEnter(Collider other) { /* Handle trigger logic */ Destroy(gameObject); }
}
