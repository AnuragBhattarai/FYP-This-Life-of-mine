using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class SpawnBuildings : MonoBehaviour
{
    #region Inspector Variables
    [SerializeField] GameObject productionTile;
    [SerializeField] LayerMask terrainLayer;
    [SerializeField] GraphicRaycaster uiRaycaster;
    [SerializeField] GameObject underConstructionGO;
    [SerializeField] BuildProgressSO buildingToPlace;
    #endregion

    #region Instance Objects
    GameObject currentSpawnedBuilding;
    RaycastHit hit;
    List<ProductionTile> activeTiles;
    GameObject activeTilesParent;
    GameObject activeGridParent;
    bool isColliding;
    #endregion

    void Start()
    {
        activeTiles = new List<ProductionTile>();
        if (!productionTile)
            Debug.LogError("Production Tile is NULL");
        if (!uiRaycaster)
            Debug.Log("GraphicRaycaster not found! Will place objects on button click");
    }

    void Update()
    {
        if (currentSpawnedBuilding)
        {
            if (Input.GetMouseButtonDown(0)) // Left-click to place the building
            {
                if (!PlacementHelpers.RaycastFromMouse(out hit, terrainLayer))
                    return;

                currentSpawnedBuilding.transform.position = hit.point;

                if (CanPlaceBuilding())
                    PlaceBuilding();
            }

            if (Input.GetMouseButtonDown(1)) // Right-click to cancel the building
            {
                ResetPlacement(); // Reset everything when right-click is pressed
            }
        }
    }

    void FixedUpdate()
    {
        if (currentSpawnedBuilding)
        {
            if (PlacementHelpers.RaycastFromMouse(out hit, terrainLayer))
            {
                Vector3 newPosition = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                currentSpawnedBuilding.transform.position = newPosition;

                // Move the grid parent to follow the building
                if (activeGridParent != null)
                {
                    activeGridParent.transform.position = newPosition;
                }
            }
        }
    }

    bool CanPlaceBuilding()
    {
        if (PlacementHelpers.IsButtonPressed(uiRaycaster))
            return false;

        if (isColliding)
        {
            Debug.LogWarning("Cannot place building: A production tile is colliding with another object.");
            return false;
        }

        return true;
    }

    void PlaceBuilding()
    {
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager != null)
        {
            float buildingCost = buildingToPlace.currentBuilding.cost;

            if (resourceManager.GetResourceValue() >= buildingCost)
            {
                resourceManager.ModifyResource(-(int)buildingCost);
                Debug.Log($"Resource reduced by {buildingCost}. Remaining resource: {resourceManager.GetResourceValue()}");

                ClearGrid();
                StartCoroutine(BeginBuilding());
            }
            else
            {
                Debug.LogWarning($"Not enough resources to place the building! Required: {buildingCost}, Available: {resourceManager.GetResourceValue()}");
            }
        }
        else
        {
            Debug.LogError("ResourceManager not found in the scene!");
        }
    }

    void ClearGrid()
    {
        Destroy(activeTilesParent);
        activeTiles.RemoveAll(i => i);
        if (activeGridParent != null)
            Destroy(activeGridParent);
    }

    IEnumerator BeginBuilding()
    {
        Vector3 pos = currentSpawnedBuilding.transform.position;
        GameObject instance = currentSpawnedBuilding;
        currentSpawnedBuilding = null;

        RaycastHit hitTerrain;
        if (PlacementHelpers.RaycastFromMouse(out hitTerrain, terrainLayer))
            pos = hitTerrain.point;

        // Instantiate the "under construction" object
        GameObject underConstructionInstance = Instantiate(underConstructionGO, pos, Quaternion.identity);

        // Attach ShowBuildProgress and listen for the build completion event
        ShowBuildProgress progress = underConstructionInstance.GetComponent<ShowBuildProgress>();
        if (progress != null)
        {
            bool buildComplete = false;

            // Subscribe to the OnBuildComplete event
            progress.OnBuildComplete.AddListener(() =>
            {
                buildComplete = true;

                // Enable the actual building's renderers once construction is complete
                PlacementHelpers.ToggleRenderers(instance, true);
                Debug.Log("Building construction completed.");
            });

            // Wait until the buildComplete flag is set to true
            yield return new WaitUntil(() => buildComplete);
        }

        // Destroy the "under construction" object after the build is complete
        Destroy(underConstructionInstance);
    }

    public void SpawnBuilding(BuildingSO building)
    {
        if (currentSpawnedBuilding)
            return;

        currentSpawnedBuilding = Instantiate(building.buildingPrefab);
        buildingToPlace.currentBuilding = building;

        // Disable the building's renderers initially (but keep colliders enabled)
        PlacementHelpers.ToggleRenderers(currentSpawnedBuilding, false);

        Debug.Log($"Spawning building: {building.objectName} with grid size: {building.buildGridSize}");
        GenerateGrid(building.buildGridSize);
    }

    void ToggleColliders(GameObject obj, bool enable)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = enable;
        }
    }

    void GenerateGrid(Vector2Int gridSize)
    {
        ClearGrid();

        activeGridParent = new GameObject("PlacementGrid");
        activeGridParent.transform.position = currentSpawnedBuilding.transform.position;

        float tileSizeX = 1f;
        float tileSizeZ = 1f;
        float yOffset = 0.1f;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 tilePosition = new Vector3(
                    currentSpawnedBuilding.transform.position.x + (x - gridSize.x / 2) * tileSizeX,
                    currentSpawnedBuilding.transform.position.y + yOffset,
                    currentSpawnedBuilding.transform.position.z + (z - gridSize.y / 2) * tileSizeZ
                );

                Quaternion tileRotation = Quaternion.Euler(90, 0, 0);
                GameObject tile = Instantiate(productionTile, tilePosition, tileRotation, activeGridParent.transform);
                ProductionTile productionTileComponent = tile.GetComponent<ProductionTile>();

                productionTileComponent.OnCollisionStateChanged.AddListener(OnTileCollisionStateChanged);
                activeTiles.Add(productionTileComponent);
            }
        }
    }

    void OnTileCollisionStateChanged(bool isTileColliding)
    {
        if (isTileColliding)
        {
            isColliding = true;
        }
        else
        {
            isColliding = activeTiles.Exists(tile => tile.colliding);
        }
    }

    void ResetPlacement()
    {
        // Clear the grid and destroy the current building
        ClearGrid();
        if (currentSpawnedBuilding != null)
        {
            Destroy(currentSpawnedBuilding);
            currentSpawnedBuilding = null;
        }

        // Reset other variables
        isColliding = false;
        Debug.Log("Building placement reset.");
    }
}
