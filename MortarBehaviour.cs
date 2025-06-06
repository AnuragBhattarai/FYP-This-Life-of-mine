using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Linq;

// Ensure this GameObject has an Animator and a NavMeshAgent component
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class MortarBehavior : MonoBehaviour
{
    [Header("Ranges")]
    [Tooltip("Inner range (e.g., Evade). If target is closer than this, move away.")]
    [Range(0.1f, 20f)]
    public float range1_Evade = 3f; // Red Gizmo
    [Tooltip("Middle range (e.g., Fire Mode). If target is within this range (but outside Evade range), stop movement and fire.")]
    [Range(1f, 30f)]
    public float range2_Fire = 8f; // Yellow Gizmo
    [Tooltip("Outer range (e.g., Normal Chase/Detection). If target is within this range (but outside Fire range), chase.")]
    [Range(5f, 50f)]
    public float range3_Chase = 15f; // Blue Gizmo

    [Header("Movement")]
    [Tooltip("Base movement speed for Chase and Evade modes. This sets the NavMeshAgent's speed.")]
    [Range(0f, 10f)]
    public float moveSpeed = 3f;
    // Rotation speed is controlled by the NavMeshAgent's Angular Speed property.
    [Tooltip("The distance to attempt to evade to when the target is within the evade range.")]
    [Range(5f, 30f)]
    public float evadeDistance = 10f; // How far to try and evade to

    [Header("Animation Settings")]
    [Tooltip("Animator trigger name for the movement speed parameter (e.g., 'Speed'). Float parameter for blend tree.")]
    public string speedParameterName = "Speed"; // Name of the float parameter in your Animator
    [Tooltip("Multiplier applied to the NavMeshAgent's velocity magnitude before sending it to the Animator's speed parameter.")]
    [Range(0.1f, 5f)]
    public float speedAnimationMultiplier = 1.0f;

    [Tooltip("Animator trigger name for when NO target is in range.")]
    public string idleTrigger = "Idle";
    [Tooltip("Animator trigger name for Range 1 (Inner/Evade).")]
    public string evadeTrigger = "EvadeAnim";
    [Tooltip("Animator trigger name for Range 2 (Middle/Fire Mode).")]
    public string fireTrigger = "FireAnim";
    [Tooltip("Animator trigger name for Range 3 (Outer/Chase).")]
    public string chaseTrigger = "ChaseAnim";

    [Header("Animator")]
    [Tooltip("Optional: Assign an external Animator if it's on a separate object. If null, uses the Animator on this GameObject.")]
    public Animator externalAnimator;

    // --- Private Variables ---
    private Animator animator;
    private NavMeshAgent navMeshAgent;

    [Header("Target Settings")]
    [Tooltip("Tags of potential targets to detect.")]
    public string[] targetTags = { "Enemy", "Player" }; // Example tags, adjust as needed
    // currentTarget will now strictly be the nearest target found in range each frame check.
    private Transform currentTarget = null;
    private string lastTrigger = "";

    void Awake()
    {
        animator = externalAnimator != null ? externalAnimator : GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("MortarBehavior requires an Animator component.", this);
            this.enabled = false;
            return;
        }

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError("MortarBehavior requires a NavMeshAgent component.", this);
            this.enabled = false;
            return;
        }

        // Configure NavMeshAgent properties for non-Root Motion
        navMeshAgent.speed = moveSpeed;
        // NavMeshAgent handles rotation directly. Angular speed controls how fast it turns.
        navMeshAgent.angularSpeed = 360; // High value for quick turning, adjust as needed
        navMeshAgent.stoppingDistance = 0.1f;

        // Enable NavMeshAgent to directly control the Transform's position and rotation
        navMeshAgent.updatePosition = true; // Let the agent move the transform
        navMeshAgent.updateRotation = true; // Let the agent rotate the transform

        // Ensure Apply Root Motion is OFF on the Animator in the Inspector for this setup
        if (animator.applyRootMotion)
        {
            Debug.LogWarning($"Animator component on {gameObject.name} HAS 'Apply Root Motion' checked, but the script is configured for NON-Root Motion. Please uncheck 'Apply Root Motion'.", this);
        }

        lastTrigger = idleTrigger;
        SetAnimationTrigger(idleTrigger);

        // Start the agent in a stopped state
        navMeshAgent.isStopped = true;
    }

    // No OnAnimatorMove method in non-Root Motion setup.

    void Update()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || animator == null || !animator.enabled) return;

        // 1. Find the closest target in range (always the nearest with this logic)
        FindClosestTarget(); // Reverted to original function name

        // 2. Determine and execute behavior based on the target's presence and distance
        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            HandleTargetInRange(distance);

            // Manually face the target ONLY when stopped (e.g., in Fire mode),
            // as the agent handles rotation when moving.
             if (navMeshAgent.isStopped && lastTrigger == fireTrigger && currentTarget != null)
             {
                 FaceTarget(currentTarget.position); // Manual facing
             }
        }
        else
        {
            // No target in range - handle idle state
            HandleNoTarget();
            // When idle and stopped, maintain current rotation (NavMeshAgent won't rotate it)
        }

        // Update animation speed based on agent's actual velocity magnitude
        UpdateAnimationSpeed(navMeshAgent.velocity.magnitude);
    }

    // Finds the absolutely closest target among all potential targets within the chase range.
    // Does NOT stick to a previous target if a new, closer one appears.
    void FindClosestTarget() // Reverted to original function name
    {
        // Always start the search assuming no target is found initially
        currentTarget = null;
        float closestDistanceSqr = Mathf.Infinity; // Use squared distance for performance

        // Iterate through each tag we are looking for
        foreach (string tag in targetTags)
        {
            // Find all GameObjects currently in the scene with the given tag
            GameObject[] targetsWithTag = GameObject.FindGameObjectsWithTag(tag);

            // Iterate through all found potential targets
            foreach (GameObject potentialTarget in targetsWithTag)
            {
                // Defensive check in case a target object was destroyed or is inactive
                if (potentialTarget == null || !potentialTarget.activeInHierarchy) continue;

                // Calculate the squared distance to the potential target
                Vector3 directionToTarget = potentialTarget.transform.position - transform.position;
                float dSqrToTarget = directionToTarget.sqrMagnitude;

                // Check if this target is within our outer detection range (squared for efficiency)
                // AND if it's closer than the closest target found so far.
                if (dSqrToTarget < range3_Chase * range3_Chase && dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget; // Update the closest distance found
                    currentTarget = potentialTarget.transform; // Set this as the current closest target found *so far*
                }
            }
        }
        // After checking all tags and potential targets, currentTarget is the absolute closest valid target found within range, or null if none were found.

        // If no target was found in this check, ensure the agent stops.
        // This is also handled by HandleNoTarget, but reinforces the logic here.
        if (currentTarget == null)
        {
             // Debug.Log($"{gameObject.name}: No targets found in range."); // Uncomment for debugging
             navMeshAgent.isStopped = true;
             navMeshAgent.ResetPath();
        }
    }

    // Handles the character's behavior based on the distance to the current target.
    void HandleTargetInRange(float distance)
    {
        if (distance <= range1_Evade)
        {
            // Evade
            SetAnimationTrigger(evadeTrigger);

            Vector3 directionAway = (transform.position - currentTarget.position).normalized;
            Vector3 evadeTargetPosition = transform.position + directionAway * evadeDistance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(evadeTargetPosition, out hit, evadeDistance * 1.5f, NavMesh.AllAreas))
            {
                navMeshAgent.SetDestination(hit.position);
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                 // Agent handles rotation when moving
            }
            else
            {
                // Couldn't find a valid evade position, stop
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
                SetAnimationTrigger(idleTrigger); // Fallback
                // Agent is stopped, maintain current rotation
            }
        }
        else if (distance <= range2_Fire)
        {
            // Fire Mode
            SetAnimationTrigger(fireTrigger);

            // Stop the NavMeshAgent from moving
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath(); // Clear path
            // Manual facing is handled in Update when agent is stopped and in Fire state
        }
        else // Chase
        {
            // Chase
            SetAnimationTrigger(chaseTrigger);
            navMeshAgent.SetDestination(currentTarget.position);
            navMeshAgent.isStopped = false;
            navMeshAgent.speed = moveSpeed;
            // Agent handles rotation when moving
        }
    }

    // Handles the character's behavior when no target is found.
    void HandleNoTarget()
    {
        // No target
        SetAnimationTrigger(idleTrigger);
        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath(); // Clear path
        // Agent is stopped, maintain current rotation
    }

    // Manually rotates the character smoothly to face the target.
    // Only used when navMeshAgent.updateRotation is false or the agent is stopped.
    // In this non-Root Motion version, used when the agent is stopped in Fire mode.
    void FaceTarget(Vector3 targetPosition)
    {
        // Only manually rotate if the agent is currently stopped
        if (navMeshAgent != null && !navMeshAgent.isStopped) return;

        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * navMeshAgent.angularSpeed / 10);
        }
    }

    // Updates a float parameter in the Animator based on the agent's actual speed.
    void UpdateAnimationSpeed(float currentAgentSpeed)
    {
        if (animator != null && navMeshAgent != null)
        {
             float animationSpeed = currentAgentSpeed * speedAnimationMultiplier;

            if (HasParameter(speedParameterName, animator, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(speedParameterName, animationSpeed);
            }
            else
            {
                 Debug.LogWarning($"Animator float parameter '{speedParameterName}' not found or is not a Float type on {gameObject.name}'s Animator. Animation speed will not be updated.", this);
            }
        }
    }

    // Helper to set Animator Triggers safely.
    void SetAnimationTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName) && lastTrigger != triggerName)
        {
             if (HasParameter(triggerName, animator, AnimatorControllerParameterType.Trigger))
             {
                if (!string.IsNullOrEmpty(lastTrigger) && HasParameter(lastTrigger, animator, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(lastTrigger);
                }
                animator.SetTrigger(triggerName);
                lastTrigger = triggerName;
             }
             else
             {
                  Debug.LogWarning($"Animator trigger parameter '{triggerName}' not found or is not a Trigger type on {gameObject.name}'s Animator. Trigger will not be set.", this);
             }
        }
    }

    // Helper to check if an Animator parameter exists by name and type.
    private bool HasParameter(string paramName, Animator animator, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName && param.type == type)
            {
                return true;
            }
        }
        return false;
    }

    // Draws debug gizmos.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range1_Evade);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range2_Fire);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, range3_Chase);

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            Gizmos.color = Color.black;
            Vector3[] pathCorners = navMeshAgent.path.corners;
            for (int i = 0; i < pathCorners.Length - 1; i++)
            {
                Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);
            }
        }
    }
}