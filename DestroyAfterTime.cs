using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [SerializeField] private float destroyTime = 5f; // Time in seconds before the object is destroyed

    private void Start()
    {
        Destroy(gameObject, destroyTime);
    }
}