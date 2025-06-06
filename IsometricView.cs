using UnityEngine;

public class IsometricView : MonoBehaviour
{
    [Header("Target Following")]
    [SerializeField] private float followSpeed = 5f; // Speed at which the camera follows the target
    [SerializeField] private float pitchHeight = 1.5f; // Height offset for the camera's look-at position
    private Transform target; // The target object to follow
    private Vector3 targetLookAtPosition; // The position the camera should look at (target + height offset)
    private Vector3 lastTargetPosition; // Store the target's position from the previous frame

    [Header("Distance and Zoom")]
    [SerializeField] private float distance = 10f; // Default distance from the target
    [SerializeField] private float minDistance = 5f; // Minimum zoom distance
    [SerializeField] private float maxDistanceIsometric = 20f; // Maximum zoom distance in Isometric view
    [SerializeField] private float maxDistanceTopDown = 30f; // Maximum zoom distance in Top-Down view
    [SerializeField] private float zoomSpeed = 100f; // Speed of zooming with the scroll wheel

    [Header("Orbiting")]
    [SerializeField] private float orbitSpeed = 100f; // Speed of orbiting when middle mouse is pressed (Isometric view)
    private float currentAngle = 45f; // Current horizontal angle of the camera (default isometric angle)
    private float currentPitch = 30f; // Current vertical pitch of the camera (default isometric pitch)
    private bool isOrbiting = false; // Whether the camera is currently orbiting

    [Header("Top-Down Toggle")]
    [SerializeField] private float topDownPitch = 90f; // Pitch for the top-down view
    [SerializeField] private float topDownDistance = 15f; // Default distance for the top-down view
    [SerializeField] private float toggleSpeed = 3f; // Speed of transitioning between isometric and top-down
    [SerializeField] private float panSpeed = 0.5f; // Speed of panning in top-down mode with middle mouse
    private bool isTopDownView = false; // Whether the camera is currently in top-down view
    private Vector3 topDownTargetPosition; // Position to center the top-down view on

    // Variables to store camera orientation when entering top-down mode (for player movement reference)
    private Vector3 lastIsometricCameraForward;
    private Vector3 lastIsometricCameraRight;

    // Public properties to expose state and orientation (for PlayerMovement script)
    public bool IsTopDownView => isTopDownView;
    public Vector3 LastIsometricCameraForward => lastIsometricCameraForward;
    public Vector3 LastIsometricCameraRight => lastIsometricCameraRight;


    void Start()
    {
        // Find the object tagged "playerMainUnit"
        GameObject player = GameObject.FindGameObjectWithTag("playerMainUnit");
        if (player != null)
        {
            target = player.transform;
            lastTargetPosition = target.position; // Initialize last target position
        }
        else
        {
            Debug.LogError("IsometricView: No object with tag 'playerMainUnit' found in the scene!");
            enabled = false; // Disable the script if no target is found
            return;
        }

        // Initialize the target look-at position
        targetLookAtPosition = target.position + Vector3.up * pitchHeight;

        // Ensure initial angle and pitch are set
        currentAngle = 45f;
        currentPitch = 30f;

        // Store initial camera orientation
        lastIsometricCameraForward = transform.forward;
        lastIsometricCameraRight = transform.right;

        // Set initial distance
        distance = maxDistanceIsometric; // Start at max zoom out in isometric view
    }

    void Update()
    {
        // Handle the 'M' key toggle for top-down view
        if (Input.GetKeyDown(KeyCode.M))
        {
            isTopDownView = !isTopDownView;
            if (isTopDownView)
            {
                // Store the current target position when entering top-down
                topDownTargetPosition = target.position;
                // Store the camera's current orientation when entering top-down
                lastIsometricCameraForward = transform.forward;
                lastIsometricCameraRight = transform.right;
                // Set distance to top-down default when switching
                distance = topDownDistance;
                Debug.Log("IsometricView: Switched to Top-Down View. Stored camera orientation.");
            }
            else
            {
                Debug.Log("IsometricView: Switched back to Isometric View.");
                // Set distance back to isometric default when switching
                distance = maxDistanceIsometric;
            }
        }

        // Handle Zoom using the mouse scroll wheel
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            // Adjust the distance based on scroll input and zoom speed
            distance -= scrollInput * zoomSpeed * Time.deltaTime;

            // Clamp the distance based on the current view mode
            if (isTopDownView)
            {
                distance = Mathf.Clamp(distance, minDistance, maxDistanceTopDown);
            }
            else
            {
                distance = Mathf.Clamp(distance, minDistance, maxDistanceIsometric);
            }
        }

