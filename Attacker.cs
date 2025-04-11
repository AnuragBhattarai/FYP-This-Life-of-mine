using UnityEngine;

public class Attacker : MonoBehaviour
{
    public float attackRange = 15f; // Detection range
    public float turnSpeed = 3f; // Rotation speed

    private ProjectileFirer projectileFirer; // Reference to ProjectileFirer

    void Start()
    {
        projectileFirer = GetComponent<ProjectileFirer>();
        if (!projectileFirer) Debug.LogError("ProjectileFirer component is missing!");
    }

    void Update()
    {
        GameObject target = FindClosestEnemy();
        if (target != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            if (distanceToTarget <= attackRange)
            {
                RotateTowards(target.transform.position);
            }
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
}