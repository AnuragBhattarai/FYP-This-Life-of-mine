using UnityEngine;
using UnityEngine.UI; // Required for using UI components like Slider
using TMPro; // Required for using TextMeshPro components

[System.Serializable]
public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int maxResource = 100; // Maximum resource value
    [SerializeField] private Slider resourceBar; // UI Slider to display resource value
    [SerializeField] private TMP_Text resourceText; // TextMeshPro Text to display resource value as text

    private int resource; // Current resource value

    private void Start()
    {
        // Initialize the resource to the maximum value
        resource = maxResource;

        // Initialize the UI elements if assigned
        UpdateUI();
    }

    // Public method to modify the resource value
    public void ModifyResource(int amount)
    {
        resource += amount;
        resource = Mathf.Clamp(resource, 0, maxResource); // Ensure resource stays between 0 and maxResource
        UpdateUI(); // Update the UI whenever the resource changes
    }

    // Public method to get the current resource value
    public int GetResourceValue()
    {
        return resource;
    }

    // Public method to get the maximum resource value
    public int GetMaxResourceValue()
    {
        return maxResource;
    }

    // Method to update the UI elements
    private void UpdateUI()
    {
        if (resourceBar != null)
        {
            resourceBar.maxValue = maxResource;
            resourceBar.value = resource;
        }

        if (resourceText != null)
        {
            resourceText.text = "Resource: " + resource + " / " + maxResource;
        }
    }
}