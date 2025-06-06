using UnityEngine;

// This script should be attached to the Turret GameObject,
// which should be a CHILD of the main player body GameObject.
public class PlayerLookAtMouse : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float rotationSpeed = 15f; // Speed of rotation towards the mouse
    [SerializeField] private LayerMask groundLayerMask; // Set this in the inspector to the layer(s) representing your ground/floor
    [SerializeField] private bool rotateOnlyOnYAxis = true; // Keep turret level with the ground?

    [Header("References")]
    [SerializeField] private Camera mainCamera; // Reference to the main camera (can often be found automatically)

    // No Rigidbody needed here, we rotate the Transform directly

    void Start()
    {
        // Auto-find camera if needed
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("PlayerLookAtMouse (Turret): Main Camera not found. Please assign it or ensure a camera is tagged 'MainCamera'.", this);
                enabled = false; // Disable script if camera is missing
                return;
            }
        }
    }

    void Update() // Can use Update as we are not dealing with Rigidbody physics here
    {
        // Only rotate towards mouse if the middle mouse button is NOT held down
        if (!Input.GetMouseButton(2)) // Middle mouse button check
        {
            RotateTowardsMouse();
        }
        // If middle mouse is down, the turret simply stops rotating towards the mouse.
    }

    private void RotateTowardsMouse()
    {
        // Create a ray from the camera through the mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Perform a raycast against the specified ground layer(s)
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 200f, groundLayerMask)) // Increased ray distance just in case
        {
            // Calculate the direction from the *turret's* position to the point where the ray hit the ground
            Vector3 lookDirection = hitInfo.point - transform.position;

            // Optional: Flatten the direction onto the horizontal plane (ignore Y difference)
            // This keeps the turret aiming level, rather than tilting up/down
            if (rotateOnlyOnYAxis)
            {
                lookDirection.y = 0;
            }

            // Ensure the direction is not zero to avoid issues with LookRotation
            if (lookDirection.sqrMagnitude > 0.01f) // Use sqrMagnitude for efficiency
            {
                // Calculate the target rotation based on the look direction
                // Use Quaternion.LookRotation to get the world-space rotation needed
                Quaternion targetWorldRotation = Quaternion.LookRotation(lookDirection);

                // Smoothly rotate the turret's transform towards the target rotation
                // Use Slerp for smooth rotational interpolation
                // We apply this directly to transform.rotation
                transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRotation, rotationSpeed * Time.deltaTime);
            }
        }
        // Optional: If the raycast doesn't hit the ground, the turret maintains its last rotation.
    }
}
