using UnityEngine;

// Attach this script to the Turret GameObject or a dedicated child Fire Point object.
public class TurretFiringSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;           // Reference to the main camera for raycasting
    [SerializeField] private Transform firePoint;          // The exact point where projectiles will spawn
    [SerializeField] private GameObject projectilePrefab; // The prefab for the bullet/shell
    [SerializeField] private GameObject targetIndicatorPrefab; // Prefab for the visual marker on the ground

    [Header("Firing Settings")]
    [SerializeField] private float initialSpeed = 30f; // Muzzle velocity (speed) of the projectile
    [SerializeField] private float fireRate = 2f;      // How many projectiles can be fired per second
    [SerializeField] private LayerMask groundLayerMask; // Set this to the layer(s) the mouse raycast should hit (e.g., "Ground")

    [Header("Targeting")]
    [SerializeField] private float maxRange = 100f; // Maximum distance the targeting raycast checks

    private float nextFireTime = 0f;          // Timestamp for when the turret can fire again
    private GameObject targetIndicatorInstance; // The instantiated ground marker object
    private Vector3 currentTargetPoint;       // Where the mouse is currently pointing on the ground
    private bool isTargetInRange = false;     // Is the current mouse target valid and potentially reachable?

    void Start()
    {
        // --- Initialization ---
        // Automatically find the main camera if not assigned in the Inspector
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("TurretFiringSystem: Main Camera not found. Please assign it or ensure a camera is tagged 'MainCamera'.", this);
                enabled = false; // Disable script if camera is missing
                return;
            }
        }

        // Validate essential references provided in the Inspector
        if (firePoint == null) { Debug.LogError("TurretFiringSystem: Fire Point transform not assigned.", this); enabled = false; return; }
        if (projectilePrefab == null) { Debug.LogError("TurretFiringSystem: Projectile Prefab not assigned.", this); enabled = false; return; }

        // Instantiate the target indicator visual if a prefab was provided
        if (targetIndicatorPrefab != null)
        {
            targetIndicatorInstance = Instantiate(targetIndicatorPrefab);
            targetIndicatorInstance.SetActive(false); // Start hidden until a valid target is found
            // Ensure the indicator doesn't interfere with raycasts itself
            int indicatorLayer = LayerMask.NameToLayer("Ignore Raycast"); // Or another appropriate layer
            if (indicatorLayer != -1) targetIndicatorInstance.layer = indicatorLayer;
        }
    }

    void Update()
    {
        // --- Aiming and Indicator Update ---
        // Continuously update where the player is aiming on the ground
        UpdateTargetPosition();

        // --- Firing Input ---
        // Check for fire input (Left Mouse Button) and if enough time has passed since the last shot
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            // Only allow firing if the current target point is valid and considered in range
            if (isTargetInRange)
            {
                // Set the time for the next allowed shot
                nextFireTime = Time.time + 1f / fireRate;
                // Execute the firing sequence
                Fire();
            }
            // Optional: Add feedback here if the player tries to fire while aiming out of range
        }
    }

    // Updates the position of the target indicator based on mouse position
    void UpdateTargetPosition()
    {
        // Create a ray from the camera through the current mouse cursor position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        isTargetInRange = false; // Reset range status each frame

        // Perform the raycast against the specified ground layers
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRange, groundLayerMask))
        {
            // A valid point on the ground was hit
            currentTargetPoint = hitInfo.point;

            // Basic check: Is the hit point within the theoretical max range from the fire point?
            float distanceToTarget = Vector3.Distance(firePoint.position, currentTargetPoint);
            if (distanceToTarget <= maxRange)
            {
                // Further check: Can we actually calculate a trajectory? (Using the ballistic formula)
                // This implicitly checks if the target is reachable with the given initialSpeed.
                isTargetInRange = CalculateLaunchVelocity(currentTargetPoint, firePoint.position, initialSpeed).HasValue;
            }

            // Update the visual indicator's position and visibility
            if (targetIndicatorInstance != null)
            {
                targetIndicatorInstance.transform.position = currentTargetPoint;
                // Optional: Add a small vertical offset if the indicator's pivot isn't at its base
                // targetIndicatorInstance.transform.position += Vector3.up * 0.01f;
                targetIndicatorInstance.SetActive(isTargetInRange); // Show only if target is valid and in range
            }
        }
        else
        {
            // Raycast didn't hit anything valid within maxRange
            if (targetIndicatorInstance != null)
            {
                targetIndicatorInstance.SetActive(false); // Hide the indicator
            }
        }
    }

    // Handles the projectile instantiation and launch
    void Fire()
    {
        // Calculate the required launch velocity vector to hit the target point, accounting for gravity
        Vector3? launchVelocity = CalculateLaunchVelocity(currentTargetPoint, firePoint.position, initialSpeed);

        // Proceed only if a valid launch velocity could be calculated
        if (launchVelocity.HasValue)
        {
            // Instantiate the projectile prefab at the fire point's position and rotation
            // Use firePoint.rotation if you want the projectile initially oriented like the fire point,
            // otherwise Quaternion.identity is fine as velocity dictates the path.
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

            // Get the Rigidbody component from the instantiated projectile
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Apply the calculated velocity to the projectile's Rigidbody
                rb.linearVelocity = launchVelocity.Value;
            }
            else
            {
                // Warn if the projectile prefab is missing the essential Rigidbody
                Debug.LogWarning("Projectile Prefab is missing a Rigidbody component.", projectilePrefab);
                Destroy(projectile); // Destroy the useless projectile instance
            }
        }
        else
        {
            // This case should ideally be caught by the isTargetInRange check in Update,
            // but log just in case calculation fails unexpectedly.
             Debug.LogWarning("Attempted to fire, but target is out of range or trajectory calculation failed.");
             if (targetIndicatorInstance != null)
             {
                 // Optional: Make the indicator flash red or provide other feedback
             }
        }
    }

    // Calculates the initial velocity vector required to hit a target position from a start position
    // with a given initial speed, accounting for gravity. Returns null if target is unreachable.
    Vector3? CalculateLaunchVelocity(Vector3 targetPos, Vector3 startPos, float speed)
    {
        // --- Ballistic Trajectory Calculation ---
        float gravity = Physics.gravity.magnitude; // Get the magnitude of gravity (should be positive)
        Vector3 deltaPos = targetPos - startPos;   // Vector from start to target

        // Separate horizontal (XZ plane) and vertical (Y) components
        Vector3 deltaXZ = new Vector3(deltaPos.x, 0f, deltaPos.z);
        float horizontalDistance = deltaXZ.magnitude;
        float verticalDistance = deltaPos.y;

        // Calculate the terms needed for the launch angle formula
        float speedSqr = speed * speed;         // v0^2
        float speedQuad = speedSqr * speedSqr;  // v0^4

        // Calculate the discriminant (the part under the square root in the quadratic formula for tan(angle))
        // Formula: v0^4 - g * (g * dh^2 + 2 * dv * v0^2)
        float discriminant = speedQuad - gravity * (gravity * horizontalDistance * horizontalDistance + 2f * verticalDistance * speedSqr);

        // Check if the target is physically reachable with the given speed
        // If discriminant is negative, the square root is imaginary, meaning no real angle exists.
        if (discriminant < 0f)
        {
            return null; // Target is out of range
        }

        // Calculate the tangent of the launch angle (theta)
        // We use the formula: tan(theta) = (v0^2 +/- sqrt(discriminant)) / (g * dh)
        // We typically want the lower trajectory for direct fire, so we use the '-' sign.
        float tanTheta = (speedSqr - Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance);

        // Handle potential division by zero if target is directly above/below (horizontalDistance is near zero)
        if (float.IsNaN(tanTheta) || float.IsInfinity(tanTheta))
        {
             // Check if it's the vertical case or another numerical issue
             if (horizontalDistance < 0.01f)
             {
                 // Very close horizontally - might need specific handling for vertical launch,
                 // but for simplicity, we can just return null (unreachable via standard calc).
                 return null;
             }
             // Otherwise, log an error as something unexpected happened
             Debug.LogError($"NaN/Infinity detected in tanTheta. Speed: {speed}, HorizDist: {horizontalDistance}, VertDist: {verticalDistance}, Discriminant: {discriminant}");
             return null;
        }

        // Calculate the launch angle in radians
        float angle = Mathf.Atan(tanTheta);

        // --- Construct the Velocity Vector ---
        // Get the normalized horizontal direction vector
        Vector3 directionXZ = deltaXZ.normalized;
        // Calculate horizontal velocity component (speed * cos(angle) in the horizontal direction)
        Vector3 velocityXZ = directionXZ * speed * Mathf.Cos(angle);
        // Calculate vertical velocity component (speed * sin(angle))
        float velocityY = speed * Mathf.Sin(angle);

        // Combine horizontal and vertical components into the final launch velocity vector
        return velocityXZ + Vector3.up * velocityY;
    }

    // Cleanup: Hide or destroy the indicator when the script is disabled or the object is destroyed
    void OnDisable()
    {
        if (targetIndicatorInstance != null)
        {
            targetIndicatorInstance.SetActive(false);
        }
    }

    void OnDestroy() {
        if (targetIndicatorInstance != null)
        {
            Destroy(targetIndicatorInstance);
        }
    }
}
