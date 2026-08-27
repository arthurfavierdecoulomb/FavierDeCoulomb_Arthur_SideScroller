using System.Collections;
using UnityEngine;

public class PauseMusicPlayer : MonoBehaviour
{
    public static PauseMusicPlayer Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] AudioSource pauseSource;
    [SerializeField] AudioClip pauseClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] float pauseMusicVolume = 0.7f;

    [Header("Fondus")]
    [SerializeField] float fadeInDuration = 0.4f;
    [SerializeField] float fadeOutDuration = 0.6f;

    [Header("Lecture")]
    [SerializeField] bool restartFromStart = true;

    [Header("Musique du niveau")]
    [SerializeField] bool muffleLevelMusic = true;

    Coroutine fadeRoutine;
    bool active;

    public bool IsActive => active;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (pauseSource == null) pauseSource = GetComponent<AudioSource>();

        if (pauseSource != null)
        {
            pauseSource.loop = true;
            pauseSource.playOnAwake = false;
            pauseSource.volume = 0f;
        }
    }

    public void EnterPause()
    {
        if (active) return;
        active = true;

        if (muffleLevelMusic && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.MuffleMusic();

        if (pauseSource == null || pauseClip == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    public void ExitPause()
    {
        if (!active) return;
        active = false;

        if (muffleLevelMusic && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.UnmuffleMusic();

        if (pauseSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    public void StopImmediate()
    {
        active = false;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = null;

        if (pauseSource == null) return;

        pauseSource.Stop();
        pauseSource.volume = 0f;
    }

    IEnumerator FadeInRoutine()
    {
        if (restartFromStart || !pauseSource.isPlaying)
        {
            pauseSource.clip = pauseClip;
            pauseSource.loop = true;
            if (restartFromStart) pauseSource.time = 0f;
            pauseSource.Play();
        }

        float start = pauseSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            pauseSource.volume = Mathf.Lerp(start, pauseMusicVolume, Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }

        pauseSource.volume = pauseMusicVolume;
        fadeRoutine = null;
    }

    IEnumerator FadeOutRoutine()
    {
        float start = pauseSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            pauseSource.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }

        pauseSource.volume = 0f;
        pauseSource.Stop();
        fadeRoutine = null;
    }
}