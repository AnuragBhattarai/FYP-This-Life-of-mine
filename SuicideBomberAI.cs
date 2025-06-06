using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(RTSUnit))]
public class SuicideBomberAI : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("List of tags for potential target GameObjects. The bomber will chase the nearest one.")]
    public List<string> targetTags = new List<string> { "Player", "ImportantBuilding" };
    private Transform target;

    [Header("Behavior Ranges")]
    [Tooltip("Distance to target for detonation (Range 1).")]
    public float range1_Detonate = 2.0f;
    [Tooltip("Distance to target to trigger jump animation and speed boost (Range 2).")]
    public float range2_Jump = 8.0f;
    [Tooltip("Distance to target to trigger chase behavior (Range 3).")]
    public float range3_Chase = 20.0f;

    [Header("Movement Settings")]
    [Tooltip("Speed when chasing the target in Range 3.")]
    public float chaseSpeed = 4.0f;
    [Tooltip("Multiplier for horizontal speed during the jump sequence (Range 2).")]
    public float jumpBoostSpeedMultiplier = 1.5f;
    [Tooltip("How fast the bomber rotates towards the target.")]
    public float rotationSpeed = 5f;

    [Header("Jump Settings")]
    [Tooltip("The peak height of the jump arc relative to the straight line between start and end.")]
    public float jumpHeight = 3.0f;
    [Tooltip("How long the jump animation/sequence takes.")]
    public float jumpDuration = 1.0f;

    [Header("Detonation Settings")]
    [Tooltip("Damage dealt to objects within the detonation radius.")]
    public int detonationDamage = 50;
    [Tooltip("Prefab for the visual explosion effect.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Audio clip for the explosion sound.")]
    public AudioClip explosionSFX;
    [Tooltip("Layer(s) that the explosion damage will affect.")]
    public LayerMask damageableLayers;

    [Header("Animation Settings")]
    [Tooltip("Animator trigger name for the jump animation.")]
    public string jumpAnimationTrigger = "Jump";
    [Tooltip("Animator trigger name for the run animation.")]
    public string runAnimationTrigger = "Run";
    [Tooltip("Animator trigger name for the idle animation.")]
    public string idleAnimationTrigger = "Idle";
    [Tooltip("Animator float parameter name for movement speed.")]
    public string speedAnimationParameter = "Speed";

    [Header("AI Update Rate")]
    [Tooltip("How often the AI logic (finding target, checking ranges) runs in seconds.")]
    public float aiUpdateInterval = 0.2f;
    private float aiUpdateTimer = 0f;

    private NavMeshAgent agent;
    private Animator animator;
    private RTSUnit rtsUnit;
    private Collider bomberCollider;

    private enum BomberState { Idle, Chasing, Jumping, Detonating, PlayerControlled }
    private BomberState currentState = BomberState.Idle;

    private Coroutine jumpSequenceCoroutine;
    private bool isPerformingJump = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        rtsUnit = GetComponent<RTSUnit>();
        bomberCollider = GetComponent<Collider>();

        if (agent == null)
        {
            Debug.LogError("SuicideBomberAI requires a NavMeshAgent component.", this);
            enabled = false;
            return;
        }
        if (rtsUnit == null)
        {
            Debug.LogError("SuicideBomberAI requires an RTSUnit component for player control integration.", this);
            enabled = false;
            return;
        }
        if (bomberCollider == null)
        {
            Debug.LogWarning($"SuicideBomberAI on {gameObject.name}: No Collider component found. Cannot reliably exclude self from explosion damage check.", this);
        }

        if (animator == null)
        {
            Debug.LogWarning("SuicideBomberAI could not find an Animator component. Animations will not play.", this);
        }

        if (range1_Detonate >= range2_Jump)
        {
            Debug.LogWarning($"SuicideBomberAI on {gameObject.name}: range1_Detonate ({range1_Detonate}) should be less than range2_Jump ({range2_Jump}). Adjusting.", this);
             range2_Jump = range1_Detonate + 0.5f;
        }
        if (range2_Jump >= range3_Chase)
        {
            Debug.LogWarning($"SuicideBomberAI on {gameObject.name}: range2_Jump ({range2_Jump}) should be less than range3_Chase ({range3_Chase}). Adjusting.", this);
            range3_Chase = range2_Jump + 0.5f;
        }

        SetState(BomberState.Idle);
    }

    void Update()
    {
        // Log timer countdown (useful for seeing if AI update block conditions are met)
        // Debug.Log($"Update: aiUpdateTimer = {aiUpdateTimer:F2}, isPerformingJump = {isPerformingJump}, agent.enabled = {agent.enabled}", this);

        if (!isPerformingJump && currentState != BomberState.PlayerControlled)
        {
             aiUpdateTimer -= Time.deltaTime;
        }

        // --- RTS Player Control Check ---
        if (rtsUnit != null && rtsUnit.IsPlayerControlled)
        {
            if (currentState != BomberState.PlayerControlled)
            {
                Debug.Log($"{gameObject.name}: Transitioning to PlayerControlled state.", this);
                SetState(BomberState.PlayerControlled);
            }
            return; // Exit Update if player controlled
        }
        else if (currentState == BomberState.PlayerControlled)
        {
             // Player control just ended
             Debug.Log($"{gameObject.name}: Player control ended. Resuming AI. Transitioning to Idle.", this);
             SetState(BomberState.Idle);
             aiUpdateTimer = 0f; // Trigger immediate AI check
        }
        // --- End RTS Player Control Check ---


        // --- AI Logic (Only runs if not player controlled) ---
        // This block runs periodically based on aiUpdateInterval
        if (aiUpdateTimer <= 0f && !isPerformingJump && agent.enabled && currentState != BomberState.PlayerControlled)
        {
            aiUpdateTimer = aiUpdateInterval;
            Debug.Log($"--- AI Update Block Triggered for {gameObject.name} --- Current State: {currentState}", this);


            FindTarget();

            if (target == null)
            {
                 Debug.Log($"{gameObject.name}: No target found. Ensuring Idle state.", this);
                 if (currentState != BomberState.Idle) SetState(BomberState.Idle);
                 return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            Debug.Log($"{gameObject.name}: Target found: {target.name}. Distance: {distanceToTarget:F2}. Ranges: Detonate={range1_Detonate}, Jump={range2_Jump}, Chase={range3_Chase}", this);


            // AI State Machine (Transitions)
            switch (currentState)
            {
                case BomberState.Idle:
                     if (distanceToTarget <= range3_Chase)
                     {
                         Debug.Log($"{gameObject.name}: Distance <= Chase Range ({range3_Chase}). Transitioning to Chasing.", this);
                         SetState(BomberState.Chasing);
                     }
                     break;

                case BomberState.Chasing:
                    if (distanceToTarget <= range1_Detonate)
                    {
                        Debug.Log($"{gameObject.name}: Distance <= Detonate Range ({range1_Detonate}). Transitioning to Detonating.", this);
                        SetState(BomberState.Detonating);
                    }
                    else if (distanceToTarget <= range2_Jump && !isPerformingJump)
                    {
                        Debug.Log($"{gameObject.name}: Distance <= Jump Range ({range2_Jump}). Transitioning to Jumping.", this);
                        SetState(BomberState.Jumping);
                    }
                    else if (distanceToTarget > range3_Chase)
                    {
                        Debug.Log($"{gameObject.name}: Distance > Chase Range ({range3_Chase}). Transitioning to Idle.", this);
                        SetState(BomberState.Idle);
                    }
                    break;

                case BomberState.Jumping:
                     // Transitions out of jumping are handled by the coroutine
                    break;

                case BomberState.Detonating:
                     // State handles explosion, no transitions out
                    break;

                 case BomberState.PlayerControlled:
                      // Should not reach here due to check at start of Update
                     break;
            }
        } // End of AI Update Interval block


        // --- Continuous AI Actions (Independent of interval) ---
        // Movement and animation updates that need to run every frame when AI is active
        if (!rtsUnit.IsPlayerControlled)
        {
             if (currentState == BomberState.Chasing && target != null)
             {
                 // NavMeshAgent handles movement, but we set destination and rotate
                 if (agent.enabled && !agent.isStopped)
                 {
                     bool pathSet = agent.SetDestination(target.position);
                     if (!pathSet)
                     {
                         Debug.LogWarning($"Failed to set destination for {gameObject.name} to {target.position}. Agent state: Enabled={agent.enabled}, Stopped={agent.isStopped}, Path Status={agent.pathStatus}", this);
                          // Optional: Try sampling a reachable position near the target
                         NavMeshHit hit;
                         if (NavMesh.SamplePosition(target.position, out hit, agent.height * 2, NavMesh.AllAreas))
                         {
                              Debug.Log($"Attempting to set destination to sampled position near target: {hit.position}. Path status: {agent.pathStatus}", this);
                              agent.SetDestination(hit.position); // Try setting to the sampled position
                         } else {
                              Debug.LogWarning($"Could not sample a valid NavMesh position near the target ({target.position}) for {gameObject.name}.", this);
                         }
                     }
                     // Debug.Log($"{gameObject.name} setting destination to {target.position}. Path status: {agent.pathStatus}", this); // Uncomment for detailed path status logs
                     RotateTowardsTarget(target.position);
                     SetAnimationSpeed(agent.velocity.magnitude);
                 } else {
                     // Debug.Log($"{gameObject.name} in Chasing state, but agent not enabled or stopped. Enabled={agent.enabled}, Stopped={agent.isStopped}", this); // Uncomment to see why agent isn't moving
                 }
             }
             else if (currentState == BomberState.Jumping && isPerformingJump)
             {
                 // Manual movement and rotation are handled by the coroutine
                 RotateTowardsTarget(target.position); // Continue rotating towards target mid-air
             }
              else if (currentState == BomberState.Idle)
              {
                  agent.isStopped = true;
                  SetAnimationSpeed(0f);
              }
              else if (currentState == BomberState.Detonating)
              {
                   agent.isStopped = true;
                   SetAnimationSpeed(0f);
              }
        }
        // Animation speed for PlayerControlled state will be handled by the RTSUnit script.
    }

    void SetState(BomberState newState)
    {
        if (currentState == newState)
        {
             // Debug.Log($"Attempted to set state to {newState}, but already in that state for {gameObject.name}.", this);
             return;
        }

        Debug.Log($"{gameObject.name} changing state from {currentState} to {newState}", this);

        // --- Exit Current State ---
        switch (currentState)
        {
            case BomberState.Chasing:
                 Debug.Log($"Exiting Chasing State for {gameObject.name}", this);
                break;
            case BomberState.Jumping:
                 Debug.Log($"Exiting Jumping State for {gameObject.name}", this);
                isPerformingJump = false;
                if (jumpSequenceCoroutine != null)
                {
                    StopCoroutine(jumpSequenceCoroutine);
                     jumpSequenceCoroutine = null;
                }
                // Agent re-enabled by the coroutine or the new state's entry logic
                break;
            case BomberState.Detonating:
                 Debug.Log($"Exiting Detonating State for {gameObject.name}", this);
                break;
            case BomberState.PlayerControlled:
                 Debug.Log($"Exiting PlayerControlled State for {gameObject.name}", this);
                 if (!agent.enabled) agent.enabled = true;
                 agent.isStopped = true;
                break;
            case BomberState.Idle:
                 Debug.Log($"Exiting Idle State for {gameObject.name}", this);
                 break;
        }

        // --- Enter New State ---
        currentState = newState;
        switch (currentState)
        {
            case BomberState.Idle:
                Debug.Log($"Entering Idle State for {gameObject.name}", this);
                if (agent != null) // Added null check for safety, though RequiredComponent should prevent it
                {
                    agent.enabled = true;
                    agent.isStopped = true;
                    agent.speed = chaseSpeed;
                }
                SetAnimationTrigger(idleAnimationTrigger);
                SetAnimationSpeed(0f);
                target = null;
                break;

            case BomberState.Chasing:
                Debug.Log($"Entering Chasing State for {gameObject.name}", this);
                 if (agent != null)
                 {
                    agent.enabled = true;
                    agent.isStopped = false;
                    agent.speed = chaseSpeed;
                 }
                SetAnimationTrigger(runAnimationTrigger);
                // SetAnimationSpeed is updated every frame in Update()
                break;

            case BomberState.Jumping:
                 Debug.Log($"Entering Jumping State for {gameObject.name}", this);
                if (agent != null && agent.enabled) agent.enabled = false;
                if (agent != null) agent.velocity = Vector3.zero;

                if (jumpSequenceCoroutine != null) StopCoroutine(jumpSequenceCoroutine);
                jumpSequenceCoroutine = StartCoroutine(PerformJumpSequence());
                break;

            case BomberState.Detonating:
                Debug.Log($"Entering Detonating State for {gameObject.name}", this);
                if (agent != null && agent.enabled) agent.enabled = false;
                 if (agent != null) agent.isStopped = true;
                Explode();
                break;

            case BomberState.PlayerControlled:
                 Debug.Log($"Entering PlayerControlled State for {gameObject.name}", this);
                if (agent != null && !agent.enabled) agent.enabled = true;
                // Animation and movement handled by RTSUnit
                break;
        }
    }

    IEnumerator PerformJumpSequence()
    {
        isPerformingJump = true;
        Debug.Log($"{gameObject.name}: Starting Jump Sequence Coroutine.", this);

        SetAnimationTrigger(jumpAnimationTrigger);

        Vector3 startPos = transform.position;
        Vector3 endPos = target != null ? target.position : transform.position;

          NavMeshHit startGroundHit;
          float startGroundY = startPos.y;
          if (agent != null && NavMesh.SamplePosition(startPos + Vector3.down * agent.height * 0.5f, out startGroundHit, agent.height * 2, NavMesh.AllAreas))
          {
              startGroundY = startGroundHit.position.y;
          } else {
               Debug.LogWarning($"{gameObject.name}: Could not find ground near start position ({startPos}) for jump arc calculation. Using start Y.", this);
          }


          NavMeshHit endGroundHit;
          float endGroundY = endPos.y;
           if (agent != null && NavMesh.SamplePosition(endPos + Vector3.down * agent.height * 0.5f, out endGroundHit, agent.height * 2, NavMesh.AllAreas))
           {
                endGroundY = endGroundHit.position.y;
           } else {
                Debug.LogWarning($"{gameObject.name}: Could not find ground near target position ({endPos}) for jump arc calculation. Using target Y.", this);
           }
        Vector3 endPosOnGround = new Vector3(endPos.x, endGroundY, endPos.z);


        float timer = 0f;
        Vector3 initialHorizontalVelocity = Vector3.zero;
        if (jumpDuration > 0)
        {
             initialHorizontalVelocity = (endPosOnGround - startPos) / jumpDuration * jumpBoostSpeedMultiplier;
        } else {
             Debug.LogError($"{gameObject.name}: Jump Duration is zero! Cannot calculate horizontal velocity.", this);
             // Fallback: Exit coroutine if duration is zero
             isPerformingJump = false;
             jumpSequenceCoroutine = null;
             if (agent != null) agent.enabled = true;
             if (target != null) {
                 float distanceToTarget = Vector3.Distance(transform.position, target.position);
                 SetState(distanceToTarget <= range1_Detonate ? BomberState.Detonating : BomberState.Chasing);
             } else {
                 SetState(BomberState.Idle);
             }
             yield break; // Exit the coroutine
        }


        while (timer < jumpDuration)
        {
             float t = timer / jumpDuration;
             float jumpArcT = t * (1 - t) * 4;

             Vector3 currentPosHorizontal = startPos + initialHorizontalVelocity * timer;
             float currentHeight = Mathf.Lerp(startGroundY, endGroundY, t) + jumpHeight * jumpArcT;

             transform.position = new Vector3(currentPosHorizontal.x, currentHeight, currentPosHorizontal.z);

             if (target != null)
             {
                 RotateTowardsTarget(target.position);
             }

             timer += Time.deltaTime;
             yield return null;
        }

        Debug.Log($"{gameObject.name}: Jump Sequence Coroutine finished.", this);

        if (agent != null && !agent.enabled) agent.enabled = true;
        if (agent != null) agent.isStopped = true;

        NavMeshHit hit;
         Vector3 finalManualPosition = transform.position; // Capture position after loop
        if (target != null && NavMesh.SamplePosition(endPosOnGround, out hit, agent.height * 2, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Debug.Log($"{gameObject.name}: Sampled NavMesh near target position {endPosOnGround}. Snapped to {hit.position}", this);
        }
        else if (agent != null && NavMesh.SamplePosition(finalManualPosition, out hit, agent.height * 2, NavMesh.AllAreas))
        {
            transform.position = hit.position;
             Debug.Log($"{gameObject.name}: Sampled NavMesh near final manual position {finalManualPosition}. Snapped to {hit.position}", this);
        }
        else
        {
             Debug.LogWarning($"{gameObject.name}: Landed off NavMesh after jump and couldn't sample a valid position. May be stuck.", this);
        }

        isPerformingJump = false;
        jumpSequenceCoroutine = null;

        // After jump sequence is finished, re-evaluate state based on current distance
        if (target != null)
        {
             float distanceToTarget = Vector3.Distance(transform.position, target.position);
             Debug.Log($"{gameObject.name}: Jump finished. Distance to target ({target.name}): {distanceToTarget:F2}. Re-evaluating state.", this);
             if (distanceToTarget <= range1_Detonate)
             {
                 SetState(BomberState.Detonating);
             }
             else
             {
                 SetState(BomberState.Chasing);
             }
        }
        else
        {
             Debug.Log($"{gameObject.name}: Jump finished, but target was lost. Transitioning to Idle.", this);
             SetState(BomberState.Idle);
        }
    }

    void Explode()
    {
        Debug.Log($"{gameObject.name} DETONATING!", this); // Added explicit log for detonation

        if (agent != null && agent.enabled) agent.isStopped = true;
        if (jumpSequenceCoroutine != null)
        {
            StopCoroutine(jumpSequenceCoroutine);
             jumpSequenceCoroutine = null;
        }
        isPerformingJump = false;
        if (agent != null && agent.enabled) agent.enabled = false;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range1_Detonate, damageableLayers);

        Debug.Log($"{gameObject.name}: Checking for damageable objects within radius {range1_Detonate}. Found {hitColliders.Length} potential hits in OverlapSphere.", this);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider == null)
            {
                 Debug.LogWarning($"{gameObject.name}: OverlapSphere found a null collider.", this);
                continue;
            }
            Debug.Log($"{gameObject.name}: OverlapSphere hit: {hitCollider.gameObject.name}", this);

            if (bomberCollider != null && hitCollider == bomberCollider)
            {
                Debug.Log($"{gameObject.name}: Excluding self collider from damage.", this);
                continue;
            }

            Health hitHealth = hitCollider.GetComponent<Health>();
            if (hitHealth == null)
            {
                hitHealth = hitCollider.GetComponentInParent<Health>();
            }
            if (hitHealth == null)
            {
                hitHealth = hitCollider.GetComponentInChildren<Health>();
            }

            if (hitHealth != null)
            {
                Debug.Log($"{gameObject.name}: Found Health component on {hitCollider.gameObject.name}. Applying {detonationDamage} damage.", this);
                hitHealth.TakeDamage(detonationDamage);
            } else {
                 Debug.Log($"{gameObject.name}: No Health component found on {hitCollider.gameObject.name} or its parent/children.", this);
            }
        }

        if (explosionEffectPrefab != null)
        {
            Debug.Log($"{gameObject.name}: Instantiating explosion effect.", this);
            GameObject explosionEffect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
             ParticleSystem ps = explosionEffect.GetComponent<ParticleSystem>();
             if (ps != null)
             {
                 Destroy(explosionEffect, ps.main.duration);
             }
             else
             {
                 Destroy(explosionEffect, 3f);
             }
        }
        else
        {
             Debug.LogWarning($"{gameObject.name}: Explosion Effect Prefab is not assigned.", this);
        }

        if (explosionSFX != null)
        {
             Debug.Log($"{gameObject.name}: Playing explosion SFX.", this);
             AudioSource.PlayClipAtPoint(explosionSFX, transform.position);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Explosion SFX is not assigned.", this);
        }


        Debug.Log($"{gameObject.name}: Destroying self.", this);
        Destroy(gameObject);
    }

    void FindTarget()
    {
        // Debug.Log($"FindTarget() called for {gameObject.name}. Searching for tags: {string.Join(", ", targetTags)}", this);
        Transform oldTarget = target;
        target = null;
        float nearestDistance = Mathf.Infinity;
        GameObject nearestGameObject = null;
        Vector3 currentPosition = transform.position;

        foreach (string tag in targetTags)
        {
             // Debug.Log($"Searching for objects with tag: {tag}", this);
            GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag(tag);
             // Debug.Log($"Found {potentialTargets.Length} objects with tag {tag}", this);

            foreach (GameObject potentialTarget in potentialTargets)
            {
                if (potentialTarget == null)
                {
                     Debug.LogWarning($"Found a null reference in potentialTargets list for tag {tag}.", this);
                    continue;
                }

                float distanceToPotentialTarget = Vector3.Distance(currentPosition, potentialTarget.transform.position);

                if (distanceToPotentialTarget < nearestDistance)
                {
                    nearestDistance = distanceToPotentialTarget;
                    nearestGameObject = potentialTarget;
                }
            }
        }

        if (nearestGameObject != null)
        {
            target = nearestGameObject.transform;
            // Debug.Log($"{gameObject.name}: Nearest target found: {target.name} (Distance: {nearestDistance:F2})", this);
        }
        else
        {
            target = null;
            // Debug.Log($"{gameObject.name}: No targets found with specified tags.", this);
        }
    }

    void RotateTowardsTarget(Vector3 lookAtPosition)
    {
        Vector3 direction = (lookAtPosition - transform.position).normalized;
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(horizontalDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    void SetAnimationTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
             if (HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
             {
                // Debug.Log($"{gameObject.name}: Setting animation trigger: {triggerName}", this);
                if (triggerName != jumpAnimationTrigger && HasParameter(jumpAnimationTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(jumpAnimationTrigger);
                if (triggerName != runAnimationTrigger && HasParameter(runAnimationTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(runAnimationTrigger);
                if (triggerName != idleAnimationTrigger && HasParameter(idleAnimationTrigger, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(idleAnimationTrigger);

                animator.SetTrigger(triggerName);
             }
             else
             {
                 Debug.LogWarning($"Animator trigger parameter '{triggerName}' not found or is not a Trigger type on {gameObject.name}'s Animator. Check Animator Controller parameters.", this);
             }
        } else if (animator == null) {
             // Debug.LogWarning($"Animator component is null on {gameObject.name}. Cannot set trigger {triggerName}.", this);
        }
    }

    void SetAnimationSpeed(float speed)
    {
        if (animator != null && !string.IsNullOrEmpty(speedAnimationParameter))
        {
             if (HasParameter(speedAnimationParameter, AnimatorControllerParameterType.Float))
             {
                 // Debug.Log($"{gameObject.name}: Setting animation speed float: {speed}", this);
                 animator.SetFloat(speedAnimationParameter, speed);
             }
             else
             {
                 Debug.LogWarning($"Animator float parameter '{speedAnimationParameter}' not found or is not a Float type on {gameObject.name}'s Animator. Check Animator Controller parameters.", this);
             }
        } else if (animator == null) {
             // Debug.LogWarning($"Animator component is null on {gameObject.name}. Cannot set speed {speed}.", this);
        }
    }

    private bool HasParameter(string paramName, AnimatorControllerParameterType type)
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range3_Chase);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, range2_Jump);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range1_Detonate);

        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}