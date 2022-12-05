using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public List<AudioClip> clips;
    private Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>();

    private void Awake()
    {
        Services.Audio = this;

        foreach (AudioClip clip in clips)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            sources.Add(clip.name, source);
        }
    }

    public void Play(string clipName, float pitch = 1f, float volume = 0.3f)
    {
        sources[clipName].pitch = pitch;
        sources[clipName].volume = volume;
        sources[clipName].Play();
    }
}
