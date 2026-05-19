using UnityEngine;
using System.Collections;

/// <summary>
/// Lecteur de la musique de fond des niveaux.
/// 
/// Possède son PROPRE AudioSource, séparé de celui des bruitages du
/// LevelTransitionManager — pour que la musique et les bruitages ne se
/// coupent jamais l'un l'autre.
/// 
/// Deux opérations :
///   - FadeOut(durée)    : estompe la musique actuelle jusqu'au silence.
///   - PlayMusic(clip)   : lance une nouvelle musique en boucle, avec un
///                         léger fondu d'entrée.
/// 
/// Accès global via LevelMusicPlayer.Instance.
/// 
/// Setup :
///   - Un GameObject permanent de la scène (ex: "LevelMusicPlayer").
///   - Un composant AudioSource dessus (Play On Awake décoché, Loop coché).
///   - Ce script, avec l'AudioSource assigné.
/// </summary>
public class LevelMusicPlayer : MonoBehaviour
{
    public static LevelMusicPlayer Instance { get; private set; }

    [Header("Audio")]
    [Tooltip("AudioSource dédié à la musique (séparé des bruitages)")]
    [SerializeField] AudioSource musicSource;

    [Header("Volume")]
    [Tooltip("Volume cible de la musique quand elle joue")]
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.6f;

    [Header("Fondus")]
    [Tooltip("Durée du fondu d'entrée quand une nouvelle musique démarre")]
    [SerializeField] float defaultFadeInDuration = 1.0f;

    Coroutine fadeRoutine;

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
        }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Estompe progressivement la musique en cours jusqu'au silence,
    /// puis arrête la lecture.
    /// </summary>
    public void FadeOut(float duration)
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    /// <summary>
    /// Lance une nouvelle musique en boucle, avec un fondu d'entrée.
    /// Si un clip joue déjà, il est remplacé.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        PlayMusic(clip, defaultFadeInDuration);
    }

    /// <summary>Lance une nouvelle musique avec une durée de fondu d'entrée précise.</summary>
    public void PlayMusic(AudioClip clip, float fadeInDuration)
    {
        if (musicSource == null) return;
        if (clip == null)
        {
            Debug.LogWarning("[LevelMusicPlayer] PlayMusic appelé avec un clip null.");
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(PlayMusicRoutine(clip, fadeInDuration));
    }

    /// <summary>Coupe la musique immédiatement, sans fondu.</summary>
    public void StopImmediate()
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        musicSource.Stop();
        musicSource.volume = 0f;
    }

    // ════════════════════════════════════════════════════════════
    //  Coroutines
    // ════════════════════════════════════════════════════════════

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
        // Démarre le nouveau clip à volume nul
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        // Fondu d'entrée jusqu'au volume cible
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