        // Handle Panning in Top-Down mode with Middle Mouse Button
        if (isTopDownView && Input.GetMouseButton(2)) // Middle mouse button held down
        {
            // Get mouse movement deltas
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // Get the horizontal components of the last isometric camera orientation
            Vector3 horizontalCameraForward = lastIsometricCameraForward;
            horizontalCameraForward.y = 0; // Flatten to the XZ plane
            horizontalCameraForward.Normalize(); // Normalize after flattening

            Vector3 horizontalCameraRight = lastIsometricCameraRight;
            horizontalCameraRight.y = 0; // Flatten to the XZ plane
            horizontalCameraRight.Normalize(); // Normalize after flattening

            // Calculate panning direction relative to the horizontal components
            // Moving mouse right means panning camera left (negative right vector)
            // Moving mouse up means panning camera down (negative forward vector)
            Vector3 panDirection = (-horizontalCameraRight * mouseX) + (-horizontalCameraForward * mouseY);

            // Apply panning movement to the top-down target position
            // We use Time.deltaTime to make it frame rate independent
            topDownTargetPosition += panDirection * panSpeed * distance * Time.deltaTime; // Multiply by distance for consistent speed at different zoom levels
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return; // Do nothing if there's no target
        }

        // Smoothly update the target look-at position (only in Isometric view)
        // This helps reduce jitter if the target's movement is not perfectly smooth
        if (!isTopDownView)
        {
             targetLookAtPosition = Vector3.Lerp(targetLookAtPosition, target.position + Vector3.up * pitchHeight, followSpeed * Time.deltaTime);
        }


        // Calculate the desired camera rotation based on current angle and pitch
        Quaternion desiredRotation;
        Vector3 desiredPosition;

        if (isTopDownView)
        {
            // In top-down view, the camera looks straight down
            desiredRotation = Quaternion.Euler(topDownPitch, 0, 0); // Pitch 90, no horizontal rotation
            // Position is relative to the stored top-down target position
            desiredPosition = topDownTargetPosition + desiredRotation * new Vector3(0, 0, -distance); // Use the current distance for positioning

            // Smoothly move the camera towards the top-down position
            transform.position = Vector3.Lerp(transform.position, desiredPosition, toggleSpeed * Time.deltaTime);
            // Smoothly rotate the camera towards the top-down rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, toggleSpeed * Time.deltaTime);

            // In top-down view, the camera looks straight down, so LookAt is not needed
            // transform.LookAt(topDownTargetPosition); // Optional: if you want it to look at the panned target position
        }
        else // Isometric View
        {
            // Check if the middle mouse button is pressed for orbiting
            if (Input.GetMouseButton(2)) // Middle mouse button
            {
                isOrbiting = true;

                // Get mouse movement
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                // Adjust the angle and pitch based on mouse movement
                currentAngle += mouseX * orbitSpeed * Time.deltaTime;
                currentPitch -= mouseY * orbitSpeed * Time.deltaTime;

                // Clamp the pitch to prevent flipping
                currentPitch = Mathf.Clamp(currentPitch, 10f, 80f); // Keep reasonable pitch range
            }
            else
            {
                isOrbiting = false;
            }

            // Calculate the offset based on the current angle and pitch and current distance
            Vector3 offset = Quaternion.Euler(currentPitch, currentAngle, 0) * new Vector3(0, 0, -distance);

            // Desired position of the camera relative to the target's position
            desiredPosition = target.position + offset;

            // Smoothly move the camera to the desired position
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            // Ensure the camera always looks at the smoothed target look-at position
            transform.LookAt(targetLookAtPosition);
        }

        // Store the current target position for the next frame's smoothing (only needed for isometric following)
        if (!isTopDownView)
        {
            lastTargetPosition = target.position;
        }
    }
}
