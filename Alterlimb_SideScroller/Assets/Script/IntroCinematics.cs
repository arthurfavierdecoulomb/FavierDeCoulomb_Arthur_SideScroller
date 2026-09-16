using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public enum TypewriterSoundMode { PerCharacter, PerWord }

public class IntroCinematic : MonoBehaviour
{
    [System.Serializable]
    public class CinematicScreen
    {
        [TextArea(2, 5)]
        public string text = "";
        public float displayDuration = 3f;
    }

    [Header("Écrans de la cinématique")]
    [SerializeField] List<CinematicScreen> screens = new List<CinematicScreen>();

    [Header("Références UI")]
    [SerializeField] TextMeshProUGUI cinematicText;
    [SerializeField] FadeEffect cinematicFade;
    [SerializeField] FadeEffect menuFade;

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.045f;

    [Header("Son de la machine à écrire")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioClip[] typewriterClips;
    [SerializeField] TypewriterSoundMode typewriterSoundMode = TypewriterSoundMode.PerCharacter;
    [SerializeField] float typewriterMinInterval = 0.06f;
    [Range(0f, 1f)]
    [SerializeField] float typewriterVolume = 0.5f;
    [SerializeField] Vector2 typewriterPitchRange = new Vector2(0.94f, 1.06f);

    [Header("Son du skip")]
    [SerializeField] AudioClip[] skipClips;
    [Range(0f, 1f)]
    [SerializeField] float skipVolume = 0.9f;

    [Header("Timings")]
    [SerializeField] float blackHoldBetweenScreens = 0.4f;
    [SerializeField] float blackHoldBeforeMenu = 0.6f;

    [Header("Passer l'intro")]
    [SerializeField] bool skipEnabled = true;
    [SerializeField] FadeEffect skipHintFade;
    [SerializeField] float skipHintDelay = 2f;
    [SerializeField] bool skipOnAnyKey = true;
    [SerializeField] KeyCode skipKey = KeyCode.Return;

    [Header("Musique")]
    [SerializeField] AudioSource cinematicMusicSource;
    [SerializeField] AudioSource menuMusicSource;
    [SerializeField] float musicFadeOutDuration = 2.5f;

    [Header("Démarrage")]
    [SerializeField] bool playOnStart = true;

    AudioSource oneShotSource;
    AudioClip lastTypewriterClip;
    float lastTypewriterTime = -999f;

    bool cinematicRunning;
    bool endingStarted;
    bool skipAvailable;

    void Awake()
    {
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.priority = 0;
        oneShotSource.ignoreListenerPause = true;
        oneShotSource.outputAudioMixerGroup = sfxGroup;
    }

    void Start()
    {
        if (sfxGroup == null && (typewriterClips.Length > 0 || skipClips.Length > 0))
            Debug.LogError("[IntroCinematic] Sfx Group non assigné : les sons ne passeront pas par le mixer.", this);

        if (playOnStart)
            StartCinematic();
    }

    void Update()
    {
        if (!cinematicRunning || !skipEnabled || !skipAvailable || endingStarted)
            return;

        if (SkipRequested())
            SkipToMenu();
    }

    public void StartCinematic()
    {
        StartCoroutine(CinematicSequence());
    }

    public void SkipToMenu()
    {
        if (endingStarted)
            return;

        endingStarted = true;
        PlayOneShot(skipClips, skipVolume, 1f);
        StopAllCoroutines();
        StartCoroutine(ShowMenuRoutine());
    }

    bool SkipRequested()
    {
        if (skipOnAnyKey && Input.anyKeyDown)
            return true;

        return Input.GetKeyDown(skipKey);
    }

    IEnumerator CinematicSequence()
    {
        cinematicRunning = true;
        endingStarted = false;
        skipAvailable = false;

        if (menuFade != null)
            menuFade.gameObject.SetActive(false);

        if (skipHintFade != null)
        {
            skipHintFade.gameObject.SetActive(skipEnabled);
            skipHintFade.HideInstantly();
        }

        if (cinematicMusicSource != null && cinematicMusicSource.clip != null)
        {
            cinematicMusicSource.loop = true;
            cinematicMusicSource.Play();
        }

        if (skipEnabled)
            StartCoroutine(ShowSkipHintRoutine());

        foreach (CinematicScreen screen in screens)
        {
            if (cinematicText != null)
            {
                cinematicText.text = "";
                cinematicText.maxVisibleCharacters = 0;
            }

            if (cinematicFade != null)
                yield return StartCoroutine(cinematicFade.FadeInRoutine());

            yield return StartCoroutine(TypewriterRoutine(screen.text));

            yield return new WaitForSeconds(screen.displayDuration);

            if (cinematicFade != null)
                yield return StartCoroutine(cinematicFade.FadeOutRoutine());

            yield return new WaitForSeconds(blackHoldBetweenScreens);
        }

        endingStarted = true;
        yield return StartCoroutine(ShowMenuRoutine());
    }

    IEnumerator ShowSkipHintRoutine()
    {
        yield return new WaitForSeconds(skipHintDelay);

        skipAvailable = true;

        if (!endingStarted && skipHintFade != null)
            yield return StartCoroutine(skipHintFade.FadeInRoutine());
    }

    IEnumerator ShowMenuRoutine()
    {
        cinematicRunning = false;

        if (skipHintFade != null && skipHintFade.IsVisible)
            skipHintFade.FadeOut();

        if (cinematicFade != null && cinematicFade.IsVisible)
            yield return StartCoroutine(cinematicFade.FadeOutRoutine());

        if (cinematicText != null)
            cinematicText.text = "";

        yield return new WaitForSeconds(blackHoldBeforeMenu);

        if (cinematicMusicSource != null && cinematicMusicSource.isPlaying)
            StartCoroutine(FadeOutMusic(cinematicMusicSource, musicFadeOutDuration));

        if (menuMusicSource != null && menuMusicSource.clip != null)
        {
            menuMusicSource.loop = true;
            menuMusicSource.Play();
        }

        if (menuFade != null)
        {
            menuFade.gameObject.SetActive(true);
            menuFade.HideInstantly();
            yield return StartCoroutine(menuFade.FadeInRoutine());
        }
    }

    IEnumerator TypewriterRoutine(string fullText)
    {
        if (cinematicText == null)
            yield break;

        cinematicText.text = fullText;
        cinematicText.maxVisibleCharacters = 0;
        cinematicText.ForceMeshUpdate();

        TMP_TextInfo info = cinematicText.textInfo;
        int total = info.characterCount;

        char previous = ' ';

        for (int i = 0; i < total; i++)
        {
            cinematicText.maxVisibleCharacters = i + 1;

            char current = info.characterInfo[i].character;

            if (ShouldClick(current, previous))
                PlayTypewriterClick();

            previous = current;

            yield return new WaitForSeconds(typewriterDelay);
        }
    }

    bool ShouldClick(char current, char previous)
    {
        if (char.IsWhiteSpace(current)) return false;

        if (typewriterSoundMode == TypewriterSoundMode.PerWord)
            return char.IsWhiteSpace(previous);

        return Time.time - lastTypewriterTime >= typewriterMinInterval;
    }

    void PlayTypewriterClick()
    {
        lastTypewriterTime = Time.time;
        PlayOneShot(typewriterClips, typewriterVolume, Random.Range(typewriterPitchRange.x, typewriterPitchRange.y));
    }

    void PlayOneShot(AudioClip[] clips, float volume, float pitch)
    {
        if (oneShotSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastTypewriterClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastTypewriterClip = clip;
        oneShotSource.pitch = pitch;
        oneShotSource.PlayOneShot(clip, volume);
    }

    IEnumerator FadeOutMusic(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        source.volume = startVolume;
    }
}