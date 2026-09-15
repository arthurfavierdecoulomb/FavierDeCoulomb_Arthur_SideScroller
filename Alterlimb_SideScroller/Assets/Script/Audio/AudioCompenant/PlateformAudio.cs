using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(SfxEmitter))]
public class PlatformAudio : MonoBehaviour
{
    public enum BleepMode { AnimationEvents, Interval }

    [Header("Boucle")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioClip motorLoop;
    [SerializeField] AudioClip creakLoop;
    [SerializeField] float loopFadeSpeed = 8f;

    [Header("Moteur")]
    [Range(0f, 1f)]
    [SerializeField] float motorVolume = 0.7f;
    [SerializeField] float motorStartDelay = 0.15f;

    [Header("Demarrage / arret")]
    [SerializeField] AudioClip[] startClips;
    [SerializeField] AudioClip[] stopClips;
    [SerializeField] bool clacOnDirectionChange = true;

    [Header("Bleeps de la fleche")]
    [SerializeField] BleepMode bleepMode = BleepMode.AnimationEvents;
    [SerializeField] AudioClip[] bleepUpClips;
    [SerializeField] AudioClip[] bleepDownClips;
    [SerializeField] float bleepInterval = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] float bleepVolume = 0.7f;

    [Header("Affaissement")]
    [Range(0f, 1f)]
    [SerializeField] float creakMinVolume = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] float creakMaxVolume = 1f;
    [SerializeField] Vector2 creakPitchRange = new Vector2(0.9f, 1.3f);
    [SerializeField] AudioClip[] breakClips;

    [Header("Priorite audio")]
    [Range(0, 256)]
    [SerializeField] int loopPriority = 140;

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    SfxEmitter sfx;
    AudioProxi proximity;
    AudioSource loopSource;

    int direction;
    bool motorPending;
    float motorDelayTimer;
    float bleepTimer;

    bool isCollapsing;
    float collapseProgress;

    public int Direction => direction;

    void Awake()
    {
        sfx = GetComponent<SfxEmitter>();
        proximity = GetComponent<AudioProxi>();
    }

    void Start()
    {
        if (motorLoop == null && creakLoop == null) return;

        if (loopGroup == null)
            Debug.LogError($"[PlatformAudio] '{name}' : Loop Group non assigné, la boucle ne passera pas par le mixer.", this);

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.loop = true;
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.volume = 0f;
        loopSource.priority = loopPriority;
        loopSource.outputAudioMixerGroup = loopGroup;
    }

    void Update()
    {
        UpdateMotorDelay();
        UpdateLoop();
        UpdateBleepInterval();
    }

    public void SetDirection(int newDirection)
    {
        newDirection = Mathf.Clamp(newDirection, -1, 1);
        if (newDirection == direction) return;

        int previous = direction;
        direction = newDirection;

        if (previous == 0)
        {
            sfx.Play(startClips);
            motorPending = motorStartDelay > 0f;
            motorDelayTimer = motorStartDelay;
            bleepTimer = 0f;
        }
        else if (direction == 0)
        {
            sfx.Play(stopClips);
            motorPending = false;
        }
        else if (clacOnDirectionChange)
        {
            sfx.Play(stopClips);
        }
    }

    public void SetCollapsing(bool active, float progress)
    {
        isCollapsing = active;
        collapseProgress = Mathf.Clamp01(progress);
    }

    public void PlayBreak()
    {
        isCollapsing = false;
        collapseProgress = 0f;
        sfx.Play(breakClips);
    }

    public void Bleep()
    {
        if (direction > 0) BleepUp();
        else if (direction < 0) BleepDown();
    }

    public void BleepUp()
    {
        sfx.Play(bleepUpClips, bleepVolume);
    }

    public void BleepDown()
    {
        sfx.Play(bleepDownClips, bleepVolume);
    }

    void UpdateMotorDelay()
    {
        if (!motorPending) return;

        motorDelayTimer -= Time.deltaTime;
        if (motorDelayTimer <= 0f) motorPending = false;
    }

    void UpdateBleepInterval()
    {
        if (bleepMode != BleepMode.Interval) return;
        if (direction == 0 || IsMuted) return;

        bleepTimer -= Time.deltaTime;
        if (bleepTimer > 0f) return;

        bleepTimer = bleepInterval;
        Bleep();
    }

    void UpdateLoop()
    {
        if (loopSource == null) return;

        AudioClip wantedClip = null;
        float targetVolume = 0f;

        if (!IsMuted)
        {
            if (isCollapsing && creakLoop != null)
            {
                wantedClip = creakLoop;
                targetVolume = Mathf.Lerp(creakMinVolume, creakMaxVolume, collapseProgress);
                loopSource.pitch = Mathf.Lerp(creakPitchRange.x, creakPitchRange.y, collapseProgress);
            }
            else if (direction != 0 && !motorPending && motorLoop != null)
            {
                wantedClip = motorLoop;
                targetVolume = motorVolume;
                loopSource.pitch = 1f;
            }

            if (proximity != null)
                targetVolume *= proximity.GetAttenuation();
        }

        if (wantedClip != null && loopSource.clip != wantedClip)
        {
            loopSource.clip = wantedClip;
            loopSource.volume = 0f;
            loopSource.Play();
        }

        loopSource.volume = Mathf.MoveTowards(loopSource.volume, targetVolume, loopFadeSpeed * Time.unscaledDeltaTime);

        if (loopSource.volume <= 0.001f && targetVolume <= 0.001f)
        {
            if (loopSource.isPlaying) loopSource.Pause();
        }
        else if (!loopSource.isPlaying && wantedClip != null)
        {
            loopSource.UnPause();
        }
    }

    bool IsMuted => muteWhilePaused && Time.timeScale <= 0f;
}