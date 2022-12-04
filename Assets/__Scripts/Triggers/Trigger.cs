using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public abstract class Trigger : MonoBehaviour
{
    public bool singleUse = true;
    public bool collected;
    public UnityEvent preTriggerEvents;
    public UnityEvent postTriggerEvents;

    protected abstract void OnTrigger();

    private void OnTriggerEnter(Collider other)
    {
        preTriggerEvents.Invoke();
        OnTrigger();
        postTriggerEvents.Invoke();
        if (singleUse) Destroy(gameObject);
        else collected = true;
    }
}
