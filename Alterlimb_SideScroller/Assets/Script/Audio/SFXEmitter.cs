using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SfxEmitter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] AudioSource source;

    [Header("Lecture")]
    [SerializeField] float volume = 1f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool blockedWhilePaused = true;

    [Header("Debug")]
    [SerializeField] bool logAttenuation = false;

    AudioProxi proximity;
    AudioClip lastClip;

    public AudioSource Source => source;

    void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;

        proximity = GetComponent<AudioProxi>();
    }

    void Start()
    {
        if (source.outputAudioMixerGroup == null)
            Debug.LogError($"[SfxEmitter] '{name}' : le champ Output de l'AudioSource est vide, le son ne passera pas par le mixer.", this);

        if (source.spatialBlend > 0f)
            Debug.LogWarning($"[SfxEmitter] '{name}' : Spatial Blend n'est pas a 0. Utilise plutot un AudioProximity, le rolloff 3D d'Unity compte le Z de la camera.", this);
    }

    public void Play(AudioClip[] clips)
    {
        Play(clips, 1f);
    }

    public void Play(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        Play(clip, volumeScale);
    }

    public void Play(AudioClip clip)
    {
        Play(clip, 1f);
    }

    public void Play(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        if (blockedWhilePaused && Time.timeScale <= 0f) return;

        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;
        if (attenuation <= 0.001f) return;

        if (logAttenuation)
            Debug.Log($"[SfxEmitter] '{name}' : attenuation = {attenuation:0.00}", this);

        lastClip = clip;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume * Mathf.Max(volumeScale, 0f) * attenuation);
    }

    public void Stop()
    {
        if (source != null) source.Stop();
    }
}