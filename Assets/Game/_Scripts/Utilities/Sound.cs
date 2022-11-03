using UnityEngine;

/// <summary>
/// Very simple class representation of sound objects
/// </summary>
[System.Serializable]
public class Sound
{
    public string name;
    public bool loop;

    public AudioClip clip;
    [HideInInspector]
    public AudioSource source;

    [Range(0f, 1f)]
    public float volume;
    [Range(.1f, 3f)]
    public float pitch;
}
