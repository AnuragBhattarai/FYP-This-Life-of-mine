// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;

// public class Mortar : MonoBehaviour
// {
//     [SerializeField]
//     private GameObject projectilePrefab;       // Prefab of the projectile
//     [SerializeField]
//     private Transform firePoint;             // Launch position
//     [SerializeField]
//     private float rotationSpeed = 5f;          // Rotation speed
//     [SerializeField]
//     private float predictionTime = 2f;        // Time to predict
//     [SerializeField]
//     private float firingRate = 2f;            // Fire rate
//     [SerializeField]
//     private float blastRadius = 5f;
//     [SerializeField]
//     private int damageAmount = 100;
//     [SerializeField]
//     private string targetTag = "Enemy";
//     [SerializeField]
//     private float gravity = 9.81f;           // Downward acceleration
//     [SerializeField]
//     private int maxIterations = 10;        // Maximum iterations to find correct velocity
//     [SerializeField]
//     private float convergenceThreshold = 0.1f; // How close to the target we need to be
//     [SerializeField]
//     private LineRenderer arcLineRenderer;      // Optional:  Line Renderer to show the arc
//     [SerializeField]
//     private int arcPoints = 10;               // Number of points on the arc

//     private GameObject target;               // Target
//     private bool canFire = true;
//     private Coroutine fireCoroutine;

//     void Start()
//     {
//         target = GameObject.FindGameObjectWithTag(targetTag);
//         if (target == null)
//         {
//             Debug.LogError("Mortar: Target with tag " + targetTag + " not found!");
//         }
//         if (arcLineRenderer != null)
//         {
//             arcLineRenderer.positionCount = arcPoints;
//         }
//     }

//     void Update()
//     {
//         if (target != null)
//         {
//             Vector3 predictedPosition = PredictTargetPosition(target, predictionTime);
//             CalculateAndFire(predictedPosition);
//         }
//     }

//     void CalculateAndFire(Vector3 targetPosition)
//     {
//         Vector3 direction = targetPosition - transform.position;
//         direction.y = 0f;
//         Quaternion targetRotation = Quaternion.LookRotation(direction);
//         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

//         Vector3 initialVelocity = CalculateInitialVelocity(targetPosition);
//         if (initialVelocity != Vector3.zero)
//         {
//             if (canFire && fireCoroutine == null)
//             {
//                 fireCoroutine = StartCoroutine(Fire(initialVelocity));
//             }
//         }
//         else
//         {
//             if (arcLineRenderer != null)
//             {
//                 arcLineRenderer.enabled = false;
//             }
//         }
//     }

//     IEnumerator Fire(Vector3 initialVelocity)
//     {
//         canFire = false;

//         // Create the projectile
//         GameObject projectile = Instantiate(projectilePrefab, firePoint.position, transform.rotation);
//         Rigidbody rb = projectile.GetComponent<Rigidbody>();
//         if (rb != null)
//         {
//             rb.linearVelocity = initialVelocity;
//         }
//         else
//         {
//             Debug.LogError("Projectile prefab is missing a Rigidbody component!");
//         }
//         //find Explosion script and set values
//         Explosion explosion = projectile.GetComponent<Explosion>();
//         if (explosion != null)
//         {
//             explosion.blastRadius = blastRadius;
//             explosion.damageAmount = damageAmount;
//             explosion.targetTags = targetTag;
//         }

//         yield return new WaitForSeconds(firingRate);
//         canFire = true;
//         fireCoroutine = null;
//     }

//     Vector3 PredictTargetPosition(GameObject target, float time)
//     {
//         Vector3 targetPosition = target.transform.position;
//         Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();
//         Vector3 targetVelocity = Vector3.zero;

//         if (targetRigidbody != null)
//         {
//             targetVelocity = targetRigidbody.linearVelocity;
//         }

//         return targetPosition + targetVelocity * time;
//     }

//     Vector3 CalculateInitialVelocity(Vector3 targetPosition)
//     {
//         Vector3 displacement = targetPosition - firePoint.position;
//         float horizontalDistance = Vector3.Distance(new Vector3(firePoint.position.x, 0, firePoint.position.z), new Vector3(targetPosition.x, 0, targetPosition.z));
//         float verticalOffset = targetPosition.y - firePoint.position.y;

//         float timeToTarget = Mathf.Sqrt((2 * (displacement.y + Mathf.Abs(verticalOffset))) / gravity);
//         if (float.IsNaN(timeToTarget) || timeToTarget <= 0)
//         {
//             timeToTarget = Mathf.Sqrt((2 * horizontalDistance) / gravity);
//         }

//         Vector3 initialVelocity = Vector3.zero;
//         initialVelocity.y = (displacement.y / timeToTarget) + (0.5f * gravity * timeToTarget);
//         Vector3 horizontalVelocity = new Vector3(displacement.x, 0f, displacement.z) / timeToTarget;
//         initialVelocity += horizontalVelocity;
//         return initialVelocity;
//     }

//     void DrawTrajectory(Vector3 initialVelocity)
//     {
//         if (arcLineRenderer == null) return;

//         arcLineRenderer.enabled = true;
//         Vector3 currentPosition = firePoint.position;
//         float timeStep = 1f / arcPoints;
//         for (int i = 0; i < arcPoints; i++)
//         {
//             float time = i * timeStep;
//             Vector3 position = CalculatePositionAtTime(currentPosition, initialVelocity, time);
//             arcLineRenderer.SetPosition(i, position);
//         }
//     }

//     Vector3 CalculatePositionAtTime(Vector3 startPosition, Vector3 initialVelocity, float time)
//     {
//         Vector3 position = startPosition;
//         position += initialVelocity * time;
//         position.y -= 0.5f * gravity * time * time;
//         return position;
//     }

//     private void OnDrawGizmos()
//     {
//         if (target != null)
//         {
//             Vector3 predictedPosition = PredictTargetPosition(target, predictionTime);
//             Gizmos.color = Color.red;
//             Gizmos.DrawSphere(predictedPosition, 0.5f);
//             Gizmos.color = Color.yellow;
//             Gizmos.DrawLine(firePoint.position, predictedPosition);
//         }
//     }
// }
