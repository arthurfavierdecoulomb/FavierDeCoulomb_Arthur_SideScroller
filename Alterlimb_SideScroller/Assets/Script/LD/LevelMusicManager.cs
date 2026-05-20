using UnityEngine;
using System.Collections;

/// <summary>
/// Lecteur de la musique de fond des niveaux.
/// 
/// Possède son PROPRE AudioSource, séparé des bruitages.
/// 
/// Opérations :
///   - FadeOut(durée)   : estompe la musique jusqu'au silence.
///   - PlayMusic(clip)  : lance une musique en boucle avec fondu d'entrée.
///   - MuffleMusic()    : "étouffe" la musique (pitch grave + volume bas) sans
///                        l'arrêter — utilisé à la mort du joueur.
///   - UnmuffleMusic()  : ramène pitch et volume à la normale — au respawn.
/// 
/// Accès global via LevelMusicPlayer.Instance.
/// </summary>
public class LevelMusicPlayer : MonoBehaviour
{
    public static LevelMusicPlayer Instance { get; private set; }

    [Header("Audio")]
    [Tooltip("AudioSource dédié à la musique (séparé des bruitages)")]
    [SerializeField] AudioSource musicSource;

    [Header("Volume")]
    [Tooltip("Volume cible de la musique quand elle joue normalement")]
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.6f;

    [Header("Fondus (transitions de niveau)")]
    [Tooltip("Durée du fondu d'entrée quand une nouvelle musique démarre")]
    [SerializeField] float defaultFadeInDuration = 1.0f;

    [Header("Effet sourdine (mort du joueur)")]
    [Tooltip("Volume de la musique quand elle est étouffée (sourdine)")]
    [Range(0f, 1f)]
    [SerializeField] float muffledVolume = 0.2f;
    [Tooltip("Pitch de la musique quand elle est étouffée (1 = normal, plus bas = grave/ralenti)")]
    [Range(0.1f, 1f)]
    [SerializeField] float muffledPitch = 0.5f;
    [Tooltip("Durée de la transition vers la sourdine (à la mort)")]
    [SerializeField] float muffleDuration = 0.4f;
    [Tooltip("Durée de la transition de retour à la normale (au respawn)")]
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

    // ════════════════════════════════════════════════════════════
    //  Transitions de niveau
    // ════════════════════════════════════════════════════════════

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
        if (clip == null)
        {
            Debug.LogWarning("[LevelMusicPlayer] PlayMusic appelé avec un clip null.");
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(PlayMusicRoutine(clip, fadeInDuration));
    }

    public void StopImmediate()
    {
        if (musicSource == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        musicSource.Stop();
        musicSource.volume = 0f;
    }

    // ════════════════════════════════════════════════════════════
    //  Effet sourdine (mort / respawn)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// "Étouffe" la musique : le pitch descend (grave/ralenti) et le volume
    /// baisse, en douceur. La musique NE S'ARRÊTE PAS — elle joue en sourdine.
    /// À appeler à la mort du joueur.
    /// </summary>
    public void MuffleMusic()
    {
        if (musicSource == null) return;

        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(muffledVolume, muffledPitch, muffleDuration));
    }

    /// <summary>
    /// Ramène la musique à la normale : pitch et volume reviennent à leurs
    /// valeurs normales, en douceur. À appeler au respawn du joueur.
    /// </summary>
    public void UnmuffleMusic()
    {
        if (musicSource == null) return;

        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(musicVolume, 1f, unmuffleDuration));
    }

    /// <summary>
    /// Transition douce du volume ET du pitch vers des valeurs cibles.
    /// Sert aussi bien à étouffer (Muffle) qu'à rétablir (Unmuffle).
    /// </summary>
    IEnumerator MuffleRoutine(float targetVolume, float targetPitch, float duration)
    {
        float startVolume = musicSource.volume;
        float startPitch = musicSource.pitch;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            musicSource.pitch = Mathf.Lerp(startPitch, targetPitch, t);

            yield return null;
        }

        musicSource.volume = targetVolume;
        musicSource.pitch = targetPitch;
        muffleRoutine = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Coroutines de fondu
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
        // Une nouvelle musique repart toujours à pitch normal
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