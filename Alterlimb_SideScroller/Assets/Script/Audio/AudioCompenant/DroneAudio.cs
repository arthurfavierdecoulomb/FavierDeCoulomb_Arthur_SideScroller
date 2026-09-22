using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(DroneEnemy))]
public class DroneAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Range(0, 256)]
    [SerializeField] int loopPriority = 170;
    [Range(0, 256)]
    [SerializeField] int sfxPriority = 120;

    [Header("Rotor")]
    [SerializeField] AudioClip rotorLoop;
    [Range(0f, 1f)]
    [SerializeField] float rotorIdleVolume = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] float rotorMaxVolume = 0.6f;
    [SerializeField] float rotorIdlePitch = 0.8f;
    [SerializeField] float rotorMaxPitch = 1.25f;
    [SerializeField] float rotorSmoothing = 2.5f;

    [Header("Flamme arriere")]
    [SerializeField] AudioClip flameLoop;
    [Range(0f, 1f)]
    [SerializeField] float flameIdleVolume = 0f;
    [Range(0f, 1f)]
    [SerializeField] float flameMaxVolume = 0.5f;
    [SerializeField] float flameIdlePitch = 0.9f;
    [SerializeField] float flameMaxPitch = 1.15f;
    [SerializeField] float flameSmoothing = 4f;

    [Header("Laser")]
    [SerializeField] AudioClip laserLoop;
    [Range(0f, 1f)]
    [SerializeField] float laserVolume = 0.7f;
    [SerializeField] float laserFadeInSpeed = 12f;
    [SerializeField] float laserFadeOutSpeed = 8f;
    [Range(0.3f, 1f)]
    [SerializeField] float laserBlinkPitch = 0.72f;
    [Range(0f, 1f)]
    [SerializeField] float laserBlinkOffVolume = 0.12f;
    [SerializeField] float laserBlinkSpeed = 40f;

    [Header("One-shots")]
    [SerializeField] AudioClip[] aimClips;
    [SerializeField] AudioClip[] lockClips;
    [SerializeField] AudioClip[] fireClips;
    [Range(0f, 1f)]
    [SerializeField] float aimVolume = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] float lockVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] float fireVolume = 0.9f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Explosion")]
    [SerializeField] AudioClip[] explosionClips;
    [Range(0f, 1f)]
    [SerializeField] float explosionVolume = 1f;
    [SerializeField] Vector2 explosionPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    DroneEnemy drone;
    DroneReactor reactor;
    AudioProxi proximity;

    AudioSource oneShotSource;
    AudioSource rotorSource;
    AudioSource flameSource;
    AudioSource laserSource;

    AudioClip lastClip;
    bool wasAiming;
    bool wasLocked;
    bool wasFiring;

    void Awake()
    {
        drone = GetComponent<DroneEnemy>();
        reactor = GetComponent<DroneReactor>();
        proximity = GetComponent<AudioProxi>();

        oneShotSource = CreateSource(sfxGroup, sfxPriority, false);

        if (rotorLoop != null)
        {
            rotorSource = CreateSource(loopGroup, loopPriority, true);
            rotorSource.clip = rotorLoop;
        }

        if (flameLoop != null)
        {
            flameSource = CreateSource(loopGroup, loopPriority, true);
            flameSource.clip = flameLoop;
        }

        if (laserLoop != null)
        {
            laserSource = CreateSource(sfxGroup, sfxPriority, true);
            laserSource.clip = laserLoop;
        }
    }

    void Start()
    {
        if (loopGroup == null && (rotorLoop != null || flameLoop != null))
            Debug.LogError($"[DroneAudio] '{name}' : Loop Group non assigné, les boucles ne passeront pas par le mixer.", this);

        if (sfxGroup == null)
            Debug.LogError($"[DroneAudio] '{name}' : Sfx Group non assigné.", this);

        StartLoop(rotorSource, rotorLoop);
        StartLoop(flameSource, flameLoop);
        StartLoop(laserSource, laserLoop);
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
        bool muted = (muteWhilePaused && Time.timeScale <= 0f) || !drone.IsAlive;
        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;

        UpdateOneShots(muted);
        UpdateMotorLoops(muted, attenuation);
        UpdateLaserLoop(muted, attenuation);
    }

    void UpdateOneShots(bool muted)
    {
        bool aiming = drone.IsAiming;
        bool locked = drone.IsLocked;
        bool firing = drone.IsFiring;

        if (!muted)
        {
            if (aiming && !wasAiming) PlayOneShot(aimClips, aimVolume, pitchRange);
            if (locked && !wasLocked) PlayOneShot(lockClips, lockVolume, pitchRange);
            if (firing && !wasFiring) PlayOneShot(fireClips, fireVolume, pitchRange);
        }

        wasAiming = aiming;
        wasLocked = locked;
        wasFiring = firing;
    }

    void UpdateMotorLoops(bool muted, float attenuation)
    {
        float load = muted ? 0f : drone.MotorLoad;

        if (rotorSource != null)
        {
            float targetVolume = muted ? 0f : Mathf.Lerp(rotorIdleVolume, rotorMaxVolume, load) * attenuation;
            float targetPitch = Mathf.Lerp(rotorIdlePitch, rotorMaxPitch, load);

            rotorSource.volume = Mathf.MoveTowards(rotorSource.volume, targetVolume, rotorSmoothing * Time.unscaledDeltaTime);
            rotorSource.pitch = Mathf.MoveTowards(rotorSource.pitch, targetPitch, rotorSmoothing * Time.unscaledDeltaTime);

            PauseIfSilent(rotorSource, targetVolume);
        }

        if (flameSource != null)
        {
            float thrust = reactor != null ? reactor.ThrustLevel : load;

            float targetVolume = muted ? 0f : Mathf.Lerp(flameIdleVolume, flameMaxVolume, thrust) * attenuation;
            float targetPitch = Mathf.Lerp(flameIdlePitch, flameMaxPitch, thrust);

            flameSource.volume = Mathf.MoveTowards(flameSource.volume, targetVolume, flameSmoothing * Time.unscaledDeltaTime);
            flameSource.pitch = Mathf.MoveTowards(flameSource.pitch, targetPitch, flameSmoothing * Time.unscaledDeltaTime);

            PauseIfSilent(flameSource, targetVolume);
        }
    }

    void UpdateLaserLoop(bool muted, float attenuation)
    {
        if (laserSource == null) return;

        bool blinking = drone.IsBeamBlinking;
        bool visible = drone.IsBeamVisible;

        float targetVolume;
        float targetPitch;
        float speed;

        if (muted)
        {
            targetVolume = 0f;
            targetPitch = laserSource.pitch;
            speed = laserFadeOutSpeed;
        }
        else if (blinking)
        {
            targetVolume = laserVolume * (visible ? 1f : laserBlinkOffVolume) * attenuation;
            targetPitch = laserBlinkPitch;
            speed = laserBlinkSpeed;
        }
        else
        {
            targetVolume = visible ? laserVolume * attenuation : 0f;
            targetPitch = 1f;
            speed = visible ? laserFadeInSpeed : laserFadeOutSpeed;
        }

        laserSource.volume = Mathf.MoveTowards(laserSource.volume, targetVolume, speed * Time.unscaledDeltaTime);
        laserSource.pitch = Mathf.MoveTowards(laserSource.pitch, targetPitch, laserBlinkSpeed * Time.unscaledDeltaTime);

        PauseIfSilent(laserSource, targetVolume);
    }

    void PauseIfSilent(AudioSource source, float target)
    {
        if (source.volume <= 0.001f && target <= 0.001f)
        {
            if (source.isPlaying) source.Pause();
        }
        else if (!source.isPlaying)
        {
            source.UnPause();
        }
    }

    void PlayOneShot(AudioClip[] clips, float volume, Vector2 clipPitchRange)
    {
        if (oneShotSource == null) return;
        if (clips == null || clips.Length == 0) return;

        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;
        if (attenuation <= 0.001f) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastClip = clip;
        oneShotSource.pitch = Random.Range(clipPitchRange.x, clipPitchRange.y);
        oneShotSource.PlayOneShot(clip, volume * attenuation);
    }

    public void PlayExplosionSound()
    {
        if (muteWhilePaused && Time.timeScale <= 0f) return;

        PlayOneShot(explosionClips, explosionVolume, explosionPitchRange);
    }
}