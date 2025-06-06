using UnityEngine;
using System.Linq; // Required for OrderBy
using System.Collections.Generic; // Required for List

// Ensure this GameObject has an Animator component
[RequireComponent(typeof(Animator))]
public class SpiderBehaviour : MonoBehaviour
{
    [Header("Ranges")]
    [Tooltip("Inner range (e.g., Attack). Movement stops.")]
    public float range1_Attack = 5f; // Red Gizmo
    [Tooltip("Middle range (e.g., Alert/Ready). Movement stops.")]
    public float range2_Alert = 10f; // Yellow Gizmo
    [Tooltip("Outer range (e.g., Normal Chase/Detection).")]
    public float range3_Chase = 15f; // Blue Gizmo

    [Header("Movement")]
    [Tooltip("Base movement speed.")]
    public float moveSpeed = 3f;
    [Tooltip("How quickly the character turns to face the target.")]
    public float rotationSpeed = 5f;

    [Header("Targeting")]
    [Tooltip("The tags to search for targets (e.g., 'Player', 'Enemy').")]
    public string[] targetTags = { "PlayerUniversal", "PlayerMainUnit" }; // Array to hold multiple target tags

    [Header("Animation Triggers")]
    [Tooltip("Animator trigger name for when NO target is in range.")]
    public string idleTrigger = "Idle";
    [Tooltip("Animator trigger name for Range 1 (Inner/Attack).")]
    public string range1Trigger = "AttackAnim";
    [Tooltip("Animator trigger name for Range 2 (Middle/Alert).")]
    public string range2Trigger = "AlertAnim";
    [Tooltip("Animator trigger name for Range 3 (Outer/Chase).")]
    public string range3Trigger = "ChaseAnim";

    [Header("Animator")]
    [Tooltip("Optional: Assign an external Animator if it's on a separate object.")]
    public Animator externalAnimator; // New field for assigning an external Animator

    // --- Private Variables ---
    private Animator animator;
    private Transform currentTarget = null;
    private string lastTrigger = ""; // Keep track of the last trigger to avoid spamming

