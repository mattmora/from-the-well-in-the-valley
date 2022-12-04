using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Vector3 offset;

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = Services.Player.cameraFocus.position + offset;

        // check camera wall collider and offset from it, overwriting player offset
    }
}
