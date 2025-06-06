using UnityEngine;
using System.Linq; // Still needed for the HasParameter helper
using System.Collections.Generic; // Still needed for List

// GatlingBehaviour is required for AI, but we don't RequireComponent it here
// because it might be on a parent/child or we want to be slightly more flexible.
// We'll just get a reference to it in Awake().
public class RTSUnit : MonoBehaviour
{
    [Header("Movement Settings (Player Control)")]
    [Tooltip("The speed the unit will move at under player control.")]
    public float playerMoveSpeed = 3.5f; // Default speed when player controlled
    [Tooltip("How fast the unit rotates towards its destination.")]
    public float rotationSpeed = 10.0f; // Speed for rotation

    [Header("Animation Settings (Player Control)")]
    [Tooltip("Animator trigger name for when the unit is moving under player control.")]
    public string playerMoveTrigger = "Move"; // Example trigger name
    [Tooltip("Animator trigger name for when the unit is idle under player control.")]
    public string playerIdleTrigger = "Idle"; // Example trigger name
    [Tooltip("Animator float parameter name for the unit's movement speed.")]
    public string speedParameterName = "Speed"; // Example float parameter name

    [Header("Animator")]
    [Tooltip("Optional: Assign an external Animator if it's on a separate object. If null, uses the Animator on this GameObject.")]
    public Animator externalAnimator;
    [Tooltip("Multiplier to apply to the unit's speed before setting the speed parameter on the Animator.")]
    public float speedAnimationMultiplier = 1.0f;

    // References to components
    private GatlingBehaviour gatlingAI;
    private Animator animator;
    private Collider unitCollider;

    // Store AI's original speed to restore it (if GatlingBehaviour sets a speed)
    private float aiOriginalSpeed; // This will only be meaningful if GatlingBehaviour uses a concept of 'speed' we can read/set.

    // State variables
    private bool isPlayerControlled = false;
    private string currentPlayerAnimTrigger = ""; // To track the current player animation state
    private Vector3 currentDestination; // Manually track player-set destination
    private bool isRotating = false; // New: To manage rotation state
    private float stopDistance = 0.1f; // New: Small tolerance for arrival

    // Public property for isPlayerControlled
    public bool IsPlayerControlled { get { return isPlayerControlled; } }

    // Public property to check if the unit is currently selected by the player controller
    private bool isSelectedByPlayer = false;
    public bool IsSelectedByPlayer { get { return isSelectedByPlayer; } }


    void Awake()
    {
        animator = externalAnimator != null ? externalAnimator : GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("RTSUnit could not find an Animator component. Player movement animations will not play.", this);
        }

        gatlingAI = GetComponent<GatlingBehaviour>();
        if (gatlingAI == null)
        {
            Debug.LogWarning("RTSUnit could not find a GatlingBehaviour component. Unit will only respond to player commands and will not move autonomously.", this);
        }
        else
        {
            // If GatlingBehaviour has a public 'moveSpeed' property, store it:
            // aiOriginalSpeed = gatlingAI.moveSpeed; // Example if GatlingBehaviour exposes its speed
            gatlingAI.enabled = true; // Enabled GatlingBehaviour
        }

        unitCollider = GetComponent<Collider>();
        if (unitCollider == null)
        {
            Debug.LogWarning($"Unit {gameObject.name} is missing a Collider component. Selection might not work correctly.", this);
        }

