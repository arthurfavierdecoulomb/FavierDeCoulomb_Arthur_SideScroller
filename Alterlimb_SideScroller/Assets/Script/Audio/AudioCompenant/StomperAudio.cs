using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(Stomper))]
public class StomperAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Range(0, 256)]
    [SerializeField] int loopPriority = 120;
    [Range(0, 256)]
    [SerializeField] int sfxPriority = 40;

    [Header("Servo de deplacement")]
    [SerializeField] AudioClip servoLoop;
    [Range(0f, 1f)]
    [SerializeField] float servoVolume = 0.55f;
    [SerializeField] float servoMinPitch = 0.85f;
    [SerializeField] float servoMaxPitch = 1.15f;
    [SerializeField] float servoFadeInSpeed = 10f;
    [SerializeField] float servoFadeOutSpeed = 8f;
    [SerializeField] float moveSpeedThreshold = 0.5f;
    [SerializeField] float teleportThreshold = 50f;

    [Header("Arret")]
    [SerializeField] AudioClip[] stopClips;
    [Range(0f, 1f)]
    [SerializeField] float stopVolume = 0.7f;
    [SerializeField] float stopConfirmDelay = 0.06f;

    [Header("Ecran")]
    [SerializeField] AudioClip[] screenBleepClips;
    [Range(0f, 1f)]
    [SerializeField] float screenBleepVolume = 0.5f;

    [Header("Ecraseur")]
    [SerializeField] AudioClip[] releaseClips;
    [Range(0f, 1f)]
    [SerializeField] float releaseVolume = 0.8f;
    [SerializeField] AudioClip[] impactClips;
    [Range(0f, 1f)]
    [SerializeField] float impactVolume = 1f;

    [Header("Variation")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    Stomper stomper;
    AudioProxi proximity;

    AudioSource oneShotSource;
    AudioSource servoSource;

    AudioClip lastClip;
    Vector3 lastPosition;
    float currentSpeed;
    bool wasMoving;
    float stillTimer;
    bool stopPending;
    int lastScreenCount;

    bool IsPaused => muteWhilePaused && Time.timeScale <= 0f;

    void Awake()
    {
        stomper = GetComponent<Stomper>();
        proximity = GetComponent<AudioProxi>();

        oneShotSource = CreateSource(sfxGroup, sfxPriority, false);

        if (servoLoop != null)
        {
            servoSource = CreateSource(loopGroup, loopPriority, true);
            servoSource.clip = servoLoop;
        }
    }

    void OnEnable()
    {
        if (stomper == null) stomper = GetComponent<Stomper>();

        stomper.onSlamStart.AddListener(OnSlamStart);
        stomper.onImpact.AddListener(OnImpact);
    }

    void OnDisable()
    {
        if (stomper == null) return;

        stomper.onSlamStart.RemoveListener(OnSlamStart);
        stomper.onImpact.RemoveListener(OnImpact);
    }

    void Start()
    {
        if (loopGroup == null && servoLoop != null)
            Debug.LogError($"[StomperAudio] '{name}' : Loop Group non assigné, le servo ne passera pas par le mixer.", this);

        if (sfxGroup == null)
            Debug.LogError($"[StomperAudio] '{name}' : Sfx Group non assigné.", this);

        lastPosition = transform.position;
        lastScreenCount = stomper.ScreenChangeCount;

        if (servoSource != null)
        {
            servoSource.Play();
            servoSource.time = Random.Range(0f, servoLoop.length);
        }
    }

    AudioSource CreateSource(AudioMixerGroup group, int priority, bool looping)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = looping;
        source.spatialBlend = 0f;
        source.volume = looping ? 0f : 1f;
        source.priority = priority;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = group;
        return source;
    }

    void LateUpdate()
    {
        MeasureMovement();
        UpdateStop();
        UpdateServo();
        UpdateScreen();
    }

    void MeasureMovement()
    {
        Vector3 position = transform.position;

        if (Time.deltaTime > 0f)
        {
            float speed = Vector3.Distance(position, lastPosition) / Time.deltaTime;
            currentSpeed = speed > teleportThreshold ? 0f : speed;
        }

        lastPosition = position;
    }

    void UpdateStop()
    {
        if (Time.deltaTime <= 0f) return;

        bool moving = currentSpeed >= moveSpeedThreshold;

        if (moving)
        {
            wasMoving = true;
            stopPending = false;
            stillTimer = 0f;
            return;
        }

        if (!wasMoving) return;

        if (!stopPending)
        {
            stopPending = true;
            stillTimer = 0f;
        }

        stillTimer += Time.deltaTime;
        if (stillTimer < stopConfirmDelay) return;

        stopPending = false;
        wasMoving = false;

        if (!IsPaused) PlayOneShot(stopClips, stopVolume);
    }

    void UpdateServo()
    {
        if (servoSource == null) return;

        bool moving = currentSpeed >= moveSpeedThreshold && !IsPaused;

        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;
        float targetVolume = moving ? servoVolume * attenuation : 0f;
        float speed = moving ? servoFadeInSpeed : servoFadeOutSpeed;

        servoSource.volume = Mathf.MoveTowards(servoSource.volume, targetVolume, speed * Time.unscaledDeltaTime);

        float ratio = Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, stomper.ReferenceSpeed));
        float targetPitch = Mathf.Lerp(servoMinPitch, servoMaxPitch, ratio);

        if (moving)
            servoSource.pitch = Mathf.MoveTowards(servoSource.pitch, targetPitch, 4f * Time.unscaledDeltaTime);

        if (servoSource.volume <= 0.001f && targetVolume <= 0.001f)
        {
            if (servoSource.isPlaying) servoSource.Pause();
        }
        else if (!servoSource.isPlaying)
        {
            servoSource.UnPause();
        }
    }

    void UpdateScreen()
    {
        int count = stomper.ScreenChangeCount;
        if (count == lastScreenCount) return;

        lastScreenCount = count;

        if (!IsPaused) PlayOneShot(screenBleepClips, screenBleepVolume);
    }

    void OnSlamStart()
    {
        PlayOneShot(releaseClips, releaseVolume);
    }

    void OnImpact()
    {
        PlayOneShot(impactClips, impactVolume);
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