using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SfxEmitter : MonoBehaviour
{
    [Header("Lecture")]
    [SerializeField] float volume = 1f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool blockedWhilePaused = true;

    [Header("Debug")]
    [SerializeField] bool logDistanceToListener = false;

    AudioSource source;
    AudioClip lastClip;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
    }

    void Start()
    {
        if (source.outputAudioMixerGroup == null)
            Debug.LogError($"[SfxEmitter] '{name}' : le champ Output de l'AudioSource est vide, le son ne passera pas par le mixer.", this);

        if (source.spatialBlend > 0f)
        {
            AudioListener listener = FindAnyObjectByType<AudioListener>();

            if (listener == null)
            {
                Debug.LogError($"[SfxEmitter] '{name}' est en son 3D mais aucun AudioListener n'existe dans la scène.", this);
            }
            else if (Mathf.Abs(listener.transform.position.z - transform.position.z) > source.maxDistance)
            {
                Debug.LogError($"[SfxEmitter] '{name}' : l'AudioListener est a {Mathf.Abs(listener.transform.position.z - transform.position.z):0.0} unites sur l'axe Z, soit au-dela du Max Distance ({source.maxDistance}). Le son sera inaudible. Deplace l'AudioListener sur le joueur ou augmente Max Distance.", this);
            }
        }
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

        if (logDistanceToListener)
        {
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
                Debug.Log($"[SfxEmitter] '{name}' : distance au listener = {Vector3.Distance(listener.transform.position, transform.position):0.00} (min {source.minDistance}, max {source.maxDistance})", this);
        }

        lastClip = clip;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume * Mathf.Max(volumeScale, 0f));
    }

    public void Stop()
    {
        if (source != null) source.Stop();
    }
}