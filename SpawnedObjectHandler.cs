using UnityEngine;

public class SpawnedObjectHandler : MonoBehaviour
{
    public int resourceValue; // Resource value to add when destroyed

    private void OnDestroy()
    {
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager != null)
        {
            resourceManager.ModifyResource(resourceValue);
            Debug.Log($"Added {resourceValue} resources. Current total: {resourceManager.GetResourceValue()}");
        }
        else
        {
            Debug.LogWarning("ResourceManager not found in the scene!");
        }
    }
}