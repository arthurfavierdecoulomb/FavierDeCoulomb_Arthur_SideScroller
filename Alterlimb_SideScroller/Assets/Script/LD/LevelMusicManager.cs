using UnityEngine;
using System.Collections;

public class LevelMusicPlayer : MonoBehaviour
{
    public static LevelMusicPlayer Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] AudioSource musicSource;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.6f;

    [Header("Fondus")]
    [SerializeField] float defaultFadeInDuration = 1f;

    [Header("Sourdine")]
    [Range(0f, 1f)]
    [SerializeField] float muffledVolume = 0.2f;
    [Range(0.1f, 1f)]
    [SerializeField] float muffledPitch = 0.5f;
    [SerializeField] float muffleDuration = 0.4f;
    [SerializeField] float unmuffleDuration = 0.6f;

    Coroutine fadeRoutine;
    Coroutine muffleRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource != null)
        {
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.pitch = 1f;
        }
    }

    public void FadeOut(float duration)
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    public void PlayMusic(AudioClip clip)
    {
        PlayMusic(clip, defaultFadeInDuration);
    }

    public void PlayMusic(AudioClip clip, float fadeInDuration)
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (clip == null)
        {
            fadeRoutine = StartCoroutine(FadeOutRoutine(fadeInDuration));
            return;
        }

        fadeRoutine = StartCoroutine(PlayMusicRoutine(clip, fadeInDuration));
    }

    public void StopImmediate()
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        musicSource.Stop();
        musicSource.volume = 0f;
    }

    public void MuffleMusic()
    {
        if (musicSource == null) return;
        if (!musicSource.isPlaying) return;

        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(muffledVolume, muffledPitch, muffleDuration));
    }

    public void UnmuffleMusic()
    {
        if (musicSource == null) return;
        if (!musicSource.isPlaying) return;

        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(musicVolume, 1f, unmuffleDuration));
    }

    IEnumerator MuffleRoutine(float targetVolume, float targetPitch, float duration)
    {
        float startVolume = musicSource.volume;
        float startPitch = musicSource.pitch;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            musicSource.pitch = Mathf.Lerp(startPitch, targetPitch, t);
            yield return null;
        }

        musicSource.volume = targetVolume;
        musicSource.pitch = targetPitch;
        muffleRoutine = null;
    }

    IEnumerator FadeOutRoutine(float duration)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.Stop();
        fadeRoutine = null;
    }

    IEnumerator PlayMusicRoutine(AudioClip clip, float fadeInDuration)
    {
        musicSource.pitch = 1f;
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }

        musicSource.volume = musicVolume;
        fadeRoutine = null;
    }
}