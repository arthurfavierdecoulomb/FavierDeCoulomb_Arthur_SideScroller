using UnityEngine;

public class FactoryLightAudio : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] FactoryLight factoryLight;
    [SerializeField] AudioSource buzzSource;
    [SerializeField] SfxEmitter sfx;

    [Header("Buzz")]
    [SerializeField] AudioClip buzzLoop;
    [Range(0f, 1f)]
    [SerializeField] float minVolume = 0.15f;
    [Range(0f, 1f)]
    [SerializeField] float maxVolume = 0.6f;
    [SerializeField] bool volumeFollowsIntensity = true;
    [SerializeField] float volumeSmoothing = 20f;

    [Header("Depitch")]
    [SerializeField] float minPitch = 0.65f;
    [SerializeField] float maxPitch = 1.05f;
    [SerializeField] float pitchSmoothing = 25f;

    [Header("Clignotement")]
    [SerializeField] AudioClip[] igniteClips;
    [SerializeField] AudioClip[] extinguishClips;
    [Range(0f, 1f)]
    [SerializeField] float flickerClipVolume = 0.6f;

    [Header("Lampe morte")]
    [SerializeField] bool silentWhenDead = true;

    [Header("Pause")]
    [SerializeField] bool muteWhilePaused = true;

    AudioProxi proximity;
    bool wasFlickerOn = true;
    bool initialised;

    void Awake()
    {
        if (factoryLight == null) factoryLight = GetComponent<FactoryLight>();
        if (sfx == null) sfx = GetComponent<SfxEmitter>();

        proximity = GetComponent<AudioProxi>();
    }

    void Start()
    {
        if (factoryLight == null)
        {
            Debug.LogError($"[FactoryLightAudio] '{name}' ne trouve aucun FactoryLight.", this);
            enabled = false;
            return;
        }

        if (silentWhenDead && factoryLight.IsDead)
        {
            if (buzzSource != null) buzzSource.Stop();
            enabled = false;
            return;
        }

        if (buzzSource == null)
        {
            Debug.LogError($"[FactoryLightAudio] '{name}' n'a pas de Buzz Source assignée.", this);
            enabled = false;
            return;
        }

        if (buzzSource.outputAudioMixerGroup == null)
            Debug.LogError($"[FactoryLightAudio] '{name}' : le champ Output de la Buzz Source est vide, le son ne passera pas par le mixer.", this);

        buzzSource.clip = buzzLoop;
        buzzSource.loop = true;
        buzzSource.playOnAwake = false;
        buzzSource.volume = 0f;

        if (buzzLoop != null)
        {
            buzzSource.Play();
            buzzSource.time = Random.Range(0f, buzzLoop.length);
        }

        wasFlickerOn = factoryLight.IsFlickerOn;
        initialised = true;
    }

    void Update()
    {
        if (!initialised) return;

        bool flickerOn = factoryLight.IsFlickerOn;

        if (flickerOn != wasFlickerOn && sfx != null && !IsMuted)
        {
            if (flickerOn) sfx.Play(igniteClips, flickerClipVolume);
            else sfx.Play(extinguishClips, flickerClipVolume);
        }

        wasFlickerOn = flickerOn;

        UpdateBuzz();
    }

    void UpdateBuzz()
    {
        if (buzzLoop == null) return;

        float normalized = factoryLight.NormalizedIntensity;

        float targetVolume;
        if (IsMuted)
            targetVolume = 0f;
        else if (volumeFollowsIntensity)
            targetVolume = Mathf.Lerp(minVolume, maxVolume, normalized);
        else
            targetVolume = maxVolume;

        float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalized);

        if (proximity != null)
            targetVolume *= proximity.GetAttenuation();

        buzzSource.volume = Mathf.MoveTowards(buzzSource.volume, targetVolume, volumeSmoothing * Time.unscaledDeltaTime);
        buzzSource.pitch = Mathf.MoveTowards(buzzSource.pitch, targetPitch, pitchSmoothing * Time.unscaledDeltaTime);

        if (buzzSource.volume <= 0.001f && targetVolume <= 0.001f)
        {
            if (buzzSource.isPlaying) buzzSource.Pause();
        }
        else if (!buzzSource.isPlaying)
        {
            buzzSource.UnPause();
        }
    }

    bool IsMuted => muteWhilePaused && Time.timeScale <= 0f;
}