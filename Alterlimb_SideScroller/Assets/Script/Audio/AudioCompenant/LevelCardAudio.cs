using UnityEngine;
using UnityEngine.Audio;

public class LevelCardAudio : MonoBehaviour
{
    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup humGroup;
    [Range(0, 256)]
    [SerializeField] int priority = 0;

    [Header("Hum du scanline")]
    [SerializeField] AudioClip normalHum;
    [SerializeField] AudioClip oxiHum;
    [Range(0f, 1f)]
    [SerializeField] float normalHumVolume = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] float oxiHumVolume = 0.6f;
    [SerializeField] float humFadeInSpeed = 1.2f;
    [SerializeField] float humFadeOutSpeed = 2f;
    [SerializeField] float humSwitchSpeed = 5f;

    [Header("Ouverture de la carte")]
    [SerializeField] AudioClip[] cardOpenClips;
    [Range(0f, 1f)]
    [SerializeField] float cardOpenVolume = 0.7f;

    [Header("Bleeps — normal")]
    [SerializeField] AudioClip[] titleBleepClips;
    [SerializeField] AudioClip[] textBleepClips;

    [Header("Bleeps — Oxi-O")]
    [SerializeField] AudioClip[] oxiTitleBleepClips;
    [SerializeField] AudioClip[] oxiTextBleepClips;

    [Header("Bleeps — reglages")]
    [Range(0f, 1f)]
    [SerializeField] float bleepVolume = 0.4f;
    [SerializeField] float titleBleepMinInterval = 0.04f;
    [SerializeField] float textBleepMinInterval = 0.05f;
    [SerializeField] Vector2 bleepPitchRange = new Vector2(0.94f, 1.06f);

    [Header("Prise de controle Oxi-O")]
    [SerializeField] AudioClip[] takeoverStompClips;
    [Range(0f, 1f)]
    [SerializeField] float takeoverVolume = 1f;

    [Header("Invite")]
    [SerializeField] AudioClip[] promptSwooshClips;
    [SerializeField] AudioClip[] continueClips;
    [Range(0f, 1f)]
    [SerializeField] float promptVolume = 0.8f;

    [Header("Decompte")]
    [SerializeField] AudioClip[] countdownTickClips;
    [SerializeField] AudioClip[] countdownFinalClips;
    [Range(0f, 1f)]
    [SerializeField] float countdownVolume = 0.5f;
    [SerializeField] int tickFromSecond = 10;
    [SerializeField] int finalFromSecond = 3;

    AudioSource oneShotSource;
    AudioSource normalHumSource;
    AudioSource oxiHumSource;

    AudioClip lastClip;
    float lastTitleBleepTime = -999f;
    float lastTextBleepTime = -999f;

    bool humActive;
    bool humOxi;

    void Awake()
    {
        oneShotSource = CreateSource(sfxGroup, false);
        normalHumSource = CreateSource(humGroup, true);
        oxiHumSource = CreateSource(humGroup, true);

        normalHumSource.clip = normalHum;
        oxiHumSource.clip = oxiHum;
    }

    void Start()
    {
        if (sfxGroup == null)
            Debug.LogError("[LevelCardAudio] Sfx Group non assigné : les bruitages ne passeront pas par le mixer.", this);

        if (humGroup == null && (normalHum != null || oxiHum != null))
            Debug.LogError("[LevelCardAudio] Hum Group non assigné : le hum ne passera pas par le mixer.", this);
    }

    AudioSource CreateSource(AudioMixerGroup group, bool looping)
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

    void Update()
    {
        UpdateHumSource(normalHumSource, humActive && !humOxi, normalHumVolume);
        UpdateHumSource(oxiHumSource, humActive && humOxi, oxiHumVolume);
    }

    void UpdateHumSource(AudioSource source, bool wanted, float maxVolume)
    {
        if (source == null || source.clip == null) return;

        float target = wanted ? maxVolume : 0f;
        float speed = humOxi && humActive ? humSwitchSpeed : (wanted ? humFadeInSpeed : humFadeOutSpeed);

        source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.unscaledDeltaTime);

        if (source.volume <= 0.001f && target <= 0.001f)
        {
            if (source.isPlaying) source.Stop();
        }
        else if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void StartHum()
    {
        humActive = true;
        humOxi = false;
    }

    public void SwitchToOxiHum()
    {
        humOxi = true;
    }

    public void StopHum()
    {
        humActive = false;
    }

    public void PlayCardOpen()
    {
        PlayClips(cardOpenClips, cardOpenVolume);
    }

    public void PlayTitleBleep(bool oxi)
    {
        if (Time.unscaledTime - lastTitleBleepTime < titleBleepMinInterval) return;
        lastTitleBleepTime = Time.unscaledTime;

        PlayClips(oxi ? oxiTitleBleepClips : titleBleepClips, bleepVolume);
    }

    public void PlayTextBleep(bool oxi)
    {
        if (Time.unscaledTime - lastTextBleepTime < textBleepMinInterval) return;
        lastTextBleepTime = Time.unscaledTime;

        PlayClips(oxi ? oxiTextBleepClips : textBleepClips, bleepVolume);
    }

    public void PlayTakeoverStomp()
    {
        PlayClips(takeoverStompClips, takeoverVolume);
    }

    public void PlayPromptSwoosh()
    {
        PlayClips(promptSwooshClips, promptVolume);
    }

    public void PlayContinue()
    {
        PlayClips(continueClips, promptVolume);
    }

    public void PlayCountdownTick(int secondsRemaining)
    {
        if (secondsRemaining > tickFromSecond) return;

        bool final = secondsRemaining <= finalFromSecond
                  && countdownFinalClips != null
                  && countdownFinalClips.Length > 0;

        PlayClips(final ? countdownFinalClips : countdownTickClips, countdownVolume);
    }

    void PlayClips(AudioClip[] clips, float volume)
    {
        if (oneShotSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastClip = clip;
        oneShotSource.pitch = Random.Range(bleepPitchRange.x, bleepPitchRange.y);
        oneShotSource.PlayOneShot(clip, volume);
    }
}