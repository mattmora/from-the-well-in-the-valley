using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WingCollectible : Trigger
{
    public GameObject wing;

    protected override void OnTrigger()
    {
        Services.Player.numAerialJumps++;

        Sequence collect = DOTween.Sequence();
        collect.Append(DOTween.To(() => Time.timeScale, t => Time.timeScale = t, 0f, 0.2f));
        collect.AppendInterval(0.6f);
        collect.Append(wing.transform.DOJump(Services.Player.transform.position + Vector3.up * 1.5f, 3f, 1, 0.5f).SetEase(Ease.Linear));
        collect.AppendInterval(1f);
        collect.Append(wing.transform.DOScale(0f, 0.1f));
        collect.AppendCallback(() => Destroy(wing));
        collect.AppendInterval(0.6f);
        collect.Append(DOTween.To(() => Time.timeScale, t => Time.timeScale = t, 1f, 0.05f));
        collect.AppendCallback(() => Destroy(gameObject));
        collect.SetUpdate(true);
    }
}
