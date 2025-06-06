using UnityEngine;

public class Attacker : MonoBehaviour
{
    public float attackRange = 15f; // Detection range
    public float minAttackRange = 3f; // Minimum range to avoid aiming at close targets
    public float turnSpeed = 3f; // Rotation speed

    private ProjectileFirer projectileFirer; // Reference to ProjectileFirer
    private Quaternion defaultRotation; // Default rotation of the attacker

    void Start()
    {
        projectileFirer = GetComponent<ProjectileFirer>();
        if (!projectileFirer) Debug.LogError("ProjectileFirer component is missing!");

        // Store the default rotation at the start
        defaultRotation = transform.rotation;
    }

    void Update()
    {
        GameObject target = FindClosestEnemy();
        if (target != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            if (distanceToTarget <= attackRange && distanceToTarget > minAttackRange)
            {
                RotateTowards(target.transform.position);
            }
        }
        else
        {
            // Rotate back to the default position if no target is found
            RotateToDefault();
        }
    }

    GameObject FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closest = null;
        float closestDistance = attackRange;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            // Skip enemies that are within the minimum attack range
            if (distance < minAttackRange)
            {
                continue;
            }

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }
        }
        return closest;
    }

    void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    void RotateToDefault()
    {
        // Smoothly rotate back to the default rotation
        transform.rotation = Quaternion.RotateTowards(transform.rotation, defaultRotation, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        // Draw the attack range as a green wireframe sphere
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw the minimum attack range as a red wireframe sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minAttackRange);
    }
}