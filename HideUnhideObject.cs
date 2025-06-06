using UnityEngine;

public class HideUnhideObjectAndScript : MonoBehaviour
{
    public GameObject objectToToggle; // Assign the GameObject you want to hide/unhide in the Inspector
    public MonoBehaviour scriptToToggle; // Assign the script component you want to enable/disable

    void Update()
    {
        // Check if the 'M' key is pressed down
        if (Input.GetKeyDown(KeyCode.M))
        {
            // Toggle the active state of the GameObject
            objectToToggle.SetActive(!objectToToggle.activeSelf);

            // Toggle the enabled state of the selected script
            // Ensure the scriptToToggle is not null before trying to access its 'enabled' property
            if (scriptToToggle != null)
            {
                scriptToToggle.enabled = !scriptToToggle.enabled;
            }
            else
            {
                Debug.LogWarning("Script to toggle is not assigned in the Inspector for " + gameObject.name);
            }
        }
    }
}