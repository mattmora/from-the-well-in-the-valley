using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarCollection : MonoBehaviour
{
    public List<SpriteRenderer> starSlots;
    public Sprite starIcon;

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < Services.Player.stars; i++)
        {
            starSlots[i].sprite = starIcon;
        }
    }
}
