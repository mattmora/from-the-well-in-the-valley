using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveSpeedTrigger : Trigger
{
    public float groundSpeed = 100f;
    public float aerialSpeed = 100f;

    protected override void OnTrigger()
    {
        Services.Player.move.groundedAcceleration = groundSpeed;
        Services.Player.move.aerialAcceleration = aerialSpeed;
        Services.Player.animator.speed = (groundSpeed * groundSpeed) / (100f * 100f);
    }
}

