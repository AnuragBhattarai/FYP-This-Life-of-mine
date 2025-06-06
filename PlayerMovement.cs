using UnityEngine;

// Ensure the GameObject has a Rigidbody and Animator component
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))] // Added Animator requirement
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f; // Speed at which the player moves forward
    [SerializeField] private float rotationSpeed = 720f; // Degrees per second the player rotates towards movement direction
    [SerializeField] [Range(0.1f, 10f)] private float minAngleToMove = 1f; // How close (in degrees) the player needs to face the target direction before moving

    [Header("Animation Settings (Triggers)")] // Changed header to indicate triggers
    [SerializeField] private Animator animator; // Reference to the Animator component
    [SerializeField] private string idleTriggerName = "Idle"; // Name of the trigger to activate idle animation
    [SerializeField] private string moveTriggerName = "Move"; // Name of the trigger to activate movement animation

    [Header("References")]
    [SerializeField] private Camera mainCamera; // Reference to the main camera
    private IsometricView isometricView; // Reference to the IsometricView script

    private Rigidbody rb;
    private Vector3 moveInputDirection = Vector3.zero; // Stores the latest movement input direction

    // Public property to check if the player is trying to move (based on input)
    public bool IsAttemptingMove { get; private set; } = false;

    private bool wasAttemptingMove = false; // To track state changes for animation

    void Start()
    {
        // Get the Rigidbody component attached to this GameObject
        rb = GetComponent<Rigidbody>();

        // Get the Animator component attached to this GameObject
        // Automatically get the Animator if not assigned in the Inspector (due to RequireComponent)
        if (animator == null)
        {
             animator = GetComponent<Animator>();
             if (animator == null)
             {
                 Debug.LogError("PlayerMovement: Animator component not found on this GameObject.", this);
                 enabled = false; // Disable script if Animator is missing
                 return;
             }
        }


        // Automatically find the main camera if not assigned in the Inspector
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("PlayerMovement: Main Camera not found. Please assign it in the Inspector.", this);
                enabled = false; // Disable script if camera is missing
                return;
            }
        }

        // Get the IsometricView component from the main camera
        isometricView = mainCamera.GetComponent<IsometricView>();
        if (isometricView == null)
        {
            Debug.LogError("PlayerMovement: IsometricView script not found on the Main Camera. Please ensure it's attached.", this);
            enabled = false; // Disable script if IsometricView is missing
            return;
        }


        // Lock the Rigidbody's rotation on the X and Z axes to prevent tilting
        // Keep Y rotation free for turning.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Initialize animation state
        UpdateAnimationState();
    }

    void Update()
    {
        // --- Input Reading ---
        // Read horizontal (A/D, Left/Right) and vertical (W/S, Up/Down) input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // --- Calculate Target Direction based on Camera Orientation ---
        Vector3 cameraForward;
        Vector3 cameraRight;

        // Check if the camera is in top-down view
        if (isometricView != null && isometricView.IsTopDownView)
        {
            // If in top-down view, use the camera's orientation from before it switched
            cameraForward = isometricView.LastIsometricCameraForward;
            cameraRight = isometricView.LastIsometricCameraRight;
        }
        else
        {
            // If in isometric view, use the camera's current orientation
            cameraForward = mainCamera.transform.forward;
            cameraRight = mainCamera.transform.right;
        }

        // Flatten vectors onto the XZ plane (ignore vertical component)
        cameraForward.y = 0;
        cameraRight.y = 0;

        // Normalize to ensure consistent direction regardless of camera angle
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate the desired movement direction relative to the camera orientation
        moveInputDirection = (cameraForward * vertical) + (cameraRight * horizontal);

        // Check if there is significant movement input
        IsAttemptingMove = moveInputDirection.magnitude > 0.1f; // Use a small threshold to avoid jitter

        // Normalize the final move direction if there is input
        if (IsAttemptingMove)
        {
            moveInputDirection.Normalize();
        }

        // --- Animation State Check (in Update for responsiveness) ---
        UpdateAnimationState();
    }

    void FixedUpdate()
    {
        // --- Movement and Rotation Logic (Physics Step) ---
        if (IsAttemptingMove)
        {
            // --- Rotation ---
            // Calculate the rotation needed to face the move direction (based on camera input)
            Quaternion targetRotation = Quaternion.LookRotation(moveInputDirection, Vector3.up);

            // Calculate the angle difference between current rotation and target rotation
            float angleDifference = Quaternion.Angle(rb.rotation, targetRotation);

            // Smoothly rotate the Rigidbody towards the target direction
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);

            // --- Movement ---
            // Only move if the player is facing close enough to the target direction
            if (angleDifference < minAngleToMove)
            {
                // Apply velocity in the direction the player is currently facing
                // Note: We use transform.forward because the rotation is applied first.
                Vector3 targetVelocity = transform.forward * moveSpeed;
                // Keep existing vertical velocity (e.g., for jumping/gravity), if any.
                targetVelocity.y = rb.linearVelocity.y;
                rb.linearVelocity = targetVelocity;
            }
            else
            {
                // If rotating, don't apply horizontal movement yet, but maintain vertical velocity
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }
        else
        {
            // No movement input, gradually stop horizontal movement
            // Keep vertical velocity for gravity/jumping etc.
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            // No input, so stop angular velocity too
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Helper method to update the animation state using triggers
    private void UpdateAnimationState()
    {
        // Check if the movement state has changed
        if (IsAttemptingMove != wasAttemptingMove)
        {
            if (IsAttemptingMove)
            {
                // Set the move trigger
                if (animator != null && !string.IsNullOrEmpty(moveTriggerName))
                {
                    animator.SetTrigger(moveTriggerName);
                }
            }
            else
            {
                // Set the idle trigger
                 if (animator != null && !string.IsNullOrEmpty(idleTriggerName))
                {
                    animator.SetTrigger(idleTriggerName);
                }
            }
            // Update the tracking variable
            wasAttemptingMove = IsAttemptingMove;
        }
    }
}
