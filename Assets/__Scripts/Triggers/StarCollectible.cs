using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarCollectible : Trigger
{
    public GameObject star;

    protected override void OnTrigger()
    {

        transform.parent = Services.Player.transform;
        Services.Audio.Play("Snare", 2f, 0.8f);
        Sequence collect = DOTween.Sequence();
        //collect.Append(DOTween.To(() => Time.timeScale, t => Time.timeScale = t, 0f, 0.2f));
        //collect.AppendInterval(0.6f);
        collect.Append(transform.DOLocalJump(Vector3.up * 1.25f, 3f, 1, 0.5f).SetEase(Ease.Linear));
        collect.AppendCallback(() => Services.Audio.Play("Chime", 1f, 0.8f));
        collect.AppendInterval(1f);
        collect.Append(star.transform.DOScale(0f, 0.1f));
        collect.AppendCallback(() => Destroy(star));
        collect.AppendInterval(0.6f);
        //collect.Append(DOTween.To(() => Time.timeScale, t => Time.timeScale = t, 1f, 0.05f));
        collect.AppendCallback(() => {
            Services.Player.stars++;
            Destroy(gameObject);
        });
        collect.SetUpdate(true);
    }
}
