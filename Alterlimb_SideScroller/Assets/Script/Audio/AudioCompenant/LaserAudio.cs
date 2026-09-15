using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(SfxEmitter))]
public class LaserAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] LaserBeam laserBeam;

    [Header("Boucle")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioClip beamLoop;
    [Range(0f, 1f)]
    [SerializeField] float loopVolume = 0.7f;
    [SerializeField] float fadeInSpeed = 6f;
    [SerializeField] float fadeOutSpeed = 10f;

    [Header("One-shots")]
    [SerializeField] AudioClip[] chargeClips;
    [SerializeField] AudioClip[] fireClips;
    [SerializeField] AudioClip[] warningClips;
    [SerializeField] AudioClip[] shutdownClips;

    [Header("Instabilite")]
    [SerializeField] float unstableFadeSpeed = 40f;
    [Range(0.3f, 1f)]
    [SerializeField] float unstablePitch = 0.72f;
    [Range(0f, 1f)]
    [SerializeField] float unstableOnVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] float unstableOffVolume = 0.15f;

    [Header("Priorite audio")]
    [Range(0, 256)]
    [SerializeField] int loopPriority = 160;

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    SfxEmitter sfx;
    AudioProxi proximity;
    AudioSource loopSource;

    bool wasActive;
    bool wasCharging;
    bool wasUnstable;

    void Awake()
    {
        sfx = GetComponent<SfxEmitter>();
        proximity = GetComponent<AudioProxi>();

        if (laserBeam == null) laserBeam = GetComponent<LaserBeam>();
    }

    void Start()
    {
        if (laserBeam == null)
        {
            Debug.LogError($"[LaserAudio] '{name}' ne trouve aucun LaserBeam.", this);
            enabled = false;
            return;
        }

        if (beamLoop == null) return;

        if (loopGroup == null)
            Debug.LogError($"[LaserAudio] '{name}' : Loop Group non assigné, la boucle ne passera pas par le mixer.", this);

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.clip = beamLoop;
        loopSource.loop = true;
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.volume = 0f;
        loopSource.priority = loopPriority;
        loopSource.outputAudioMixerGroup = loopGroup;

        wasActive = laserBeam.IsActive;
        wasCharging = laserBeam.IsCharging;
        wasUnstable = laserBeam.IsUnstable;
    }

    void Update()
    {
        bool active = laserBeam.IsActive;
        bool charging = laserBeam.IsCharging;
        bool unstable = laserBeam.IsUnstable;

        if (charging && !wasCharging)
            sfx.Play(chargeClips);

        if (unstable && !wasUnstable)
            sfx.Play(warningClips);

        if (active && !wasActive && !unstable)
            sfx.Play(fireClips);

        if (!active && wasActive && !unstable)
            sfx.Play(shutdownClips);

        wasActive = active;
        wasCharging = charging;
        wasUnstable = unstable;

        UpdateLoop(active, unstable);
    }

    void UpdateLoop(bool active, bool unstable)
    {
        if (loopSource == null) return;

        float target;
        float pitch;
        float speed;

        if (muteWhilePaused && Time.timeScale <= 0f)
        {
            target = 0f;
            pitch = loopSource.pitch;
            speed = fadeOutSpeed;
        }
        else if (unstable)
        {
            target = loopVolume * (active ? unstableOnVolume : unstableOffVolume);
            pitch = unstablePitch;
            speed = unstableFadeSpeed;
        }
        else
        {
            target = active ? loopVolume : 0f;
            pitch = 1f;
            speed = active ? fadeInSpeed : fadeOutSpeed;
        }

        if (proximity != null)
            target *= proximity.GetAttenuation();

        loopSource.pitch = Mathf.MoveTowards(loopSource.pitch, pitch, unstableFadeSpeed * Time.unscaledDeltaTime);
        loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, speed * Time.unscaledDeltaTime);

        if (loopSource.volume <= 0.001f && target <= 0.001f)
        {
            if (loopSource.isPlaying) loopSource.Pause();
        }
        else if (!loopSource.isPlaying)
        {
            if (loopSource.time > 0f) loopSource.UnPause();
            else loopSource.Play();
        }
    }
}