using UnityEngine;

// Attach this script to the Turret base, or a parent object that contains the rotating head and fire points.
// Controls automatic targeting, aiming, and firing with ballistic trajectory calculation for multiple fire points.
public class AutoTurretController : MonoBehaviour
{
    [Header("References")]
    // The part of the turret that rotates to aim (e.g., the turret head).
    // Assign this transform in the Inspector.
    [SerializeField] private Transform turretHead;
    // The exact points where projectiles will spawn. Should be children of the turretHead or the base.
    // Assign these transforms in the Inspector.
    [SerializeField] private Transform[] firePoints; // Changed to an array
    // The prefab for the bullet/shell. Needs a Rigidbody and the TurretProjectile script.
    [SerializeField] private GameObject projectilePrefab;

    [Header("Firing Settings")]
    // Muzzle velocity (speed) of the projectile.
    [SerializeField] private float initialSpeed = 30f;
    // How many projectiles can be fired per second *per fire point*.
    // If you have multiple fire points firing simultaneously or in quick succession, the overall fire rate will be higher.
    [SerializeField] private float fireRate = 2f;
    // Rotation speed in degrees per second.
    [SerializeField] private float rotationSpeed = 90f;
    // Angle tolerance (in degrees) for considering the turret "aimed".
    [SerializeField] private float aimTolerance = 5f;
    // Delay between firing from consecutive fire points (if multiple fire points exist).
    // Set to 0 for simultaneous firing.
    [SerializeField] private float firePointSpreadDelay = 0.1f;


    [Header("Targeting")]
    // The tags the turret will search for as targets.
    [SerializeField] private string[] targetTags = { "Enemy" };
    // Maximum distance to search for and engage targets.
    [SerializeField] private float maxRange = 100f;
    // How often (in seconds) the turret scans for the nearest target.
    [SerializeField] private float targetCheckInterval = 1f;

    [Header("Debug")]
    // Number of points to use for drawing the trajectory gizmo
    [SerializeField] private int trajectoryGizmoPoints = 50;
    // Time step for calculating trajectory points for gizmo
    [SerializeField] private float trajectoryGizmoTimeStep = 0.1f;


    // Timestamp for when the turret can fire again from any point.
    private float nextFireTime = 0f;
    // The currently selected target object.
    private GameObject currentTargetObject;
    // Timestamp of the last target scan.
    private float lastTargetCheckTime = 0f;
    // Index of the next fire point to use for firing.
    private int currentFirePointIndex = 0;


    void Start()
    {
        // --- Initialization ---
        // Validate essential references provided in the Inspector
        if (turretHead == null) { Debug.LogError("AutoTurretController: Turret Head transform not assigned.", this); enabled = false; return; }
        if (firePoints == null || firePoints.Length == 0) { Debug.LogError("AutoTurretController: Fire Points array is not assigned or is empty.", this); enabled = false; return; }
        if (projectilePrefab == null) { Debug.LogError("AutoTurretController: Projectile Prefab not assigned.", this); enabled = false; return; }

        // Validate each fire point in the array
        for (int i = 0; i < firePoints.Length; i++)
        {
            if (firePoints[i] == null)
            {
                Debug.LogError($"AutoTurretController: Fire Point at index {i} is not assigned.", this);
                enabled = false;
                return;
            }
        }


        // Ensure Rigidbody and the projectile script exist on the prefab
        if (projectilePrefab.GetComponent<Rigidbody>() == null)
        {
            Debug.LogError("AutoTurretController: Projectile Prefab is missing a Rigidbody component. Physics-based trajectory won't work.", projectilePrefab);
        }
        if (projectilePrefab.GetComponent<TurretProjectile>() == null) // Check for the new script
        {
            Debug.LogWarning("AutoTurretController: Projectile Prefab is missing the TurretProjectile script.", projectilePrefab);
        }


        // Initial scan for targets immediately
        FindNearestTarget();
        lastTargetCheckTime = Time.time; // Prevent immediate rescan
        nextFireTime = Time.time; // Ready to fire immediately if target is found
    }

