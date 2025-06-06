using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Front : MonoBehaviour
{
    private Transform localTrans;

    public Camera facingCamera;

    private void Start()
    {
        localTrans = GetComponent<Transform>();
    }

    private void LateUpdate()
    {
        if (facingCamera)
        {
            // Make the object look at the camera
            localTrans.rotation = Quaternion.LookRotation(localTrans.position - facingCamera.transform.position);

            // Reset rotation relative to the parent
            localTrans.rotation = Quaternion.Euler(localTrans.rotation.eulerAngles.x, localTrans.rotation.eulerAngles.y, 0f);
        }
    }
}
