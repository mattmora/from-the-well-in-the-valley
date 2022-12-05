using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StarCollection : MonoBehaviour
{
    public List<Image> starSlots;
    public Sprite starIcon;
    public GameObject completeText;
    public GameObject completeObject;

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < Services.Player.stars; i++)
        {
            starSlots[i].sprite = starIcon;
        }
        completeText.SetActive(Services.Player.stars == 5 && Services.Player.numAerialJumps < 2);
        completeObject.SetActive(Services.Player.stars == 5);
    }
}