    void Update()
    {
        // --- Target Management ---
        // Periodically scan for the nearest target
        // Also scan if the current target becomes null (e.g., destroyed or went out of range)
        // or if the current target object itself is invalid/destroyed.
        if (Time.time >= lastTargetCheckTime + targetCheckInterval || currentTargetObject == null || !IsTargetValid(currentTargetObject))
        {
            FindNearestTarget();
            lastTargetCheckTime = Time.time;
        }

        // --- Aiming and Firing ---
        if (currentTargetObject != null)
        {
            // Aim towards the current target's position
            AimAtTarget(currentTargetObject.transform.position);

            // Check if the turret is aimed and ready to fire
            if (IsAimedAtTarget() && Time.time >= nextFireTime)
            {
                // Check if the target is still within range before firing
                // We can use the first fire point for a range check, or the turret base.
                // Using the first fire point is generally sufficient for range.
                if ((currentTargetObject.transform.position - firePoints[0].position).sqrMagnitude <= maxRange * maxRange)
                {
                    // Check if the target is actually reachable with the ballistic calculation
                    // We need to calculate reachability from the *current* fire point being used.
                    if (CalculateLaunchVelocity(currentTargetObject.transform.position, firePoints[currentFirePointIndex].position, initialSpeed).HasValue)
                    {
                        // Fire the projectile from the current fire point
                        Fire(firePoints[currentFirePointIndex]);

                        // Calculate the time for the next allowed shot, considering fire rate and spread delay
                        nextFireTime = Time.time + 1f / fireRate;

                        // Move to the next fire point for the next shot
                        currentFirePointIndex = (currentFirePointIndex + 1) % firePoints.Length;
                    }
                    // else: Target unreachable by trajectory in free space from this specific fire point, don't fire, keep aiming or wait for rescan
                }
                else
                {
                    // Target went out of max range, clear it so FindNearestTarget runs
                    currentTargetObject = null;
                }
            }
        }
        else
        {
             // If no target, reset fire point index and ensure ready to fire when one appears
             currentFirePointIndex = 0;
             nextFireTime = Time.time;
        }
    }

    // Finds the nearest valid target with any of the specified tags within maxRange.
    void FindNearestTarget()
    {
        GameObject potentialTarget = null;
        float minDistanceSqr = maxRange * maxRange + 1f; // Initialize slightly outside max range squared

        // Iterate through all specified target tags
        foreach (string tag in targetTags)
        {
            // Find all objects with the current tag
            // NOTE: FindGameObjectsWithTag can be slow with many objects.
            // For performance in large scenes, consider a dedicated target manager.
            GameObject[] objectsWithTag = GameObject.FindGameObjectsWithTag(tag);

            // Check each found object
            foreach (GameObject obj in objectsWithTag)
            {
                // Ensure the object is valid and not somehow null or destroyed
                if (!IsTargetValid(obj)) continue; // Use helper function

                // Calculate distance from the turret's base position to the potential target
                // Using the turret's base position for targeting range is usually more consistent
                // than using a specific fire point, especially if fire points are spread out.
                float distanceSqr = (obj.transform.position - transform.position).sqrMagnitude;

                // Check if it's within the maximum range and closer than the current nearest
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    potentialTarget = obj;
                }
            }
        }

        // Update the current target
        if (potentialTarget != currentTargetObject)
        {
            // Optional debug log: Debug.Log($"New target found: {(potentialTarget != null ? potentialTarget.name : "None")}", this);
            currentTargetObject = potentialTarget;
            // If we found a new target, reset fire time and fire point index so it can fire immediately (if aimed)
            // This prevents waiting for the full fireRate delay when a target appears.
            if (currentTargetObject != null)
            {
                nextFireTime = Time.time;
                currentFirePointIndex = 0; // Start firing from the first point
            }
        }