        // Start with AI enabled by default (if it exists).
        // If no AI, the unit starts idle and its animations are set to idle.
        if (gatlingAI == null && animator != null)
        {
            SetPlayerAnimationTrigger(playerIdleTrigger);
            SetAnimationSpeed(0f);
        }
    }

    void Update()
    {
        if (isPlayerControlled)
        {
            float distanceToDestination = Vector3.Distance(transform.position, currentDestination);

            // --- Rotation Logic ---
            // Create a target rotation that only considers Y-axis
            Vector3 targetDirection = (currentDestination - transform.position).normalized;
            targetDirection.y = 0; // Ignore Y-axis for rotation to prevent leaning
            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                // Check if we are "close enough" to the target rotation to start moving
                // This prevents the unit from moving while still facing the wrong way.
                if (Quaternion.Angle(transform.rotation, targetRotation) < 5f) // Small angle tolerance
                {
                    isRotating = false;
                }
                else
                {
                    isRotating = true;
                }
            }
            else
            {
                isRotating = false; // No direction to face, effectively not rotating
            }


            // --- Movement Logic ---
            if (!isRotating && distanceToDestination > stopDistance)
            {
                // Move towards the destination if rotation is complete and not at destination
                transform.position = Vector3.MoveTowards(transform.position, currentDestination, playerMoveSpeed * Time.deltaTime);
                SetPlayerAnimationTrigger(playerMoveTrigger);
                SetAnimationSpeed(playerMoveSpeed * speedAnimationMultiplier);
            }
            else if (distanceToDestination <= stopDistance)
            {
                // Unit has arrived or is very close to destination
                OnArrival();
            }
            else // Still rotating or very close to destination but not yet arrived
            {
                SetPlayerAnimationTrigger(playerIdleTrigger);
                SetAnimationSpeed(0f);
            }
        }
        // If not playerControlled, GatlingBehaviour is responsible for movement and animations.
        // We do not manage animations here if AI is active.
    }

    // Called by the RTSPlayerController when the unit is selected
    public void OnSelected()
    {
        isSelectedByPlayer = true;
    }

    // Called by the RTSPlayerController when the unit is deselected
    public void OnDeselected()
    {
        isSelectedByPlayer = false;
        // Unit continues its current command (player move or AI)
    }

    // Called by the RTSPlayerController when a move command is given
    public void OnMoveCommand(Vector3 destination)
    {
        isPlayerControlled = true;
        currentDestination = destination;
        isRotating = true; // Indicate that rotation needs to happen first

        if (gatlingAI != null)
        {
            gatlingAI.enabled = false; // Disable AI behavior
        }

        // Immediately set idle/0 speed to allow rotation animation to play if applicable,
        // or just to hold position until movement starts.
        SetPlayerAnimationTrigger(playerIdleTrigger);
        SetAnimationSpeed(0f);
    }

    // Handles what happens when the unit truly arrives at the destination of a player command
    private void OnArrival()
    {
        CompletePlayerMoveAndRestoreAI();
    }

    // Helper method to complete the player move and return control to AI
    private void CompletePlayerMoveAndRestoreAI()
    {
        if (!isPlayerControlled) return;

        isPlayerControlled = false;
        isRotating = false; // Reset rotation state

        if (gatlingAI != null)
        {
            gatlingAI.enabled = true; // Enable GatlingBehaviour
            // If GatlingBehaviour exposes a 'moveSpeed' property, set it back here:
            // gatlingAI.moveSpeed = aiOriginalSpeed;
        }
        else if (animator != null)
        {
            // If no AI behavior, set the player idle animation and zero speed
            SetPlayerAnimationTrigger(playerIdleTrigger);
            SetAnimationSpeed(0f);
        }
    }

    // Helper to set Animator Triggers safely for player control.
    // Only runs if isPlayerControlled is true and if the trigger name actually exists.
    void SetPlayerAnimationTrigger(string triggerName)
    {
        // Only manage animations if we are player controlled AND have an animator
        if (!isPlayerControlled || animator == null || string.IsNullOrEmpty(triggerName)) return;

        if (currentPlayerAnimTrigger != triggerName)
        {
            if (HasParameter(triggerName, animator, AnimatorControllerParameterType.Trigger))
            {
                // Reset the previous player trigger if it exists and is valid
                if (!string.IsNullOrEmpty(currentPlayerAnimTrigger) && HasParameter(currentPlayerAnimTrigger, animator, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(currentPlayerAnimTrigger);
                }
                animator.SetTrigger(triggerName);
                currentPlayerAnimTrigger = triggerName;
            }
            else
            {
                // Consider adding a simple `Debug.LogWarning` here if you suspect parameter names are incorrect.
                // However, avoid spamming if this method is called frequently.
                // Debug.LogWarning($"Animator trigger parameter '{triggerName}' not found or is not a Trigger type on {gameObject.name}'s Animator.", this);
            }
        }
    }

    // Helper to set the Animator Speed float parameter safely.
    // Only runs if isPlayerControlled is true and if the parameter name actually exists.
    void SetAnimationSpeed(float speed)
    {
        if (!isPlayerControlled || animator == null || string.IsNullOrEmpty(speedParameterName)) return;

        if (HasParameter(speedParameterName, animator, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(speedParameterName, speed);
        }
        // Similar to SetPlayerAnimationTrigger, consider adding a warning here if needed.
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

    // Optional: Draws debug gizmos
    void OnDrawGizmosSelected()
    {
        if (isPlayerControlled)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawSphere(currentDestination, 0.2f);
        }
    }
}