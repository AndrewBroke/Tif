using System;
using System.Collections.Generic;
using UnityEngine;

public class MedicSoundController : MonoBehaviour
{
    [Serializable]
    private class SoundEntry
    {
        public string name;
        public AudioSource source;
    }

    [SerializeField] private SoundEntry[] sounds = Array.Empty<SoundEntry>();

    private readonly Dictionary<string, AudioSource> soundLookup =
        new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        foreach (SoundEntry sound in sounds)
        {
            if (sound == null || string.IsNullOrWhiteSpace(sound.name))
                continue;

            string key = sound.name.Trim();
            if (soundLookup.ContainsKey(key))
            {
                Debug.LogWarning($"Duplicate sound name '{key}'.", this);
                continue;
            }

            soundLookup.Add(key, sound.source);
        }
    }

    public void PlaySound(string soundName)
    {
        if (TryGetSound(soundName, out AudioSource source))
            source.Play();
    }

    public void StopSound(string soundName)
    {
        if (TryGetSound(soundName, out AudioSource source))
            source.Stop();
    }

    private bool TryGetSound(string soundName, out AudioSource source)
    {
        source = null;
        if (!string.IsNullOrWhiteSpace(soundName) &&
            soundLookup.TryGetValue(soundName.Trim(), out source) && source != null)
            return true;

        Debug.LogWarning($"Sound '{soundName}' is missing or has no AudioSource.", this);
        return false;
    }

    public void PlaySFX(AudioSource audio)
    {
        audio.Play();
    }

    public void StopSFX(AudioSource audio)
    {
        audio.Stop();
    }

}