    void Awake()
    {
        // Use the external Animator if assigned, otherwise get the Animator on this GameObject
        animator = externalAnimator != null ? externalAnimator : GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("SpiderBehaviour requires an Animator component.", this);
            this.enabled = false; // Disable script if Animator is missing
        }
        lastTrigger = idleTrigger; // Start assuming idle
    }

    void Update()
    {
        // 1. Find the closest target within the maximum range (range3)
        FindClosestTarget();

        // 2. Perform actions based on whether a target is found and its distance
        if (currentTarget != null)
        {
            // Target found - determine behaviour based on distance
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            HandleTargetInRange(distance);
        }
        else
        {
            // No target in range - handle idle state
            HandleNoTarget();
        }
    }

    void FindClosestTarget()
    {
        // Reset current target before searching
        currentTarget = null;
        float closestDistanceSqr = Mathf.Infinity; // Use squared distance for efficiency

        // Collect all potential targets from the specified tags
        List<GameObject> potentialTargets = new List<GameObject>();
        if (targetTags != null)
        {
            foreach (string tag in targetTags)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    // Find all active GameObjects with the specified tag
                    // Using FindGameObjectsWithTag is less performant than other methods (like a centralized manager)
                    // but acceptable for periodic searches if needed.
                    GameObject[] objectsWithTag = GameObject.FindGameObjectsWithTag(tag);
                    potentialTargets.AddRange(objectsWithTag);
                }
            }
        }

        // Find the closest active target among the potential targets within the max range
        // Filter out null or inactive targets first
        currentTarget = potentialTargets
            .Where(t => t != null && t.activeInHierarchy)
            .Where(t => (t.transform.position - transform.position).sqrMagnitude < range3_Chase * range3_Chase) // Filter within max range
            .OrderBy(t => (t.transform.position - transform.position).sqrMagnitude) // Order by squared distance
            .Select(t => t.transform) // Select the Transform
            .FirstOrDefault(); // Take the first (closest) one, or null if list is empty


        if (currentTarget == null && targetTags.Length > 0 && targetTags.Any(tag => !string.IsNullOrEmpty(tag)))
        {
            // Only warn if tags were actually specified but no valid targets were found within max range
            // Debug.LogWarning($"SpiderBehaviour: No active targets found within range {range3_Chase} with tags {string.Join(", ", targetTags)}.");
        }
    }


    void HandleTargetInRange(float distance)
    {
        // Make the character face the target
        FaceTarget(currentTarget.position);

        // Determine action based on distance ranges
        if (distance <= range1_Attack)
        {
            // --- Range 1: Inner / Attack ---
            // Stop movement
            MoveTowardsTarget(0f);
            SetAnimationTrigger(range1Trigger);
        }
        else if (distance <= range2_Alert)
        {
            // --- Range 2: Middle / Alert ---
            // Stop movement
            MoveTowardsTarget(0f);
            SetAnimationTrigger(range2Trigger);
        }
        else // distance > range2_Alert but <= range3_Chase
        {
            // --- Range 3: Outer / Chase ---
            MoveTowardsTarget(1.0f); // Normal speed
            SetAnimationTrigger(range3Trigger);
        }
    }

    void HandleNoTarget()
    {
        // No target found within range3_Chase
        // Set animation to Idle
        SetAnimationTrigger(idleTrigger);
        // Ensure no movement if idle
        MoveTowardsTarget(0f);
    }

    void MoveTowardsTarget(float speedMultiplier)
    {
        // Don't move if the speed multiplier is zero or no target
        if (speedMultiplier <= 0f || currentTarget == null) return;

        // Calculate direction towards the target (ignoring y-axis for ground movement)
        Vector3 direction = (currentTarget.position - transform.position);
        direction.y = 0; // Keep movement horizontal
        if (direction.sqrMagnitude < 0.001f) return; // Avoid moving if already at target (squared distance check)

        direction.Normalize(); // Make it a unit vector

        // Calculate movement step
        Vector3 movement = direction * moveSpeed * speedMultiplier * Time.deltaTime;

        // Apply movement
        // Consider using Rigidbody.MovePosition if you have a Rigidbody and need physics interaction
        transform.position += movement;
    }

    void FaceTarget(Vector3 targetPosition)
    {
        // Calculate the direction to look at (ignoring y-axis difference)
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0; // Don't tilt up/down

        // If the direction is not zero (i.e., not already at the target position)
        if (direction.sqrMagnitude > 0.001f) // Use squared magnitude check
        {
            // Calculate the rotation needed to look in that direction
            Quaternion lookRotation = Quaternion.LookRotation(direction);

            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    // Helper function to set triggers and avoid spamming the same trigger
    void SetAnimationTrigger(string triggerName)
    {
        // Only set the trigger if it's different from the last one set
        if (animator != null && !string.IsNullOrEmpty(triggerName) && lastTrigger != triggerName)
        {
            // Reset the previously set trigger. This is good practice, although triggers auto-reset.
            // This handles cases where you might transition *out* of a state before the trigger fires.
            if (!string.IsNullOrEmpty(lastTrigger)) // Only reset if there was a previous trigger
            {
                // It's generally safe to reset a trigger even if it wasn't active.
                animator.ResetTrigger(lastTrigger);
            }

            animator.SetTrigger(triggerName);
            lastTrigger = triggerName; // Remember the last trigger set
            // Debug.Log($"Set Trigger: {triggerName}"); // Uncomment for debugging
        }
        // If the trigger is the same, we do nothing, assuming the animation is looping or ongoing.
        // If triggerName is empty or animator is null, nothing happens.
    }


    // Draw Gizmos in the Scene view for easier range visualization
    void OnDrawGizmosSelected()
    {
        // Draw Inner Range (Attack) - Red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range1_Attack);

        // Draw Middle Range (Alert) - Yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range2_Alert);

        // Draw Outer Range (Chase) - Blue
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, range3_Chase);

        // Draw line to current target if one exists
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}