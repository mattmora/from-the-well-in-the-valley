using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bubble : MonoBehaviour
{
    public Rigidbody rb;

    public List<GameObject> sizes;

    private bool popped;

    public void SetSize(int size)
    {
        for (int i = 0; i < sizes.Count; i++)
        {
            sizes[i].SetActive(i == size);
        }
    }

    private void FixedUpdate()
    {
        if (popped) return;
        if (Vector3.Distance(transform.position, Services.Player.transform.position) > 30f)
        {
            Pop();
        }
    }

    private void Pop()
    {
        popped = true;
        Destroy(gameObject);
        Services.Player.bubbleCount--;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (popped) return;
        Vector3 away = Services.Player.transform.position - transform.position;
        if (away.magnitude < 3f)
        {
            Services.Player.rb.velocity = Vector3.zero;
            Services.Player.rb.AddForce(away.normalized * 14f, ForceMode.VelocityChange);
        }
        Pop();
    }
}