        // If no target is found, ensure nextFireTime is ready for when one appears
        if (currentTargetObject == null)
        {
            nextFireTime = Time.time;
            currentFirePointIndex = 0; // Reset index
        }
    }

    // Checks if a potential target GameObject is still valid and active.
    private bool IsTargetValid(GameObject target)
    {
        // Checks if the GameObject reference is not null and the underlying object hasn't been destroyed.
        return target != null && target.activeInHierarchy;
    }


    // Rotates the turret head to aim at the specified target position.
    void AimAtTarget(Vector3 targetPos)
    {
        // Calculate the direction vector from the turret head to the target
        Vector3 targetDirection = targetPos - turretHead.position;

        // We only want to rotate on the Y (up) axis for horizontal aiming.
        Vector3 directionXZ = new Vector3(targetDirection.x, 0f, targetDirection.z);

        // Avoid looking straight up/down if target is directly above/below (horizontal distance is negligible)
        if (directionXZ.sqrMagnitude < 0.001f)
        {
            // Target is almost directly above/below. Cannot determine horizontal aim.
            // Keep current horizontal rotation.
            return;
        }

        // Calculate the rotation needed to look in the horizontal direction
        Quaternion targetRotation = Quaternion.LookRotation(directionXZ);

        // Smoothly rotate the turret head towards the target rotation
        turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Note: This script doesn't make the turret barrel physically pitch up/down.
        // The vertical launch angle is handled purely by the initial velocity applied to the projectile's Rigidbody.
        // If you need physical pitching, you'd need another rotating transform (the barrel)
        // and calculate the required pitch angle using the ballistic formula results (the angle variable).
    }

    // Checks if the turret head is aimed closely enough at the target's horizontal position.
    bool IsAimedAtTarget()
    {
        if (currentTargetObject == null || !IsTargetValid(currentTargetObject)) return false;

        Vector3 targetDirection = currentTargetObject.transform.position - turretHead.position;
        Vector3 directionXZ = new Vector3(targetDirection.x, 0f, targetDirection.z);

        // Avoid issues if target is directly above/below
        if (directionXZ.sqrMagnitude < 0.001f)
        {
            // If target is directly overhead/underfoot, can't really aim horizontally.
            // For this script, aiming requires a horizontal component.
            return false;
        }

        // Calculate the angle difference between the turret's forward direction (XZ plane projection)
        // and the direction to the target (XZ plane projection).
        // Normalize both vectors before calculating the angle.
        float angleDifference = Vector3.Angle(turretHead.forward, directionXZ); // Vector3.Angle already handles normalization implicitly for directions

        // Check if the angle difference is within the allowed tolerance
        return angleDifference <= aimTolerance;
    }


    // Handles the projectile instantiation and launch from a specific fire point.
    void Fire(Transform firePointToUse)
    {
        // Double check target validity and reachability just before firing
        if (currentTargetObject == null || !IsTargetValid(currentTargetObject) ||
            (currentTargetObject.transform.position - firePointToUse.position).sqrMagnitude > maxRange * maxRange)
        {
            // The target might have moved out of range relative to this specific fire point,
            // or become invalid just now. Don't fire.
            return;
        }

        Vector3 targetPosition = currentTargetObject.transform.position;

        // Calculate the required launch velocity vector to hit the target object's current position
        Vector3? launchVelocity = CalculateLaunchVelocity(targetPosition, firePointToUse.position, initialSpeed);

        if (launchVelocity.HasValue)
        {
            // Instantiate the projectile prefab at the fire point's position and rotation
            // Use the fire point's rotation; it should inherit the turret head's horizontal aim
            // and potentially have a fixed vertical offset if needed (handled by the ballistic velocity).
            GameObject projectile = Instantiate(projectilePrefab, firePointToUse.position, firePointToUse.rotation);

            // Get the Rigidbody component and the new projectile script
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            // TurretProjectile projScript = projectile.GetComponent<TurretProjectile>(); // Reference if needed for setup


            if (rb != null)
            {
                // Apply the calculated velocity
                rb.linearVelocity = launchVelocity.Value;
            }
            else
            {
                Debug.LogWarning("Projectile Prefab instantiated without a Rigidbody component.", projectile);
            }

            // Optional: Pass information to the projectile script if needed, e.g., damage amount
            // if (projScript != null)
            // {
            //     projScript.SetDamage( /* your damage value here */ );
            //     // Note: It's often better to set damage/other properties on the projectile prefab itself
            //     // if they are constant, or pass them via a method if they vary.
            // }

            // Apply the spread delay for the *next* shot from a different fire point.
            // This is handled by the main Update loop controlling `nextFireTime` based on `fireRate`.
            // If you want sequential firing with a delay between points, the `nextFireTime` logic
            // would need to be slightly different, perhaps a coroutine or tracking time per fire point.
            // The current logic fires from one point, waits `1/fireRate` seconds, then fires from the next.
            // If you want a delay *between* points within a single 'volley', you'd need more complex timing.
            // The `firePointSpreadDelay` variable is added but not currently used for sequential firing delay between points in the current structure.
            // It could be used if you change the firing logic to handle bursts or volleys.
        }
        else
        {
            // Target is unreachable by trajectory from this specific fire point.
            // This point won't fire this time, but the turret will cycle to the next point
            // for the next firing opportunity.
            // Debug.LogWarning($"Attempted to fire from {firePointToUse.name} at {currentTargetObject?.name} but trajectory calculation failed.", this);
        }
    }


    // Calculates the initial velocity vector required to hit a target position from a start position
    // with a given initial speed, accounting for gravity. Returns null if target is unreachable.
    // This method is general and can be called for any start position (fire point).
    Vector3? CalculateLaunchVelocity(Vector3 targetPos, Vector3 startPos, float speed)
    {
        // --- Ballistic Trajectory Calculation ---
        // Note: This calculation assumes a constant gravity and no air resistance.
        // It solves for the required launch angle(s) to hit the target point.
        float gravity = Physics.gravity.magnitude; // Get the magnitude of gravity
        Vector3 deltaPos = targetPos - startPos;   // Vector from start to target

        // Separate horizontal (XZ plane) and vertical (Y) components of the displacement
        Vector3 deltaXZ = new Vector3(deltaPos.x, 0f, deltaPos.z);
        float horizontalDistance = deltaXZ.magnitude;
        float verticalDistance = deltaPos.y;

        // Calculate the terms needed for the launch angle formula
        float speedSqr = speed * speed;         // v0^2

        // Formula for the tangent of the launch angle (theta):
        // tan(theta) = (v0^2 +/- sqrt(v0^4 - g * (g*dh^2 + 2*dv*v0^2))) / (g * dh)
        // where v0 is initial speed, g is gravity, dh is horizontal distance, dv is vertical distance.

        // Calculate the term under the square root (the discriminant)
        float term = gravity * horizontalDistance * horizontalDistance + 2f * verticalDistance * speedSqr;
        float discriminant = speedSqr * speedSqr - gravity * term;

        // Check if the target is physically reachable with the given speed
        // If discriminant is negative, the square root is imaginary, meaning no real angle exists.
        if (discriminant < 0f)
        {
            // Target is out of range or unreachable with this speed/gravity configuration.
            return null;
        }

        // Handle potential division by zero if horizontalDistance is near zero
        if (horizontalDistance < 0.001f)
        {
             // If horizontal distance is negligible, it's a near-vertical shot.
             // Need to check if the vertical distance is reachable with the given speed.
             // v_f^2 = v_0^2 + 2*a*dy. At peak height v_f = 0. Max height = v0^2 / (2g).
             // For the target to be reachable vertically, its vertical distance must be <= max height (if positive)
             // AND the speed must be sufficient to reach that height or fall to that depth.
             // We can check if discriminant is zero or positive in the vertical case too:
             // speedSqr * speedSqr - gravity * (0 + 2f * verticalDistance * speedSqr) >= 0
             // speedSqr - 2f * gravity * verticalDistance >= 0
             // speedSqr >= 2f * gravity * verticalDistance
             if (speedSqr < 2f * gravity * verticalDistance)
             {
                 // Target is too high to reach vertically with this speed
                 return null;
             }
             // If reachable vertically, the required vertical speed is sqrt(v0^2 - 2g*dy)
             float requiredVerticalSpeedSqr = speedSqr - 2f * gravity * verticalDistance;
             float verticalSpeed = Mathf.Sqrt(requiredVerticalSpeedSqr);

             // Direction is purely vertical (or mostly vertical if horizontalDistance is very small but non-zero)
             // Using the sign of verticalDistance to determine up/down direction.
             Vector3 verticalDir = verticalDistance > 0 ? Vector3.up : (verticalDistance < 0 ? Vector3.down : Vector3.zero);
             // Add a small horizontal component if horizontalDistance is not exactly zero to avoid NaN issues with normalization
             Vector3 combinedDirection = (verticalDir * verticalSpeed + deltaXZ.normalized * (horizontalDistance * speed / horizontalDistance));
             // A simpler approach might be: return (verticalDir * Mathf.Abs(verticalSpeed)) + (deltaXZ.normalized * (speed * Mathf.Cos(Mathf.Atan2(verticalSpeed, horizontalDistance))));
             // Let's stick to the core formula result interpretation:

             // For the near-vertical case, the required launch velocity is primarily vertical.
             // The tangent formula gave us two possible angles, but for HZ near zero, the angle approaches +/- 90 deg.
             // If the target is below, need negative velocity component. If above, positive.
             // The speed 'speed' is the *magnitude* of the initial velocity.
             // For purely vertical, the time of flight T is solved by: dv = v0*T + 0.5*g*T^2
             // If verticalDistance > 0, the minimum speed to reach is sqrt(2*g*verticalDistance).
             // If verticalDistance < 0, it's always reachable if speed is non-zero, time T is positive root of 0.5*g*T^2 + v0*T - dv = 0.
             // Let's reconsider the discriminant check for the near-vertical case.
             // discriminant = speedSqr * speedSqr - gravity * (0 + 2f * verticalDistance * speedSqr)
             // discriminant = speedSqr * (speedSqr - 2f * gravity * verticalDistance)
             // If this is >= 0, it's reachable.
             // The required vertical speed component is sqrt(speedSqr - 2f * gravity * verticalDistance).
             // The horizontal speed component is negligible.
             // So, the velocity is approximately Vector3.up * sqrt(speedSqr - 2f * gravity * verticalDistance) * sign(verticalDistance).
             // Or, more precisely, using the angle from the tangent formula:
             float angle;
             // If horizontal distance is near zero, the formula for tanThetaLow/High becomes unstable (division by zero).
             // In this vertical-only scenario, we need to determine if the target is reachable vertically.
             // The time of flight T satisfies: verticalDistance = v_y * T + 0.5 * gravity * T^2
             // And speed^2 = v_x^2 + v_y^2. For HZ near zero, v_x is near zero, so speed is roughly |v_y|.
             // verticalDistance = +/- speed * T + 0.5 * gravity * T^2
             // This is a quadratic in T. We need a real positive solution for T.
             // A simpler approach for near-vertical: if discriminant is >= 0, it's reachable.
             // The velocity vector is mostly vertical, with magnitude 'speed'.
             // The vertical component v_y must satisfy: verticalDistance = v_y * T + 0.5 * gravity * T^2 and speed^2 = v_y^2 (approx).
             // Let's use the angle derived from the discriminant if discriminant >= 0.
             // When horizontalDistance is near zero, tan(theta) approaches infinity, meaning theta approaches +/- 90 degrees.
             // The required vertical component is speed * sin(angle).
             // The required horizontal component is speed * cos(angle) * directionXZ.
             // Let's calculate the angle from the discriminant term directly:
             // tan(theta) = Y / X where Y = v0^2 +/- sqrt(...) and X = g*dh
             // If dh is near 0, X is near 0. If discriminant > 0, Y is non-zero. tan(theta) -> infinity. angle -> +/- PI/2.
             // If discriminant = 0, Y = 0. tan(theta) = 0/0 indeterminate. Requires L'Hopital's rule or special handling.
             // When discriminant = 0, the target is at the peak of the trajectory (highest reachable point).
             // speed^2 = 2 * gravity * verticalDistance. The launch is purely vertical.
             // v_y = sqrt(speed^2) = speed (if verticalDistance > 0).
             // Let's rely on the discriminant check. If discriminant >= 0, a real angle exists.
             // Even if horizontalDistance is very small, the formula for tanThetaLow will produce a very large number,
             // and Mathf.Atan will give an angle close to +/- PI/2.
             // Let's use tanThetaLow and handle potential floating point issues.

             // Calculate the tangent of the launch angle (low arc)
             float tanTheta = (speedSqr - Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance); // Use the low arc tangent

             // Handle potential NaN from division by near zero or other floating point issues
             if (float.IsNaN(tanTheta) || float.IsInfinity(tanTheta))
             {
                 // Recalculate angle for the purely vertical case or handle as unreachable if appropriate
                 if (speedSqr >= 2f * gravity * verticalDistance)
                 {
                     // Reachable vertically. The launch angle is +/- 90 degrees.
                     // The velocity is mostly vertical with magnitude 'speed'.
                     // The sign depends on whether the target is above or below.
                     return Vector3.up * Mathf.Sqrt(speedSqr - 2f * gravity * verticalDistance) * Mathf.Sign(verticalDistance);
                 }
                 else
                 {
                      return null; // Still unreachable
                 }
             }

             angle = Mathf.Atan(tanTheta);

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
         else
         {
             // Standard ballistic calculation for non-vertical shots.
             // Calculate the tangent(s) of the launch angle(s)
             // There are usually two possible trajectories (high arc and low arc), corresponding to the +/- in the formula.
             // We typically want the lower trajectory for direct fire, so we use the '-' sign.
             // If you wanted the higher lob trajectory, use the '+'.
             float tanThetaLow = (speedSqr - Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance);
             // float tanThetaHigh = (speedSqr + Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance); // Keep this just in case we want the high arc later

             // Calculate the launch angle in radians for the chosen trajectory (low arc)
             float angle = Mathf.Atan(tanThetaLow);

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
    }

    // Helper function to calculate points along a trajectory for visualization/prediction
    private Vector3 GetPointOnTrajectory(Vector3 startPos, Vector3 initialVelocity, float t, Vector3 gravity)
    {
        // Position at time t = startPos + initialVelocity * t + 0.5 * gravity * t^2
        return startPos + initialVelocity * t + 0.5f * gravity * t * t;
    }

    // Optional: Draw gizmos in the editor to visualize max range and aim tolerance
    void OnDrawGizmosSelected()
    {
        // Draw max range sphere (using the base position for consistency)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxRange);


        // Draw aim tolerance cone (simplified)
        if (turretHead != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 forward = turretHead.forward;

            // Calculate horizontal direction based on turret head forward
            Vector3 forwardXZ = new Vector3(forward.x, 0, forward.z).normalized;

            // Create rotation quaternions for the left and right edges of the cone (horizontal only)
            // Apply rotation to the horizontal forward vector
            Quaternion leftLimit = Quaternion.Euler(0, -aimTolerance, 0);
            Quaternion rightLimit = Quaternion.Euler(0, aimTolerance, 0);

            Vector3 leftDir = leftLimit * forwardXZ * maxRange;
            Vector3 rightDir = rightLimit * forwardXZ * maxRange;


            // Draw lines from the turret head position along these directions
            Gizmos.DrawLine(turretHead.position, turretHead.position + leftDir);
            Gizmos.DrawLine(turretHead.position, turretHead.position + rightDir);
        }

        // If there's a current target and valid fire points, draw trajectories from each fire point
        if (currentTargetObject != null && IsTargetValid(currentTargetObject) && firePoints != null && firePoints.Length > 0)
        {
             Vector3 targetPosition = currentTargetObject.transform.position;
             Gizmos.color = Color.red;
             // Draw line from the first fire point to the target (can pick one arbitrarily or loop)
             if(firePoints[0] != null) Gizmos.DrawLine(firePoints[0].position, targetPosition);


             // Draw the calculated trajectory for *each* fire point if reachable
             Gizmos.color = Color.green;
             Vector3 currentGravity = Physics.gravity; // Get current scene gravity vector

             foreach (Transform firePoint in firePoints)
             {
                 if (firePoint == null) continue; // Skip if a fire point is null

                 Vector3? launchVelocity = CalculateLaunchVelocity(targetPosition, firePoint.position, initialSpeed);

                 if (launchVelocity.HasValue)
                 {
                     Vector3 previousPoint = firePoint.position;

                     for (int i = 1; i <= trajectoryGizmoPoints; i++)
                     {
                         float time = i * trajectoryGizmoTimeStep;
                         Vector3 currentPoint = GetPointOnTrajectory(firePoint.position, launchVelocity.Value, time, currentGravity);

                         // Optional: Stop drawing if the trajectory goes below ground (requires a way to determine ground height)
                         // For a simple flat ground at Y=0, you could add:
                         // if (currentPoint.y < 0 && previousPoint.y >= 0) break; // Stop if it crosses Y=0

                         Gizmos.DrawLine(previousPoint, currentPoint);
                         previousPoint = currentPoint;

                         // Stop drawing if the point goes significantly past the target horizontally
                         // (prevents drawing excessively long trajectories)
                         Vector3 distToTargetXZ = new Vector3(targetPosition.x - previousPoint.x, 0, targetPosition.z - previousPoint.z);
                         Vector3 startToPreviousXZ = new Vector3(previousPoint.x - firePoint.position.x, 0, previousPoint.z - firePoint.position.z);
                         Vector3 startToTargetXZ = new Vector3(targetPosition.x - firePoint.position.x, 0, targetPosition.z - firePoint.position.z);

                         // Simple check: if horizontal distance from start to current point is greater than
                         // horizontal distance from start to target point + some tolerance
                         if (startToPreviousXZ.sqrMagnitude > startToTargetXZ.sqrMagnitude * 1.2f) // Added tolerance
                         {
                             break;
                         }
                     }
                 }
                 // else: Trajectory from this fire point is not reachable, don't draw for this point
             }
         }
    }
}