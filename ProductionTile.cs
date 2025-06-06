using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class ProductionTile : MonoBehaviour
{
    [SerializeField] Material tileMaterial;
    [SerializeField] LayerMask collisionLayers;
    [SerializeField] Color noCollisionColor;
    [SerializeField] Color collisionColor;

    public bool colliding { get; private set; }

    // Event to broadcast collision state
    public UnityEvent<bool> OnCollisionStateChanged = new UnityEvent<bool>();

    void Start()
    {
        GetComponent<Renderer>().material.CopyPropertiesFromMaterial(tileMaterial);
        SetColor(noCollisionColor);
    }

    void SetColor(Color c)
    {
        GetComponent<Renderer>().material.SetColor("_TintColor", c);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collisionLayers == (collisionLayers | (1 << other.gameObject.layer)))
        {
            if (other.gameObject.transform.root.gameObject.GetInstanceID() != transform.root.gameObject.GetInstanceID())
            {
                SetColor(collisionColor);
                colliding = true;
                OnCollisionStateChanged.Invoke(true); // Broadcast collision state
                Debug.Log($"ProductionTile collided with {other.gameObject.name}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (collisionLayers == (collisionLayers | (1 << other.gameObject.layer)))
        {
            SetColor(noCollisionColor);
            colliding = false;
            OnCollisionStateChanged.Invoke(false); // Broadcast collision state
            Debug.Log($"ProductionTile no longer colliding with {other.gameObject.name}");
        }
    }
}
