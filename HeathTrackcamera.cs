using UnityEngine;

public class HealthTrackCamera : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Get the main camera in the scene
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        // Make the object face the camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}