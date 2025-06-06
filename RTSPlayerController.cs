using UnityEngine;
 using UnityEngine.AI;
 using System.Collections.Generic;
 using System.Linq; // Required for LINQ methods like Count()

 public class RTSPlayerController : MonoBehaviour
 {
     [Header("Indicators")]
     [Tooltip("Prefab for the visual indicator shown when a unit is selected.")]
     public GameObject selectionIndicatorPrefab;
     [Tooltip("Multiplier applied to the calculated size of the selection indicator.")]
     [Range(0.1f, 5f)] // Add a range slider in the inspector
     public float selectionIndicatorSizeMultiplier = 1.0f;
      [Tooltip("Default size for the selection indicator if the unit has no collider.")]
      public float defaultSelectionIndicatorSize = 1.0f; // Fallback size
      private Vector3 selectionIndicatorOriginalLocalScale; // To store the prefab's inherent local scale

     [Tooltip("Prefab for the visual indicator shown at the destination of a move command.")]
     public GameObject destinationIndicatorPrefab;
     [Tooltip("Multiplier applied to the calculated base size of the destination indicator.")]
     [Range(0.1f, 5f)] // Add a range slider in the inspector
     public float destinationIndicatorSizeMultiplier = 1.0f;


     [Header("Layers")]
     [Tooltip("The layer(s) containing selectable units.")]
     public LayerMask unitLayer;
     [Tooltip("The layer(s) considered ground for movement commands.")]
     public LayerMask groundLayer;

     [Header("Movement Formation")]
     [Tooltip("The base spacing between units when calculating formation positions. Unit's NavMeshAgent stoppingDistance is added to this.")]
     [Range(0.5f, 5f)]
     public float baseFormationSpacing = 1.5f; // Adjust based on unit size

     // Changed from single RTSUnit to a List for multiple selection
     private List<RTSUnit> selectedUnits = new List<RTSUnit>();
     // We will now manage multiple selection indicator instances
     private List<GameObject> selectionIndicatorInstances = new List<GameObject>();
     private GameObject destinationIndicatorInstance;

     // We store the last move destination to keep the indicator there
     private Vector3 lastMoveDestination;

     // Store the last calculated formation grid dimensions to size the destination indicator
     private int lastUnitsPerRow = 1; // Default to 1 for a single unit
     private int lastNumRows = 1;   // Default to 1 for a single unit


     void Awake()
     {
         // Instantiate a temporary selection indicator to get its original scale and then destroy it
         if (selectionIndicatorPrefab != null)
         {
             GameObject tempIndicator = Instantiate(selectionIndicatorPrefab);
             selectionIndicatorOriginalLocalScale = tempIndicator.transform.localScale;
             Destroy(tempIndicator);
         }
         else
         {
             Debug.LogWarning("Selection Indicator Prefab is not assigned in RTSPlayerController. Unit selection visuals will not be shown.", this);
             selectionIndicatorOriginalLocalScale = Vector3.one; // Default if prefab is missing
         }


         // Instantiate the destination indicator but keep it inactive initially
         if (destinationIndicatorPrefab != null)
         {
             destinationIndicatorInstance = Instantiate(destinationIndicatorPrefab);
             destinationIndicatorInstance.SetActive(false);
             // Initial scale is set in UpdateIndicators based on formation
         }
         else
         {
             Debug.LogWarning("Destination Indicator Prefab is not assigned in RTSPlayerController. Destination visuals will not be shown.", this);
         }
     }

     void Update()
     {
         HandleInput();
         UpdateIndicators();
     }

     void HandleInput()
     {
         // Left Click: Select unit(s)
         if (Input.GetMouseButtonDown(0)) // 0 is left mouse button
         {
             Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
             RaycastHit hit;

             // Raycast specifically against the unit layer(s)
             if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayer))
             {
                 // Hit something on the unit layer, try to get the RTSUnit component
                 RTSUnit hitUnit = hit.collider.GetComponent<RTSUnit>();

                 if (hitUnit != null)
                 {
                     bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                     if (shiftHeld)
                     {
                         // Shift is held: Toggle selection for this unit
                         if (selectedUnits.Contains(hitUnit))
                         {
                             // Unit is already selected, deselect it
                             DeselectUnit(hitUnit);
                         }
                         else
                         {
                             // Unit is not selected, select it
                             SelectUnit(hitUnit);
                         }
                     }
                     else
                     {
                         // Shift is NOT held: Deselect all others and select only this unit
                         DeselectAllUnits();
                         SelectUnit(hitUnit);
                     }
                 }
             }
             else
             {
                 // Did not hit a unit on the unit layer
                 bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                 if (!shiftHeld)
                 {
                     // If shift is not held and we click off a unit, deselect all
                     DeselectAllUnits();
                 }
                 // If shift is held and we click off a unit, do nothing (maintain current selection)
             }
         }

         // Right Click: Give move command to selected units
         if (Input.GetMouseButtonDown(1)) // 1 is right mouse button
         {
             if (selectedUnits.Count > 0) // Only proceed if at least one unit is selected
             {
                 Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                 RaycastHit hit;

                 // Raycast specifically against the ground layer(s)
                 // NOTE: To handle targeting specific objects (like enemies) for the indicator size,
                 // you would add additional Raycast checks here (e.g., against an enemy layer).
                 // For this modification, we only handle ground clicks and size based on formation.
                 if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                 {
                     // Hit the ground - calculate formation destinations
                     lastMoveDestination = hit.point; // Store the clicked point for the main indicator

                     CalculateAndAssignFormation(hit.point);

                     // Destination indicator will be updated in UpdateIndicators
                 }
                 // Optional: Add logic here to check for hitting other targetable layers (e.g., enemies)
                 // If a target object is hit, you would store a reference to it
                 // and potentially pass an "Attack" command to the units instead of "Move".
                 // The indicator logic would then need to check if there's a target object
                 // and size/position the indicator differently (e.g., centered on the target,
                 // size based on target bounds).
             }
         }
     }

     // Calculates positions for each selected unit in a simple grid formation around the target point
     void CalculateAndAssignFormation(Vector3 centerPoint)
     {
         int numUnits = selectedUnits.Count;
         if (numUnits == 0)
         {
             lastUnitsPerRow = 1; // Reset for indicator if no units
             lastNumRows = 1;
             return;
         }

         // For a single unit, just send it to the clicked point
         if (numUnits == 1)
         {
             selectedUnits[0].OnMoveCommand(centerPoint);
             lastUnitsPerRow = 1; // Set for indicator
             lastNumRows = 1;
             return;
         }

         // Calculate approximate grid dimensions
         int unitsPerRow = Mathf.CeilToInt(Mathf.Sqrt(numUnits));
         int numRows = Mathf.CeilToInt((float)numUnits / unitsPerRow);

         // Store these for the destination indicator size calculation later
         lastUnitsPerRow = unitsPerRow;
         lastNumRows = numRows;

         // Calculate the starting offset to center the grid around the clicked point
         float startOffsetX = -(unitsPerRow - 1) * baseFormationSpacing / 2f;
         float startOffsetZ = -(numRows - 1) * baseFormationSpacing / 2f;

         for (int i = 0; i < numUnits; i++)
         {
             RTSUnit currentUnit = selectedUnits[i];
             if (currentUnit == null) continue; // Skip null units

             // Get the NavMeshAgent for this specific unit
             NavMeshAgent unitAgent = currentUnit.GetComponent<NavMeshAgent>();
             if (unitAgent == null)
             {
                 Debug.LogWarning($"Unit {currentUnit.gameObject.name} is missing a NavMeshAgent component. Skipping formation calculation for this unit.", currentUnit.gameObject);
                 continue; // Skip this unit if it doesn't have an agent
             }

             int row = i / unitsPerRow;
             int col = i % unitsPerRow;

             // Calculate the base offset for this position in the grid
             float offsetX = startOffsetX + col * baseFormationSpacing;
             float offsetZ = startOffsetZ + row * baseFormationSpacing;

             // Create the base target position for this unit without stopping distance factored in yet
             Vector3 baseUnitTargetPosition = centerPoint + new Vector3(offsetX, 0, offsetZ);

             // Adjust the target position based on the unit's stopping distance
             // Move the target point slightly away from the formation center by the stopping distance
             Vector3 directionFromCenter = (baseUnitTargetPosition - centerPoint).normalized;
             // Handle the case where the base position is exactly the center (only happens for 1 unit, but good to be safe)
              if (directionFromCenter == Vector3.zero)
              {
                  // Default to forward if the position is exactly at the center point
                  directionFromCenter = Vector3.forward;
              }

             Vector3 finalUnitTargetPosition = baseUnitTargetPosition + directionFromCenter * unitAgent.stoppingDistance;


             // Sample the NavMesh to ensure the calculated position is valid
             // Use a sampling radius that accounts for the base spacing and stopping distance
             NavMeshHit navMeshHit;
             // Increased sample distance slightly for robustness
             if (NavMesh.SamplePosition(finalUnitTargetPosition, out navMeshHit, baseFormationSpacing + unitAgent.stoppingDistance + 1f, NavMesh.AllAreas))
             {
                 // Found a valid position on the NavMesh near the calculated spot
                 currentUnit.OnMoveCommand(navMeshHit.position);
             }
             else
             {
                 // Couldn't find a valid NavMesh position nearby, send to the main clicked point as a fallback
                 Debug.LogWarning($"Could not sample NavMesh for formation position for unit {currentUnit.gameObject.name}. Sending to center point.", currentUnit.gameObject);
                 currentUnit.OnMoveCommand(centerPoint); // Fallback destination
             }
         }
     }


     // Helper method to select a single unit
     void SelectUnit(RTSUnit unit)
     {
         if (unit != null && !selectedUnits.Contains(unit))
         {
             selectedUnits.Add(unit);
             unit.OnSelected(); // Notify the unit it's selected
             InstantiateSelectionIndicator(unit); // Create and attach indicator
         }
     }

     // Helper method to deselect a single unit
     void DeselectUnit(RTSUnit unit)
     {
         if (unit != null && selectedUnits.Contains(unit))
         {
             selectedUnits.Remove(unit);
             unit.OnDeselected(); // Notify the unit it's deselected
             DestroySelectionIndicator(unit); // Destroy the indicator for this unit
         }
     }

     // Helper method to deselect all units
     void DeselectAllUnits()
     {
         // Iterate through a copy of the list because DeselectUnit modifies the list
         foreach (RTSUnit unit in new List<RTSUnit>(selectedUnits))
         {
             DeselectUnit(unit);
         }
         selectedUnits.Clear(); // Ensure the list is empty
         // All indicators are destroyed by DestroySelectionIndicator

         // Also hide the destination indicator when deselecting all
         if (destinationIndicatorInstance != null)
         {
             destinationIndicatorInstance.SetActive(false);
         }
     }

     // Helper to instantiate and attach a selection indicator to a unit
     void InstantiateSelectionIndicator(RTSUnit unit)
     {
         // Only instantiate if the prefab is assigned
         if (selectionIndicatorPrefab == null || unit == null)
         {
             if (selectionIndicatorPrefab == null) Debug.LogWarning("Selection Indicator Prefab is not assigned.", this);
             return;
         }


         GameObject indicator = Instantiate(selectionIndicatorPrefab);
         // Parent the indicator to the unit's transform
         indicator.transform.SetParent(unit.transform);
         // Position it at the unit's pivot (local position 0,0,0)
         indicator.transform.localPosition = Vector3.zero;
         // Keep its rotation aligned with the parent (or set to identity)
         indicator.transform.localRotation = Quaternion.identity;


         // --- Calculate size based on unit's collider bounds ---
         Collider unitCollider = unit.GetComponentInChildren<Collider>(); // Use GetComponentsInChildren to find collider on unit or its children
         float unitHorizontalSize = defaultSelectionIndicatorSize; // Default size if no collider

         if (unitCollider != null)
         {
             Bounds bounds = unitCollider.bounds;
             // Calculate the largest horizontal dimension of the collider's world bounds
             // This accounts for the unit's rotation and scale
             unitHorizontalSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 2f; // Use extents * 2 for total size
         }

         // Get the original scale of the prefab instance (stored in Awake)
         // selectionIndicatorOriginalLocalScale

         // Calculate the scale factor needed relative to the prefab's base horizontal scale
         // Assuming the prefab's base size in X/Z is represented by its original localScale.x/z
         float baseHorizontalScale = Mathf.Max(selectionIndicatorOriginalLocalScale.x, selectionIndicatorOriginalLocalScale.z);
         if (baseHorizontalScale == 0) baseHorizontalScale = 1.0f; // Avoid division by zero if prefab scale is zero

         // The factor needed is (desired_size) / (prefab_base_size_per_unit_scale)
         float scaleFactor = (unitHorizontalSize * selectionIndicatorSizeMultiplier) / baseHorizontalScale;

         // Calculate the final local scale, locking the Y axis
         Vector3 finalLocalScale = new Vector3(
             selectionIndicatorOriginalLocalScale.x * scaleFactor, // Scale X by the calculated factor
             selectionIndicatorOriginalLocalScale.y,             // Keep Y original
             selectionIndicatorOriginalLocalScale.z * scaleFactor  // Scale Z by the calculated factor
         );

         // Apply the final local scale
         indicator.transform.localScale = finalLocalScale;

         // Ensure the indicator is active
         indicator.SetActive(true);

         // Add to our list of active indicator instances
         selectionIndicatorInstances.Add(indicator);
     }

     // Helper to destroy the selection indicator associated with a unit
     void DestroySelectionIndicator(RTSUnit unit)
     {
         if (unit == null) return; // Cannot destroy indicator for null unit

         GameObject indicatorToRemove = null;
         // Find the indicator parented to this unit
         // Iterate through the list of indicator instances
         foreach (GameObject indicator in selectionIndicatorInstances)
         {
             // Check if the indicator exists and its parent is the unit's transform
             // This is a reliable way to find the correct indicator for the unit
             if (indicator != null && indicator.transform.parent == unit.transform)
             {
                 indicatorToRemove = indicator;
                 break; // Found the one to remove
             }
         }

         if (indicatorToRemove != null)
         {
             selectionIndicatorInstances.Remove(indicatorToRemove);
             Destroy(indicatorToRemove);
         }
     }


     void UpdateIndicators()
     {
         // Check if any selected unit is currently executing a player-controlled move.
         // This determines if the destination indicator should be shown.
         bool anySelectedUnitPlayerControlled = false;
         foreach(RTSUnit unit in selectedUnits)
         {
             // Use the IsSelectedByPlayer property and the IsPlayerControlled state from RTSUnit
             if(unit != null && unit.IsSelectedByPlayer && unit.IsPlayerControlled)
             {
                 anySelectedUnitPlayerControlled = true;
                 break; // Found at least one, no need to check further
             }
         }

         if (destinationIndicatorInstance != null)
         {
             // Show destination indicator only if at least one unit is selected AND is currently moving under player control.
             // It hides when units are deselected or when they finish their player move.
             if (selectedUnits.Count > 0 && anySelectedUnitPlayerControlled)
             {
                 destinationIndicatorInstance.SetActive(true);
                 destinationIndicatorInstance.transform.position = lastMoveDestination; // Position at the command location (the center of the formation)

                 // Calculate base size based on the last commanded formation dimensions
                 // Use the maximum dimension of the grid for a reasonable indicator size base
                 // This is for the DESTINATION indicator
                 float baseSize = Mathf.Max(lastUnitsPerRow, lastNumRows) * baseFormationSpacing * 0.5f; // 0.5f to make it roughly fit the *area*
                 if (selectedUnits.Count == 1) // Special case for single unit - use a fixed small size based on spacing
                 {
                      baseSize = baseFormationSpacing * 0.5f; // Or unit's radius * 2, etc.
                 }

                 // Apply the multiplier and set the scale
                 destinationIndicatorInstance.transform.localScale = Vector3.one * baseSize * destinationIndicatorSizeMultiplier;

                 // Optional: Make the indicator face the camera or stand up straight
                 // destinationIndicatorInstance.transform.rotation = Quaternion.identity; // For a non-rotating indicator


             }
             else
             {
                 // If no units selected, or selected units are not player-controlled movement, hide the indicator.
                 destinationIndicatorInstance.SetActive(false);
             }
         }

         // Selection indicators follow their units automatically because they are parented and scaled in InstantiateSelectionIndicator.
     }

     // Add this method to allow other scripts (e.g., a camera controller) to access selected units
     public List<RTSUnit> GetSelectedUnits()
     {
         return selectedUnits;
     }
 }