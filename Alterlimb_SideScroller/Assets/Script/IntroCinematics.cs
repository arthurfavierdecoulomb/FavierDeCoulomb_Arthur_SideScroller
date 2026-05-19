using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Cinématique d'introduction du jeu (façon Undertale).
/// 
/// Défilement 100% AUTOMATIQUE — aucune touche. Enchaîne une liste d'écrans
/// de texte. Pour chaque écran :
///   1. L'écran apparaît en flicker.
///   2. Le texte s'écrit avec un effet machine à écrire.
///   3. L'écran reste affiché pendant son 'displayDuration' propre.
///   4. L'écran disparaît en flicker.
/// 
/// Une fois tous les écrans passés, le menu apparaît (flicker).
/// 
/// MUSIQUE : deux AudioSource séparés.
///   - musique de cinématique : joue pendant la cinématique, fondu de sortie
///     quand le menu apparaît.
///   - musique du menu : démarre quand le menu apparaît.
///     (Son AudioSource doit avoir 'Play On Awake' DÉCOCHÉ.)
/// </summary>
public class IntroCinematic : MonoBehaviour
{
    [System.Serializable]
    public class CinematicScreen
    {
        [TextArea(2, 5)]
        [Tooltip("Le texte affiché sur cet écran")]
        public string text = "";
        [Tooltip("Temps d'affichage de l'écran APRÈS que le texte soit entièrement tapé")]
        public float displayDuration = 3f;
    }

    [Header("Écrans de la cinématique")]
    [SerializeField] List<CinematicScreen> screens = new List<CinematicScreen>();

    [Header("Références UI")]
    [Tooltip("Le TextMeshPro où s'affiche le texte de la cinématique")]
    [SerializeField] TextMeshProUGUI cinematicText;
    [Tooltip("FlickerEffect du panneau de cinématique (le bloc texte)")]
    [SerializeField] FlickerEffect cinematicFlicker;
    [Tooltip("FlickerEffect du panneau du MENU (affiché à la fin de la cinématique)")]
    [SerializeField] FlickerEffect menuFlicker;

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.045f;

    [Header("Timings")]
    [Tooltip("Pause sur fond noir entre deux écrans")]
    [SerializeField] float blackHoldBetweenScreens = 0.4f;
    [Tooltip("Pause sur fond noir avant l'apparition du menu")]
    [SerializeField] float blackHoldBeforeMenu = 0.6f;

    [Header("Musique")]
    [Tooltip("AudioSource dédié à la musique de la cinématique")]
    [SerializeField] AudioSource cinematicMusicSource;
    [Tooltip("AudioSource dédié à la musique du menu (Play On Awake DÉCOCHÉ !)")]
    [SerializeField] AudioSource menuMusicSource;
    [Tooltip("Durée du fondu de sortie de la musique de cinématique")]
    [SerializeField] float musicFadeOutDuration = 2.5f;

    [Header("Démarrage")]
    [Tooltip("Si coché, la cinématique démarre automatiquement au lancement de la scène")]
    [SerializeField] bool playOnStart = true;

    void Start()
    {
        if (playOnStart)
            StartCinematic();
    }

    /// <summary>Lance la cinématique depuis le début.</summary>
    public void StartCinematic()
    {
        StartCoroutine(CinematicSequence());
    }

    IEnumerator CinematicSequence()
    {
        // Le menu est caché pendant toute la cinématique
        if (menuFlicker != null)
            menuFlicker.gameObject.SetActive(false);

        // ── Démarre la musique de la cinématique ──
        if (cinematicMusicSource != null && cinematicMusicSource.clip != null)
        {
            cinematicMusicSource.loop = true;
            cinematicMusicSource.Play();
        }

        // ── Parcours de chaque écran ──
        foreach (CinematicScreen screen in screens)
        {
            if (cinematicText != null)
                cinematicText.text = "";

            // 1. Flicker in du panneau de cinématique
            if (cinematicFlicker != null)
                yield return StartCoroutine(cinematicFlicker.FlickerInRoutine());

            // 2. Machine à écrire
            yield return StartCoroutine(TypewriterRoutine(screen.text));

            // 3. L'écran reste affiché pendant son délai propre
            yield return new WaitForSeconds(screen.displayDuration);

            // 4. Flicker out du panneau
            if (cinematicFlicker != null)
                yield return StartCoroutine(cinematicFlicker.FlickerOutRoutine());

            yield return new WaitForSeconds(blackHoldBetweenScreens);
        }

        // ── Fin de la cinématique ──
        yield return new WaitForSeconds(blackHoldBeforeMenu);

        // ── Bascule musique : fondu de sortie cinématique + démarrage menu ──
        if (cinematicMusicSource != null)
            StartCoroutine(FadeOutMusic(cinematicMusicSource, musicFadeOutDuration));

        if (menuMusicSource != null && menuMusicSource.clip != null)
        {
            menuMusicSource.loop = true;
            menuMusicSource.Play();
        }

        // ── Apparition du menu en flicker ──
        if (menuFlicker != null)
        {
            menuFlicker.gameObject.SetActive(true);
            yield return StartCoroutine(menuFlicker.FlickerInRoutine());
        }
    }

    // ── Machine à écrire ──
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

    /// <summary>Estompe progressivement une musique jusqu'au silence, puis l'arrête.</summary>
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
    }
}