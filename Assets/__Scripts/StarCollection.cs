using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarCollection : MonoBehaviour
{
    public List<SpriteRenderer> starSlots;
    public Sprite starIcon;
    public GameObject completeText;

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < Services.Player.stars; i++)
        {
            starSlots[i].sprite = starIcon;
        }
        completeText.SetActive(Services.Player.stars == 5);
    }
}
