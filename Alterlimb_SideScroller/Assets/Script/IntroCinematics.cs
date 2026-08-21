using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

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

    [Header("Timings")]
    [SerializeField] float blackHoldBetweenScreens = 0.4f;
    [SerializeField] float blackHoldBeforeMenu = 0.6f;

    [Header("Passer l'intro")]
    [SerializeField] bool skipEnabled = true;
    [SerializeField] FadeEffect skipHintFade;
    [SerializeField] float skipHintDelay = 2f;
    [SerializeField] bool skipOnAnyKey = true;
    [SerializeField] KeyCode skipKey = KeyCode.Escape;

    [Header("Musique")]
    [SerializeField] AudioSource cinematicMusicSource;
    [SerializeField] AudioSource menuMusicSource;
    [SerializeField] float musicFadeOutDuration = 2.5f;

    [Header("Démarrage")]
    [SerializeField] bool playOnStart = true;

    bool cinematicRunning;
    bool endingStarted;
    bool skipAvailable;

    void Start()
    {
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
                cinematicText.text = "";

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
        if (cinematicText != null)
            cinematicText.text = "";

        foreach (char c in fullText)
        {
            if (cinematicText != null)
                cinematicText.text += c;

            yield return new WaitForSeconds(typewriterDelay);
        }
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