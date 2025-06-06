using UnityEngine;
using UnityEngine.AI;
using System.Linq; // Although using Linq is generally discouraged for performance in Update loops, it's currently only used in a helper function which isn't called per frame. Keeping it for now but noting the potential performance impact if used elsewhere.

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class GatlingBehaviour : MonoBehaviour
{
    [Header("Ranges")]
    [Tooltip("The maximum distance at which the character will detect and chase a target (range3).")]
    public float range_Chase = 8f;
    [Tooltip("The distance to stop moving and engage the target with ranged attacks (range2).")]
    public float range_RangedAttack = 3f;
    [Tooltip("The distance to stop ranged attacks and potentially start melee attacks (range1).")]
    public float range_MeleeAttack = 1f;

    [Header("Movement")]
    [Tooltip("Base movement speed for Chase mode. This sets the NavMeshAgent's speed when AI is active.")]
    public float moveSpeed = 3f;

    [Header("Animation Settings")]
    [Tooltip("Animator float parameter name for the movement speed (e.g., 'Speed') for blend trees.")]
    public string speedParameterName = "Speed";
    [Tooltip("Multiplier applied to the NavMeshAgent's velocity magnitude before sending it to the Animator's speed parameter.")]
    [Range(0.1f, 5f)]
    public float speedAnimationMultiplier = 1.0f;

    [Tooltip("Animator trigger name for when NO target is in range OR when stopped.")]
    public string idleTrigger = "Idle";
    [Tooltip("Animator trigger name for when actively chasing a target (distance > range_RangedAttack).")]
    public string chaseTrigger = "ChaseAnim";
    [Tooltip("Animator trigger name for when stopped at ranged attack distance (range_MeleeAttack < distance <= range_RangedAttack).")]
    public string rangedAttackTrigger = "FireAnim";
    [Tooltip("Animator trigger name for when stopped at melee attack distance (distance <= range_MeleeAttack).")]
    public string meleeAttackTrigger = "MeleeAnim";

    [Header("Animator")]
    [Tooltip("Optional: Assign an external Animator if it's on a separate object. If null, uses the Animator on this GameObject.")]
    public Animator externalAnimator;

    // Private references to components
    private Animator animator;
    private NavMeshAgent navMeshAgent;
    private Transform currentTarget = null;
    private string lastTrigger = ""; // To track the last animation trigger set by THIS script

    [Header("Target Settings")]
    [Tooltip("Tags of potential targets to detect.")]
    public string[] targetTags = { "Enemy", "Player" };

    void Awake()
    {
        // Get the Animator component. Prioritize external if assigned, otherwise get from this GameObject.
        animator = externalAnimator != null ? externalAnimator : GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"{gameObject.name}: Animator component not found. GatlingBehaviour requires an Animator.", this);
            enabled = false; // Disable this script if essential component is missing
            return;
        }

        // Get the NavMeshAgent component. This script requires it on the same GameObject.
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError($"{gameObject.name}: NavMeshAgent component not found. GatlingBehaviour requires a NavMeshAgent on the same GameObject.", this);
            enabled = false; // Disable this script if essential component is missing
            return;
        }

        // Set initial NavMeshAgent properties when AI is active
        // These will be restored in OnEnable when this script takes over control
        navMeshAgent.speed = moveSpeed;
        navMeshAgent.angularSpeed = 360;
        // REMOVED: navMeshAgent.stoppingDistance = range_RangedAttack * 0.9f; // We will manage stopping manually

        navMeshAgent.updatePosition = true;
        navMeshAgent.updateRotation = true;

        // Warning for Root Motion if not intended
        if (animator.applyRootMotion)
        {
            Debug.LogWarning($"Animator component on {gameObject.name} HAS 'Apply Root Motion' checked. This script is configured for NON-Root Motion. Please uncheck 'Apply Root Motion'.", this);
        }

        // Initialize state if enabled from the start
        // If disabled by RTSUnit in its Awake, OnEnable will handle initialization later.
        if (enabled)
        {
             // Ensure agent is stopped initially if AI starts active
            if (navMeshAgent != null)
            {
                 navMeshAgent.isStopped = true;
                 navMeshAgent.ResetPath();
            }
            // Set initial AI animation state (Idle)
            lastTrigger = idleTrigger;
            SetAnimationTrigger(idleTrigger);
        }
    }

    // OnEnable is called when the script becomes enabled (e.g., by an RTSUnit script)
    void OnEnable()
    {
        // When enabled, the AI should take over.
        // Reset its state and start looking for targets.
        currentTarget = null; // Clear current target

        // Ensure NavMeshAgent is ready for AI control
        if (navMeshAgent != null)
        {
             navMeshAgent.isStopped = true; // Start stopped
             navMeshAgent.ResetPath(); // Clear any previous path

             // Restore AI speed
             navMeshAgent.speed = moveSpeed;
             // REMOVED: navMeshAgent.stoppingDistance = range_RangedAttack * 0.9f; // We will manage stopping manually
             // Ensure agent is enabled for AI control
             navMeshAgent.enabled = true;
        }


        // Set initial AI animation state (Idle)
        // Clear the last trigger first to ensure the Idle trigger is set correctly
        lastTrigger = "";
        SetAnimationTrigger(idleTrigger);

         Debug.Log($"{gameObject.name}: GatlingBehaviour Enabled (AI Active)");
    }

    // OnDisable is called when the script becomes disabled (e.g., by an RTSUnit script)
    void OnDisable()
    {
        // When disabled, stop the agent and clear its path.
        // The RTSUnit script will take over control.
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
            // The controlling script (RTSUnit) will likely manage the agent's enabled state.
            // We don't disable it here as RTSUnit might still need it immediately.
        }
        // Do NOT reset animation triggers here; the controlling script (RTSUnit)
        // will set the appropriate animation state.
        lastTrigger = ""; // Clear the last trigger tracked by this script

        Debug.Log($"{gameObject.name}: GatlingBehaviour Disabled (AI Inactive, RTS control expected)");
    }


    void Update()
    {
        // Only run AI logic if this script is explicitly enabled.
        // This check is the core of preventing AI interference when RTS controls the unit.
        if (!enabled || navMeshAgent == null || !navMeshAgent.enabled || animator == null)
        {
            // If disabled, ensure agent is stopped and path is cleared (redundant with OnDisable but safe)
             if (navMeshAgent != null && navMeshAgent.enabled)
             {
                 navMeshAgent.isStopped = true;
                 navMeshAgent.ResetPath();
             }
            return; // Exit Update loop if AI is not active
        }

        FindClosestTarget();

        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            HandleTargetBehavior(distance);

            // Always face target when stopped for attack states
            // Check if the agent is stopped AND the current animation state is one of the attack states
            if (navMeshAgent.isStopped && currentTarget != null &&
               (lastTrigger == rangedAttackTrigger || lastTrigger == meleeAttackTrigger))
            {
                FaceTarget(currentTarget.position);
            }
        }
        else
        {
            HandleNoTarget();
        }

        // Update animation speed based on the agent's current velocity (managed by AI when enabled)
        UpdateAnimationSpeed(navMeshAgent.velocity.magnitude);
    }

    void FindClosestTarget()
    {
        // Only find target if this AI script is enabled
        if (!enabled) return;

        currentTarget = null;
        float closestDistanceSqr = Mathf.Infinity;

        // Find the closest target within the *Chase* range
        float chaseRangeSqr = range_Chase * range_Chase;

        foreach (string tag in targetTags)
        {
            GameObject[] targetsWithTag = GameObject.FindGameObjectsWithTag(tag);

            foreach (GameObject potentialTarget in targetsWithTag)
            {
                if (potentialTarget == null || !potentialTarget.activeInHierarchy) continue;

                Vector3 directionToTarget = potentialTarget.transform.position - transform.position;
                float dSqrToTarget = directionToTarget.sqrMagnitude;

                if (dSqrToTarget < chaseRangeSqr && dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    currentTarget = potentialTarget.transform;
                }
            }
        }

        // If no target is found within chase range, stop movement
        if (currentTarget == null)
        {
            // Only stop the agent and reset path if the AI is currently active
            if (enabled && navMeshAgent != null && navMeshAgent.enabled)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath(); // Clear any existing path
            }
        }
    }

    void HandleTargetBehavior(float distance)
    {
        // Only handle target behavior if this AI script is enabled
        if (!enabled || navMeshAgent == null || !navMeshAgent.enabled) return;

        // Prioritize ranges from smallest to largest distance check
        if (distance <= range_MeleeAttack)
        {
            // Melee Attack State: Stop and play melee animation
            SetAnimationTrigger(meleeAttackTrigger);
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath(); // Ensure we are stopped

        }
        else if (distance <= range_RangedAttack)
        {
            // Ranged Attack State: Stop and play ranged animation
            SetAnimationTrigger(rangedAttackTrigger);
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath(); // Ensure we are stopped
        }
        else if (distance <= range_Chase)
        {
            // Chase State: Move towards the target
            // Only start chasing if we are not already moving towards the target's position
            // This prevents constantly setting the destination each frame when chasing
            if (navMeshAgent.isStopped || (navMeshAgent.destination - currentTarget.position).sqrMagnitude > 0.1f) // Check if destination needs updating
            {
                SetAnimationTrigger(chaseTrigger);
                navMeshAgent.SetDestination(currentTarget.position);
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed; // Ensure AI speed is set when chasing
            }
        }
        // If distance is greater than range_Chase, HandleNoTarget will be called by Update
    }

    void HandleNoTarget()
    {
        // Only handle no target if this AI script is enabled
        if (!enabled || navMeshAgent == null || !navMeshAgent.enabled) return;

        // No Target State: Stop movement and play idle animation
        SetAnimationTrigger(idleTrigger);
        // Only stop the agent and reset path if the AI is currently active
        if (navMeshAgent.enabled && !navMeshAgent.isStopped) // Only stop if not already stopped
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }
    }

    void FaceTarget(Vector3 targetPosition)
    {
        // Only face target if we are stopped (in an attack state) and the AI is enabled
        if (!enabled || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isStopped) return;

        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0; // Keep rotation on the Y axis

        if (direction.sqrMagnitude > 0.001f) // Check if direction is significant
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            // Smoothly rotate towards the target
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * navMeshAgent.angularSpeed / 10);
        }
    }

    void UpdateAnimationSpeed(float currentAgentSpeed)
    {
        // Only set animation speed if this AI script is enabled and managing animations
        if (!enabled || animator == null || string.IsNullOrEmpty(speedParameterName)) return;

        float animationSpeed = currentAgentSpeed * speedAnimationMultiplier;
        if (HasParameter(speedParameterName, animator, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(speedParameterName, animationSpeed);
        }
        else
        {
            // Only warn if the parameter name is set in the inspector but not found on the animator
            // and the script is enabled (to avoid warnings when disabled intentionally)
             if (enabled)
             {
                 Debug.LogWarning($"Animator float parameter '{speedParameterName}' not found or is not a Float type on {gameObject.name}'s Animator. Animation speed will not be updated.", this);
             }
        }
    }

    void SetAnimationTrigger(string triggerName)
    {
        // Only set the trigger if this AI script is enabled,
        // it's different from the last one set by THIS script, and the name is valid
        if (!enabled || animator == null || string.IsNullOrEmpty(triggerName) || lastTrigger == triggerName) return;

        if (HasParameter(triggerName, animator, AnimatorControllerParameterType.Trigger))
        {
            // Reset the previous trigger set by THIS script if it exists and is valid
            if (!string.IsNullOrEmpty(lastTrigger) && HasParameter(lastTrigger, animator, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(lastTrigger);
            }
            animator.SetTrigger(triggerName);
            lastTrigger = triggerName; // Store the trigger set by THIS script
        }
        else
        {
            // Only warn if the trigger name is set in the inspector but not found on the animator
            // and the script is enabled (to avoid warnings when disabled intentionally)
            if (enabled)
            {
                 Debug.LogWarning($"Animator trigger '{triggerName}' not found or is not a Trigger type on {gameObject.name}'s Animator. AI animation will not be set.", this);
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
        // Draw Melee Attack Range (range1)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range_MeleeAttack);

        // Draw Ranged Attack Range (range2)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range_RangedAttack);

        // Draw Chase Range (range3)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, range_Chase);

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        // Draw NavMeshAgent path (only if AI is active and has a path)
        if (enabled && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.hasPath)
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