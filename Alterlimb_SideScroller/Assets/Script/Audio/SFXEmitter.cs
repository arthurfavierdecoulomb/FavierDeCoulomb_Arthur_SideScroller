using UnityEngine;
using UnityEngine.Audio;

public class SfxEmitter : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Lecture")]
    [SerializeField] float volume = 1f;
    [Range(0f, 1f)]
    [SerializeField] float spatialBlend = 0f;
    [SerializeField] float minDistance = 4f;
    [SerializeField] float maxDistance = 30f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool blockedWhilePaused = true;

    AudioSource source;
    AudioClip lastClip;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.outputAudioMixerGroup = sfxGroup;

        if (sfxGroup == null)
            Debug.LogError($"[SfxEmitter] '{name}' n'a pas de Sfx Group assigne : ses sons ignoreront le mixer.", this);
    }

    public void Play(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        Play(clip);
    }

    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (blockedWhilePaused && Time.timeScale <= 0f) return;

        lastClip = clip;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume);
    }

    public void Stop()
    {
        if (source != null) source.Stop();
    }
}