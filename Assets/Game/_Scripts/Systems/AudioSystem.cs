using UnityEngine;
using System;

/// <summary>
/// Insanely basic audio system which supports 3D sound.
/// Ensure you change the 'Sounds' audio source to use 3D spatial blend if you intend to use 3D sounds.
/// </summary>
public class AudioSystem : StaticInstance<AudioSystem>
{
    [SerializeField] private AudioSource _musicSource;
    [SerializeField] private AudioSource _soundsSource;

    public Sound[] sounds;

    void Start()
    {
        foreach (Sound s in sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    public void PlayMusic(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Sound: " + name + " not found!");
            return;
        }

        _musicSource.clip = s.clip;
        _musicSource.loop = s.loop;
        _musicSource.volume = s.volume;

        _musicSource.Play();
    }

    public void PlaySound(string name, Vector3 pos, float vol = 1)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Sound: " + name + " not found!");
            return;
        }

        _soundsSource.transform.position = pos;
        _soundsSource.clip = s.source.clip;
        _soundsSource.loop = s.source.loop;
        _soundsSource.volume = s.source.volume;

        if (_soundsSource.loop)
            _soundsSource.Play();
        else
            _soundsSource.PlayOneShot(_soundsSource.clip, vol);
    }
}