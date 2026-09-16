using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(SfxEmitter))]
public class WallTelevisionAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] WallTelevision television;

    [Header("Ronflement")]
    [SerializeField] AudioMixerGroup humGroup;
    [SerializeField] AudioClip humLoop;
    [Range(0f, 1f)]
    [SerializeField] float humVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] float interferenceHumVolume = 0.15f;
    [SerializeField] float humFadeInSpeed = 4f;
    [SerializeField] float humFadeOutSpeed = 8f;
    [SerializeField] float connectingPitch = 0.8f;
    [SerializeField] float pitchSmoothing = 12f;

    [Header("One-shots")]
    [SerializeField] AudioClip[] connectionClips;
    [SerializeField] AudioClip[] glitchClips;
    [SerializeField] AudioClip[] shutdownClips;
    [Range(0f, 1f)]
    [SerializeField] float glitchVolume = 0.8f;

    [Header("Priorite audio")]
    [Range(0, 256)]
    [SerializeField] int humPriority = 180;

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    SfxEmitter sfx;
    AudioProxi proximity;
    AudioSource humSource;

    bool wasPossessed;
    bool wasInterference;
    bool wasPoweredOff;

    void Awake()
    {
        sfx = GetComponent<SfxEmitter>();
        proximity = GetComponent<AudioProxi>();

        if (television == null) television = GetComponent<WallTelevision>();
    }

    void Start()
    {
        if (television == null)
        {
            Debug.LogError($"[WallTelevisionAudio] '{name}' ne trouve aucun WallTelevision.", this);
            enabled = false;
            return;
        }

        if (humLoop != null)
        {
            if (humGroup == null)
                Debug.LogError($"[WallTelevisionAudio] '{name}' : Hum Group non assigné, la boucle ne passera pas par le mixer.", this);

            humSource = gameObject.AddComponent<AudioSource>();
            humSource.clip = humLoop;
            humSource.loop = true;
            humSource.playOnAwake = false;
            humSource.spatialBlend = 0f;
            humSource.volume = 0f;
            humSource.priority = humPriority;
            humSource.outputAudioMixerGroup = humGroup;

            humSource.Play();
            humSource.time = Random.Range(0f, humLoop.length);
        }

        wasPossessed = television.IsPossessed;
        wasInterference = television.IsInterferenceOnScreen;
        wasPoweredOff = television.IsPoweredOff;
    }

    void Update()
    {
        bool possessed = television.IsPossessed;
        bool connecting = television.IsConnecting;
        bool interference = television.IsInterferenceOnScreen;
        bool poweredOff = television.IsPoweredOff;

        if (!IsMuted)
        {
            if (possessed && !wasPossessed)
                sfx.Play(connectionClips);

            if (interference && !wasInterference && !connecting)
                sfx.Play(glitchClips, glitchVolume);

            if (poweredOff && !wasPoweredOff)
                sfx.Play(shutdownClips);
        }

        wasPossessed = possessed;
        wasInterference = interference;
        wasPoweredOff = poweredOff;

        UpdateHum(connecting, poweredOff);
    }

    void UpdateHum(bool connecting, bool poweredOff)
    {
        if (humSource == null) return;

        float target;

        if (IsMuted || poweredOff)
            target = 0f;
        else if (television.IsFaceOnScreen)
            target = humVolume;
        else if (television.IsInterferenceOnScreen)
            target = interferenceHumVolume;
        else
            target = 0f;

        if (proximity != null)
            target *= proximity.GetAttenuation();

        float speed = target > humSource.volume ? humFadeInSpeed : humFadeOutSpeed;
        humSource.volume = Mathf.MoveTowards(humSource.volume, target, speed * Time.unscaledDeltaTime);

        float targetPitch = connecting ? connectingPitch : 1f;
        humSource.pitch = Mathf.MoveTowards(humSource.pitch, targetPitch, pitchSmoothing * Time.unscaledDeltaTime);

        if (humSource.volume <= 0.001f && target <= 0.001f)
        {
            if (humSource.isPlaying) humSource.Pause();
        }
        else if (!humSource.isPlaying)
        {
            humSource.UnPause();
        }
    }

    bool IsMuted => muteWhilePaused && Time.timeScale <= 0f;
}