using UnityEngine;
using TMPro;
using System.Collections.Generic; // Required for using List

public class Spawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [Tooltip("List of enemy prefabs to spawn")]
    public List<GameObject> enemyPrefabs; // Changed to a List of GameObjects

    [Tooltip("Initial time interval between spawns (in seconds)")]
    public float initialSpawnInterval = 120f; // 2 minutes

    [Tooltip("Spawn location")]
    public Transform spawnLocation;

    [Header("Wave Settings")]
    [Tooltip("Number of waves")]
    public int totalWaves = 3;

    [Tooltip("Base number of prefabs to spawn per wave")]
    public int baseSpawnCount = 10;

    [Tooltip("Reference to the DifficultySet script")]
    public DifficultySet difficultySet;

    [Tooltip("Maximum random offset for spawn positions")]
    public float spawnRadius = 5f; // Radius around the spawn location for spreading out objects

    [Tooltip("Amount of resources to add at the start of each wave")]
    public int resourcesPerWave = 200; // New field for resource gain

    [Header("UI Settings")]
    [Tooltip("TextMeshProUGUI element to display time until the next wave")]
    public TextMeshProUGUI waveCountdownText;

    [Header("Resource Settings")] // New Header for ResourceManager reference
    [Tooltip("Reference to the ResourceManager script")]
    public ResourceManager resourceManager; // New field to link to the ResourceManager

    private float currentSpawnInterval;
    private int currentWave = 0;
    private float timer;

    void Start()
    {
        currentSpawnInterval = initialSpawnInterval; // Set the initial spawn interval
        UpdateWaveCountdownText(); // Initialize the countdown text

        // Basic check to ensure ResourceManager is assigned
        if (resourceManager == null)
        {
            Debug.LogError("ResourceManager is not assigned to the Spawner!");
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Update the countdown timer text
        UpdateWaveCountdownText();

        // Check if it's time for the next wave and if there are more waves
        if (timer >= currentSpawnInterval && currentWave < totalWaves)
        {
            // --- Wave Start Logic ---

            // Add resources when the wave starts, only if ResourceManager is assigned
            if (resourceManager != null)
            {
                AddResources(resourcesPerWave); // Call the resource addition method
            }
            else
            {
                 Debug.LogWarning("ResourceManager is not assigned, skipping resource addition.");
            }


            SpawnWave(); // Spawn the enemies for the new wave
            timer = 0f; // Reset the timer
            currentWave++; // Increment the wave count

            // Increase spawn interval and base spawn count for the next wave
            // You might want to adjust these values or make them configurable
            currentSpawnInterval += 2f; // Increase spawn interval by 2 seconds
            baseSpawnCount += 10; // Increase base spawn count by 10

            // --- End Wave Start Logic ---
        }
    }

    private void SpawnWave()
    {
        // Check if the list of prefabs is assigned and not empty
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("EnemyPrefabs list is not assigned or is empty! Cannot spawn wave.");
            return;
        }

        if (difficultySet == null)
        {
            Debug.LogWarning("DifficultySet is not assigned! Cannot spawn wave.");
            return;
        }

        if (spawnLocation == null)
        {
            Debug.LogError("Spawn location is not assigned! Cannot spawn wave.");
            return;
        }

        // Calculate the number of prefabs to spawn based on difficulty
        int spawnCount = baseSpawnCount * difficultySet.modifiedValue;

        Debug.Log($"Spawning {spawnCount} units (mixed types) for wave {currentWave + 1} with a spawn interval of {currentSpawnInterval:F1} seconds.");

        for (int i = 0; i < spawnCount; i++)
        {
            // --- Moved Random Selection Inside the Loop ---
            // Randomly select one prefab from the list for *this* enemy
            int randomIndex = Random.Range(0, enemyPrefabs.Count);
            GameObject selectedPrefab = enemyPrefabs[randomIndex];

            // Check if the selected prefab is null
            if (selectedPrefab == null)
            {
                Debug.LogWarning($"Selected prefab at index {randomIndex} is null! Skipping this enemy spawn.");
                continue; // Skip this iteration and try to spawn the next enemy
            }
            // --- End Moved Random Selection ---


            // Generate a random offset within the spawn radius
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f, // Keep the Y-axis constant (assuming ground spawning)
                Random.Range(-spawnRadius, spawnRadius)
            );

            // Calculate the final spawn position
            Vector3 spawnPosition = spawnLocation.position + randomOffset;

            // Spawn the selected prefab
            GameObject spawnedObject = Instantiate(selectedPrefab, spawnPosition, spawnLocation.rotation);

            // Removed SpawnedObjectHandler logic as SO dependency is removed.
            // If enemies still need resource values on destruction, you'll need a different script attached to the prefabs.
        }
    }

    private void AddResources(int amount)
    {
        // Call the ModifyResource method on the assigned ResourceManager
        if (resourceManager != null)
        {
            resourceManager.ModifyResource(amount);
            Debug.Log($"Added {amount} resources via ResourceManager at the start of wave {currentWave + 1}.");
        }
        else
        {
            Debug.LogWarning("ResourceManager is not assigned, cannot add resources.");
        }
    }


    private void UpdateWaveCountdownText()
    {
        if (waveCountdownText != null)
        {
            // Calculate the remaining time until the next wave
            float timeRemaining = Mathf.Max(0, currentSpawnInterval - timer);

            // Update the TMP text with the formatted time
            waveCountdownText.text = $"Next Wave In: {timeRemaining:F1} seconds";
        }
    }
}
