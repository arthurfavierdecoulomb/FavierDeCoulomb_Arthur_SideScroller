using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(ElevatorPlatform))]
public class ElevatorAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [Range(0, 256)]
    [SerializeField] int loopPriority = 150;
    [Range(0, 256)]
    [SerializeField] int sfxPriority = 110;

    [Header("Moteur en mouvement")]
    [SerializeField] AudioClip motorLoop;
    [Range(0f, 1f)]
    [SerializeField] float motorVolume = 0.6f;
    [SerializeField] float motorStartDelay = 0.2f;
    [SerializeField] float motorFadeInSpeed = 2.5f;
    [SerializeField] float motorFadeOutSpeed = 4f;
    [SerializeField] float upPitch = 1.05f;
    [SerializeField] float downPitch = 0.92f;
    [SerializeField] float pitchSmoothing = 4f;

    [Header("Demarrage et arret")]
    [SerializeField] AudioClip[] startClips;
    [Range(0f, 1f)]
    [SerializeField] float startVolume = 0.8f;
    [SerializeField] AudioClip[] stopClips;
    [Range(0f, 1f)]
    [SerializeField] float stopVolume = 0.9f;

    [Header("Lumieres clignotantes")]
    [SerializeField] Light2D leftLight;
    [SerializeField] Light2D rightLight;
    [SerializeField] float lightOnIntensity = 1f;
    [SerializeField] float lightOffIntensity = 0f;
    [SerializeField] float lightFadeSpeed = 0f;

    [Header("Sons des lumieres")]
    [SerializeField] AudioClip[] leftBlinkClips;
    [SerializeField] AudioClip[] rightBlinkClips;
    [Range(0f, 1f)]
    [SerializeField] float blinkVolume = 0.5f;
    [SerializeField] bool blinkOnlyWhileMoving = true;

    [Header("Variation")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.98f, 1.02f);

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    ElevatorPlatform elevator;
    AudioProxi proximity;

    AudioSource oneShotSource;
    AudioSource motorSource;

    AudioClip lastClip;
    bool wasMoving;
    float motorDelayTimer;
    bool motorPending;

    float leftTargetIntensity;
    float rightTargetIntensity;

    void Awake()
    {
        elevator = GetComponent<ElevatorPlatform>();
        proximity = GetComponent<AudioProxi>();

        oneShotSource = CreateSource(sfxGroup, sfxPriority, false);

        if (motorLoop != null)
        {
            motorSource = CreateSource(loopGroup, loopPriority, true);
            motorSource.clip = motorLoop;
        }
    }

    void Start()
    {
        if (loopGroup == null && motorLoop != null)
            Debug.LogError($"[ElevatorAudio] '{name}' : Loop Group non assigné, la boucle ne passera pas par le mixer.", this);

        if (sfxGroup == null)
            Debug.LogError($"[ElevatorAudio] '{name}' : Sfx Group non assigné.", this);

        if (motorSource != null)
        {
            motorSource.Play();
            motorSource.time = Random.Range(0f, motorLoop.length);
        }

        BlinkOff();
        ApplyLightsInstant();
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

    void Update()
    {
        bool muted = muteWhilePaused && Time.timeScale <= 0f;
        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;

        UpdateTransitions(muted);
        UpdateMotor(muted, attenuation);
        UpdateLights();
    }

    void UpdateTransitions(bool muted)
    {
        bool moving = elevator.IsMoving;

        if (moving && !wasMoving)
        {
            if (!muted) PlayOneShot(startClips, startVolume);
            motorPending = motorStartDelay > 0f;
            motorDelayTimer = motorStartDelay;
        }
        else if (!moving && wasMoving)
        {
            if (!muted) PlayOneShot(stopClips, stopVolume);
            motorPending = false;
            BlinkOff();
        }

        wasMoving = moving;

        if (!motorPending) return;

        motorDelayTimer -= Time.deltaTime;
        if (motorDelayTimer <= 0f) motorPending = false;
    }

    void UpdateMotor(bool muted, float attenuation)
    {
        if (motorSource == null) return;

        bool wanted = elevator.IsMoving && !motorPending && !muted;

        float targetVolume = wanted ? motorVolume * attenuation : 0f;
        float speed = wanted ? motorFadeInSpeed : motorFadeOutSpeed;

        motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVolume, speed * Time.unscaledDeltaTime);

        float targetPitch = elevator.MoveDirection >= 0 ? upPitch : downPitch;
        motorSource.pitch = Mathf.MoveTowards(motorSource.pitch, targetPitch, pitchSmoothing * Time.unscaledDeltaTime);

        if (motorSource.volume <= 0.001f && targetVolume <= 0.001f)
        {
            if (motorSource.isPlaying) motorSource.Pause();
        }
        else if (!motorSource.isPlaying)
        {
            motorSource.UnPause();
        }
    }

    public void BlinkLeft()
    {
        SetSide(true, true);
        SetSide(false, false);

        if (CanBlink()) PlayOneShot(leftBlinkClips, blinkVolume);
    }

    public void BlinkRight()
    {
        SetSide(false, true);
        SetSide(true, false);

        if (CanBlink()) PlayOneShot(rightBlinkClips, blinkVolume);
    }

    public void BlinkOff()
    {
        SetSide(true, false);
        SetSide(false, false);
    }

    bool CanBlink()
    {
        if (muteWhilePaused && Time.timeScale <= 0f) return false;
        if (blinkOnlyWhileMoving && !elevator.IsMoving) return false;

        return true;
    }

    void SetSide(bool left, bool on)
    {
        float intensity = on ? lightOnIntensity : lightOffIntensity;

        if (left) leftTargetIntensity = intensity;
        else rightTargetIntensity = intensity;

        if (lightFadeSpeed <= 0f)
        {
            Light2D light = left ? leftLight : rightLight;
            if (light != null) light.intensity = intensity;
        }
    }

    void UpdateLights()
    {
        if (lightFadeSpeed <= 0f) return;

        if (leftLight != null)
            leftLight.intensity = Mathf.MoveTowards(leftLight.intensity, leftTargetIntensity, lightFadeSpeed * Time.deltaTime);

        if (rightLight != null)
            rightLight.intensity = Mathf.MoveTowards(rightLight.intensity, rightTargetIntensity, lightFadeSpeed * Time.deltaTime);
    }

    void ApplyLightsInstant()
    {
        if (leftLight != null) leftLight.intensity = leftTargetIntensity;
        if (rightLight != null) rightLight.intensity = rightTargetIntensity;
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