using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AirConditioner))]
public class AirConditionerAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Range(0, 256)]
    [SerializeField] int loopPriority = 200;
    [Range(0, 256)]
    [SerializeField] int sfxPriority = 160;

    [Header("Boucle — ventilation")]
    [SerializeField] AudioClip ventilationLoop;
    [Range(0f, 1f)]
    [SerializeField] float ventilationVolume = 0.5f;

    [Header("Boucle — hors service")]
    [SerializeField] AudioClip hsLoop;
    [Range(0f, 1f)]
    [SerializeField] float hsVolume = 0.35f;

    [Header("Fondus")]
    [SerializeField] float fadeInSpeed = 1.5f;
    [SerializeField] float fadeOutSpeed = 2.5f;

    [Header("Bleeps")]
    [SerializeField] AudioClip[] fanPowerClips;
    [SerializeField] AudioClip[] alertClips;
    [SerializeField] AudioClip[] ventHsClips;
    [Range(0f, 1f)]
    [SerializeField] float fanPowerVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] float alertVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] float ventHsVolume = 0.7f;
    [SerializeField] float bleepMinInterval = 0.05f;
    [SerializeField] Vector2 bleepPitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    AirConditioner conditioner;
    AudioProxi proximity;

    AudioSource oneShotSource;
    AudioSource ventilationSource;
    AudioSource hsSource;

    AudioClip lastClip;
    float lastBleepTime = -999f;

    void Awake()
    {
        conditioner = GetComponent<AirConditioner>();
        proximity = GetComponent<AudioProxi>();

        oneShotSource = CreateSource(sfxGroup, sfxPriority, false);

        if (ventilationLoop != null)
        {
            ventilationSource = CreateSource(loopGroup, loopPriority, true);
            ventilationSource.clip = ventilationLoop;
        }

        if (hsLoop != null)
        {
            hsSource = CreateSource(loopGroup, loopPriority, true);
            hsSource.clip = hsLoop;
        }
    }

    void Start()
    {
        if (loopGroup == null && (ventilationLoop != null || hsLoop != null))
            Debug.LogError($"[AirConditionerAudio] '{name}' : Loop Group non assigné, les boucles ne passeront pas par le mixer.", this);

        if (sfxGroup == null)
            Debug.LogError($"[AirConditionerAudio] '{name}' : Sfx Group non assigné, les bleeps ne passeront pas par le mixer.", this);

        StartLoop(ventilationSource, ventilationLoop);
        StartLoop(hsSource, hsLoop);
    }

    AudioSource CreateSource(AudioMixerGroup group, int priority, bool looping)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = looping;
        source.spatialBlend = 0f;
        source.volume = looping ? 0f : 1f;
        source.priority = priority;
        source.outputAudioMixerGroup = group;
        return source;
    }

    void StartLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;

        source.Play();
        source.time = Random.Range(0f, clip.length);
    }

    void Update()
    {
        AirConditioner.State state = conditioner.CurrentState;
        bool muted = muteWhilePaused && Time.timeScale <= 0f;

        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;

        float ventilationTarget = (!muted && state == AirConditioner.State.Ventilation)
            ? ventilationVolume * attenuation
            : 0f;

        float hsTarget = (!muted && state == AirConditioner.State.HS)
            ? hsVolume * attenuation
            : 0f;

        FadeSource(ventilationSource, ventilationTarget);
        FadeSource(hsSource, hsTarget);
    }

    void FadeSource(AudioSource source, float target)
    {
        if (source == null) return;

        float speed = target > source.volume ? fadeInSpeed : fadeOutSpeed;
        source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.unscaledDeltaTime);

        if (source.volume <= 0.001f && target <= 0.001f)
        {
            if (source.isPlaying) source.Pause();
        }
        else if (!source.isPlaying)
        {
            source.UnPause();
        }
    }

    public void BleepFanPower()
    {
        PlayBleep(fanPowerClips, fanPowerVolume);
    }

    public void BleepAlert()
    {
        PlayBleep(alertClips, alertVolume);
    }

    public void BleepVentHs()
    {
        PlayBleep(ventHsClips, ventHsVolume);
    }

    void PlayBleep(AudioClip[] clips, float volume)
    {
        if (muteWhilePaused && Time.timeScale <= 0f) return;
        if (Time.unscaledTime - lastBleepTime < bleepMinInterval) return;
        if (clips == null || clips.Length == 0) return;

        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;
        if (attenuation <= 0.001f) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastBleepTime = Time.unscaledTime;
        lastClip = clip;
        oneShotSource.pitch = Random.Range(bleepPitchRange.x, bleepPitchRange.y);
        oneShotSource.PlayOneShot(clip, volume * attenuation);
    }
}