using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Vector3 offsetFromPlayer;

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = Services.Player.transform.position + offsetFromPlayer;

        // check camera wall collider and offset from it, overwriting player offset
    }
}
