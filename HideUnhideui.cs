using UnityEngine;

public class HideUnhideTwoObjects : MonoBehaviour
{
    public GameObject objectToShow;   // Assign the GameObject you want to show
    public GameObject objectToHide;   // Assign the GameObject you want to hide

    void Start()
    {
        // Ensure initial states are set correctly when the scene starts
        // You can comment out or change these based on your desired initial visibility
        objectToShow.SetActive(true);  // objectToShow is initially visible
        objectToHide.SetActive(false); // objectToHide is initially hidden
    }

    void Update()
    {
        // Check if the 'Tab' key is pressed down
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // If objectToShow is currently active (visible)
            if (objectToShow.activeSelf)
            {
                objectToShow.SetActive(false); // Hide objectToShow
                objectToHide.SetActive(true);  // Show objectToHide
            }
            // If objectToShow is currently inactive (hidden)
            else
            {
                objectToShow.SetActive(true);  // Show objectToShow
                objectToHide.SetActive(false); // Hide objectToHide
            }
        }
    }
}