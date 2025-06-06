using UnityEngine;
using System.Collections.Generic; // Needed for List
using System.Linq; // Needed for Linq queries (like OrderBy, FirstOrDefault)

// This StateMachineBehaviour handles the rapid firing of a Gatling gun
// from multiple distinct firing points (specified by child names),
// targeting objects with specific tags. The main object will rotate
// towards the target before firing, and bullet drop is simulated via Rigidbody.
public class GatlingGunSMB_New : StateMachineBehaviour
{
    [Header("Firing Points")]
    [Tooltip("Enter the names of the child GameObjects that represent the firing points.")]
    public string[] firingPointNames;

    private List<Transform> actualFiringPoints = new List<Transform>(); // List to store the found Transforms

    [Header("Bullet Settings")]
    [Tooltip("Assign the bullet GameObject prefab.")]
    public GameObject bulletPrefab;
    [Tooltip("The initial speed of the bullet.")]
    public float bulletSpeed = 50f;
    // bulletDropFactor is less relevant here as Rigidbody handles gravity,
    // but kept for potential manual simulation if needed.
    // [Tooltip("Multiplier for gravity effect on the bullet. 0 means no drop.")]
    // public float bulletDropFactor = 9.81f; // Standard gravity value

    [Header("Firing Rate")]
    [Tooltip("Bullets fired per second.")]
    public float fireRate = 20f;

    [Header("Targeting")]
    [Tooltip("Only fire at GameObjects with one of these tags.")]
    public string[] targetTags;
    [Tooltip("Maximum range to search for targets.")]
    public float targetRange = 100f; // Max distance to find a target
    [Tooltip("Vertical offset to add to the target's position for aiming (targets the top part).")]
    public float targetVerticalOffset = 1.0f; // Default offset, adjust in Inspector

    [Header("Ballistic Settings")]
    [Tooltip("Adjusts the aiming angle for bullet drop compensation. Higher values aim higher.")]
    public float trajectoryHeightMultiplier = 1.0f; // Multiplier for the calculated upward angle

    [Header("Rotation Settings")]
    [Tooltip("Speed at which the main object rotates towards the target (degrees per second).")]
    public float rotationSpeed = 90f; // Degrees per second
    [Tooltip("The angle threshold (in degrees) within which the object must be facing the target to fire.")]
    public float rotationThreshold = 2f; // Degrees

    private float timeSinceLastShot = 0f;
    private int currentFiringPointIndex = 0; // Index to cycle through firing points
    private Transform currentTarget = null; // Store the current target

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Reset time since last shot when entering the state
        timeSinceLastShot = 0f;
        // Reset firing point index
        currentFiringPointIndex = 0;
        // Clear the list of actual firing points and find them by name
        FindFiringPointsByName(animator.gameObject);
        // Find an initial target
        currentTarget = FindClosestTarget(animator.gameObject.transform.position);
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Ensure we have firing points before attempting to fire or rotate
        if (actualFiringPoints == null || actualFiringPoints.Count == 0)
        {
            // Debug.LogWarning("GatlingGunSMB_New: No valid firing points found by name.", animator.gameObject);
            return; // Cannot fire if no firing points are found
        }

        // --- Target Acquisition (Re-evaluate periodically or based on events) ---
        // For simplicity, we'll re-find the closest target every update.
        // In a real game, you might do this less often or when the current target is lost.
        currentTarget = FindClosestTarget(animator.gameObject.transform.position);


