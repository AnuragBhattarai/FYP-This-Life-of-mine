using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Required for Any()

public class Explosion : MonoBehaviour
{
    public float blastRadius = 5f;
    public int damageAmount = 100;
    // Changed from a single string to an array of strings to support multiple tags
    public string[] targetTags = { "PlayerUniversal" }; // Default to checking for "Enemy" tag
    public GameObject explosionEffect;

    void OnCollisionEnter(Collision collision)
    {
        //detonate
        Explode();
    }

    void Explode()
    {
        //show effect
        if(explosionEffect != null){
           GameObject explosion =  Instantiate(explosionEffect, transform.position, transform.rotation);
           Destroy(explosion, 2f);
        }

        // Find all colliders in the blast radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, blastRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            // Check if the collider has any of the correct tags
            bool isTarget = false;
            if (targetTags != null && targetTags.Length > 0)
            {
                // Use LINQ's Any() to check if the hit object's tag matches any tag in the array
                isTarget = targetTags.Any(tag => hitCollider.gameObject.CompareTag(tag));
            }

            if (isTarget)
            {
                // Try to get the Health component
                Health health = hitCollider.GetComponent<Health>();
                if (health != null)
                {
                    // Deal damage
                    health.TakeDamage(damageAmount);
                    // Updated debug log to reflect multiple tags
                    Debug.Log("Dealt " + damageAmount + " damage to " + hitCollider.gameObject.name + " with a target tag.");
                }
                else
                {
                    // Updated debug warning to reflect multiple tags
                    Debug.LogWarning("Object " + hitCollider.gameObject.name + " has a target tag but no Health component!");
                }
            }
        }
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
