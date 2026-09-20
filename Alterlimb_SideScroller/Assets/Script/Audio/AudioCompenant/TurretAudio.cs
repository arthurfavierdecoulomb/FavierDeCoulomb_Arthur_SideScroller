using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(Turret))]
public class TurretAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Range(0, 256)]
    [SerializeField] int loopPriority = 180;
    [Range(0, 256)]
    [SerializeField] int sfxPriority = 120;

    [Header("Moteur")]
    [SerializeField] AudioClip motorLoop;
    [Range(0f, 1f)]
    [SerializeField] float motorIdleVolume = 0.3f;
    [Range(0f, 1f)]
    [SerializeField] float motorAlertVolume = 0.6f;
    [SerializeField] float motorIdlePitch = 0.85f;
    [SerializeField] float motorAlertPitch = 1.35f;
    [SerializeField] float motorSmoothing = 3f;

    [Header("Servo de la tete")]
    [SerializeField] AudioClip servoLoop;
    [Range(0f, 1f)]
    [SerializeField] float servoMaxVolume = 0.5f;
    [SerializeField] float servoMaxTurnRate = 120f;
    [SerializeField] float servoMinTurnRate = 8f;
    [SerializeField] float servoIdlePitch = 0.9f;
    [SerializeField] float servoMaxPitch = 1.2f;
    [SerializeField] float servoSmoothing = 8f;

    [Header("Clac de fin de mouvement")]
    [SerializeField] AudioClip[] settleClips;
    [Range(0f, 1f)]
    [SerializeField] float settleVolume = 0.6f;
    [SerializeField] float settleArmTurnRate = 40f;
    [SerializeField] float settleTriggerTurnRate = 10f;
    [SerializeField] float settleMinInterval = 0.25f;

    [Header("One-shots")]
    [SerializeField] AudioClip[] readyClips;
    [SerializeField] AudioClip[] shootClips;
    [SerializeField] AudioClip[] alertClips;
    [Range(0f, 1f)]
    [SerializeField] float readyVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] float shootVolume = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] float alertVolume = 0.7f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    Turret turret;
    AudioProxi proximity;

    AudioSource oneShotSource;
    AudioSource motorSource;
    AudioSource servoSource;

    AudioClip lastClip;
    bool wasReady;
    bool wasInSight;
    int lastShotCount;
    bool settleArmed;
    float lastSettleTime = -999f;

    void Awake()
    {
        turret = GetComponent<Turret>();
        proximity = GetComponent<AudioProxi>();

        oneShotSource = CreateSource(sfxGroup, sfxPriority, false);

        if (motorLoop != null)
        {
            motorSource = CreateSource(loopGroup, loopPriority, true);
            motorSource.clip = motorLoop;
        }

        if (servoLoop != null)
        {
            servoSource = CreateSource(loopGroup, loopPriority, true);
            servoSource.clip = servoLoop;
        }
    }

    void Start()
    {
        if (loopGroup == null && (motorLoop != null || servoLoop != null))
            Debug.LogError($"[TurretAudio] '{name}' : Loop Group non assigné, les boucles ne passeront pas par le mixer.", this);

        if (sfxGroup == null)
            Debug.LogError($"[TurretAudio] '{name}' : Sfx Group non assigné.", this);

        StartLoop(motorSource, motorLoop);
        StartLoop(servoSource, servoLoop);

        lastShotCount = turret.ShotCount;
        wasInSight = turret.PlayerInSight;
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
        bool muted = muteWhilePaused && Time.timeScale <= 0f;
        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;

        UpdateOneShots(muted);
        UpdateSettle(muted);
        UpdateMotor(muted, attenuation);
        UpdateServo(muted, attenuation);
    }

    void UpdateSettle(bool muted)
    {
        float rate = turret.HeadTurnRate;

        if (rate >= settleArmTurnRate)
        {
            settleArmed = true;
            return;
        }

        if (!settleArmed) return;
        if (rate > settleTriggerTurnRate) return;

        settleArmed = false;

        if (muted) return;
        if (Time.unscaledTime - lastSettleTime < settleMinInterval) return;

        lastSettleTime = Time.unscaledTime;
        PlayOneShot(settleClips, settleVolume);
    }

    void UpdateOneShots(bool muted)
    {
        bool inSight = turret.PlayerInSight;
        bool ready = turret.IsReadyToFire;
        int shots = turret.ShotCount;

        if (!muted)
        {
            if (inSight && !wasInSight) PlayOneShot(alertClips, alertVolume);
            if (ready && !wasReady) PlayOneShot(readyClips, readyVolume);
            if (shots != lastShotCount) PlayOneShot(shootClips, shootVolume);
        }

        wasInSight = inSight;
        wasReady = ready;
        lastShotCount = shots;
    }

    void UpdateMotor(bool muted, float attenuation)
    {
        if (motorSource == null) return;

        float ratio = turret.SpinRatio;

        float targetVolume = muted ? 0f : Mathf.Lerp(motorIdleVolume, motorAlertVolume, ratio) * attenuation;
        float targetPitch = Mathf.Lerp(motorIdlePitch, motorAlertPitch, ratio);

        motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVolume, motorSmoothing * Time.unscaledDeltaTime);
        motorSource.pitch = Mathf.MoveTowards(motorSource.pitch, targetPitch, motorSmoothing * Time.unscaledDeltaTime);

        PauseIfSilent(motorSource, targetVolume);
    }

    void UpdateServo(bool muted, float attenuation)
    {
        if (servoSource == null) return;

        float rate = turret.HeadTurnRate;
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(servoMinTurnRate, servoMaxTurnRate, rate));

        float targetVolume = muted ? 0f : servoMaxVolume * normalized * attenuation;
        float targetPitch = Mathf.Lerp(servoIdlePitch, servoMaxPitch, normalized);

        servoSource.volume = Mathf.MoveTowards(servoSource.volume, targetVolume, servoSmoothing * Time.unscaledDeltaTime);
        servoSource.pitch = Mathf.MoveTowards(servoSource.pitch, targetPitch, servoSmoothing * Time.unscaledDeltaTime);

        PauseIfSilent(servoSource, targetVolume);
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

    void PlayOneShot(AudioClip[] clips, float volume)
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
        oneShotSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShotSource.PlayOneShot(clip, volume * attenuation);
    }
}