        // --- Rotation Logic ---
        if (currentTarget != null)
        {
            // Calculate the direction to the target (ignoring vertical difference for horizontal rotation)
            // Use the adjusted target position for rotation as well for consistency
            Vector3 adjustedTargetPosForRotation = currentTarget.position + Vector3.up * targetVerticalOffset;
            Vector3 directionToTarget = adjustedTargetPosForRotation - animator.gameObject.transform.position;
            directionToTarget.y = 0; // Ignore vertical difference for horizontal rotation

            if (directionToTarget.sqrMagnitude > 0.001f) // Avoid looking at zero vector
            {
                // Calculate the desired rotation
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                // Smoothly rotate towards the target rotation
                animator.gameObject.transform.rotation = Quaternion.RotateTowards(
                    animator.gameObject.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        // --- Firing Logic ---
        // Only fire if a target exists AND the object is aimed correctly AND enough time has passed
        if (currentTarget != null && IsAimedAtTarget(animator.gameObject.transform, currentTarget.position + Vector3.up * targetVerticalOffset) && timeSinceLastShot >= 1f / fireRate)
        {
            FireBullet(animator);
            timeSinceLastShot = 0f; // Reset timer
        }
        else
        {
            // If not firing, still increment timeSinceLastShot
            timeSinceLastShot += Time.deltaTime;
        }
    }

    // Method to find the firing point Transforms based on their names
    void FindFiringPointsByName(GameObject parentObject)
    {
        actualFiringPoints.Clear(); // Clear previous entries

        if (firingPointNames == null || firingPointNames.Length == 0)
        {
            Debug.LogWarning("GatlingGunSMB_New: No firing point names specified!", parentObject);
            return;
        }

        Transform parentTransform = parentObject.transform;

        foreach (string pointName in firingPointNames)
        {
            if (string.IsNullOrEmpty(pointName))
            {
                Debug.LogWarning("GatlingGunSMB_New: An empty firing point name was specified.", parentObject);
                continue; // Skip empty names
            }

            // Find the child Transform by name
            Transform foundPoint = parentTransform.Find(pointName);

            if (foundPoint != null)
            {
                actualFiringPoints.Add(foundPoint);
            }
            else
            {
                Debug.LogWarning($"GatlingGunSMB_New: Child GameObject with name '{pointName}' not found under '{parentObject.name}'.", parentObject);
            }
        }

        if (actualFiringPoints.Count == 0)
        {
             Debug.LogError("GatlingGunSMB_New: No valid firing points were found based on the provided names. Firing will not occur.", parentObject);
        }
    }


    // Method to handle firing a single bullet
    // Now accepts the Animator component as a parameter
    void FireBullet(Animator animator)
    {
        // Ensure we have valid firing points before firing
        if (actualFiringPoints == null || actualFiringPoints.Count == 0)
        {
             Debug.LogWarning("GatlingGunSMB_New: Attempted to fire but no actual firing points were found.", animator.gameObject);
            return;
        }

        // Get the current firing point using the index, cycling through the list
        Transform currentFiringPoint = actualFiringPoints[currentFiringPointIndex];

        if (currentFiringPoint == null)
        {
            Debug.LogWarning($"GatlingGunSMB_New: Firing point at index {currentFiringPointIndex} is null in the actualFiringPoints list!", animator.gameObject);
            // Move to the next firing point even if the current one is null
            currentFiringPointIndex = (currentFiringPointIndex + 1) % actualFiringPoints.Count;
            return;
        }

        // --- Ballistic Trajectory Calculation (Aims from the current firing point towards the adjusted target position) ---
        // This calculation assumes the main object is already roughly facing the target due to rotation logic.
        Vector3 startPos = currentFiringPoint.position;
        // Adjust the target position by adding the vertical offset
        Vector3 targetPos = currentTarget.position + Vector3.up * targetVerticalOffset;

        // Calculate horizontal distance
        Vector3 horizontalTargetPos = new Vector3(targetPos.x, startPos.y, targetPos.z);
        float horizontalDistance = Vector3.Distance(startPos, horizontalTargetPos);

        // Calculate vertical distance
        float verticalDistance = targetPos.y - startPos.y;

        // Calculate the time it takes to reach the target horizontally
        // Avoid division by zero if bulletSpeed is 0
        float timeToTarget = (bulletSpeed > 0) ? horizontalDistance / bulletSpeed : 0;

        // Calculate the required initial vertical velocity to hit the target
        // Formula: verticalDistance = initialVerticalVelocity * time + 0.5 * gravity * time^2
        // Rearranging for initialVerticalVelocity:
        // initialVerticalVelocity = (verticalDistance - 0.5f * Physics.gravity.y * Mathf.Pow(timeToTarget, 2)) / timeToTarget;
        // Avoid division by zero if timeToTarget is 0
        float initialVerticalVelocity = (timeToTarget > 0) ? (verticalDistance - 0.5f * Physics.gravity.y * Mathf.Pow(timeToTarget, 2)) / timeToTarget : 0;

        // Construct the fire direction vector
        // The horizontal direction is towards the target's horizontal position
        Vector3 horizontalDirection = (horizontalTargetPos - startPos).normalized;

        // Apply the trajectory height multiplier to the vertical component calculation
        // This allows for fine-tuning the aiming angle
        // The final fireDirection vector magnitude will be adjusted by bulletSpeed later
        Vector3 fireDirection = (horizontalDirection * bulletSpeed + Vector3.up * (initialVerticalVelocity * trajectoryHeightMultiplier));


        // Instantiate the bullet at the current firing point's position and rotation
        // Set rotation to look towards the calculated fireDirection
        GameObject bulletGO = Instantiate(bulletPrefab, currentFiringPoint.position, Quaternion.LookRotation(fireDirection));

        // Get the Rigidbody component of the bullet (assuming the bullet prefab has one)
        Rigidbody bulletRigidbody = bulletGO.GetComponent<Rigidbody>();

        if (bulletRigidbody != null)
        {
            // Apply initial velocity in the calculated fire direction
            // Note: The fireDirection vector calculated above already includes the speed and angle for trajectory
            bulletRigidbody.linearVelocity = fireDirection.normalized * bulletSpeed; // Ensure velocity magnitude is exactly bulletSpeed

            // Unity's physics engine will handle gravity automatically if the Rigidbody
            // has 'Use Gravity' enabled and a non-zero mass on the bullet prefab.
        }
        else
        {
            Debug.LogWarning("GatlingGunSMB_New: Bullet prefab does not have a Rigidbody component. Bullet drop simulation may not work as expected.", bulletGO);
            // If no Rigidbody, you'd need a custom script on the bullet to move it and simulate gravity.
        }

        // Move to the next firing point for the next shot, cycling through the list
        currentFiringPointIndex = (currentFiringPointIndex + 1) % actualFiringPoints.Count;
    }

     // Checks if the main object is aimed at the target within the rotation threshold
    bool IsAimedAtTarget(Transform mainObjectTransform, Vector3 targetPosition)
    {
        // targetPosition here is already the adjusted position with vertical offset
        if (targetPosition == null) return false; // Cannot be aimed if no target

        Vector3 directionToTarget = targetPosition - mainObjectTransform.position;
        directionToTarget.y = 0; // Ignore vertical difference for horizontal check

        if (directionToTarget.sqrMagnitude < 0.001f) return true; // If very close, consider it aimed

        // Calculate the angle between the object's forward direction and the direction to the target
        float angle = Vector3.Angle(mainObjectTransform.forward, directionToTarget);

        // Return true if the angle is within the specified threshold
        return angle <= rotationThreshold;
    }


    // Finds the closest GameObject with one of the specified target tags within range
    Transform FindClosestTarget(Vector3 currentPosition)
    {
        // Handle null or empty targetTags array gracefully
        if (targetTags == null || targetTags.Length == 0 || targetTags.All(string.IsNullOrEmpty))
        {
            return null; // No target tags specified, cannot find a target
        }

        List<GameObject> allTargetsList = new List<GameObject>();

        foreach (string tag in targetTags)
        {
            if (!string.IsNullOrEmpty(tag)) // Avoid searching for empty tags
            {
                 GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                 if (taggedObjects != null)
                 {
                    allTargetsList.AddRange(taggedObjects);
                 }
            }
        }

        // If no objects with specified tags are found, return null
        if (allTargetsList.Count == 0)
        {
            return null;
        }


        // Filter targets by range and find the closest one
        GameObject closestTarget = null;
        float closestDistanceSq = targetRange * targetRange; // Use squared distance for performance

        foreach (GameObject target in allTargetsList)
        {
            // Check if the target is active in the hierarchy and not null
            if (target != null && target.activeInHierarchy)
            {
                Vector3 directionToTarget = target.transform.position - currentPosition;
                float distanceSq = directionToTarget.sqrMagnitude;

                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestTarget = target;
                }
            }
        }

        return closestTarget?.transform; // Return the transform of the closest target, or null if none found
    }


    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    // override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    //    
    // }

    // OnStateMove is called right after Animator.OnAnimatorMove()
    // override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    //    // Implement code that processes and affects root motion
    // }

    // OnStateIK is called right after Animator.OnAnimatorIK()
    // override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    // {
    //    // Implement code that sets up animation IK (inverse kinematics)
    // }
}
