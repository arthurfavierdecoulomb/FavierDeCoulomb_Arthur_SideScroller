using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Gère les transitions entre niveaux ET l'intro de démarrage du jeu.
/// 
/// Deux modes d'utilisation :
///   1. StartTransition(level, autoRunDir, runDistance)
///      → utilisé par les TransitionZone en fin de niveau (course auto + écran + téléport + course auto)
/// 
///   2. StartIntro(level)
///      → utilisé au démarrage du jeu pour afficher le titre du premier niveau
///        sans course automatique (le joueur garde le contrôle immédiatement après)
/// 
/// Effets visuels :
///   - Fond noir + CRT pendant la phase d'écran
///   - Titre et description apparaissent en "flicker in" (clignotement avant stabilisation)
///   - Idem en "flicker out" pour disparaître
///   - Pas de glitch de lettres (texte propre)
/// </summary>
public class LevelTransitionManager : MonoBehaviour
{
    public static LevelTransitionManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Références UI")]
    [SerializeField] GameObject transitionOverlay;
    [SerializeField] GameObject blackBackground;
    [SerializeField] GameObject crtEffect;
    [SerializeField] TextMeshProUGUI levelTitle;
    [SerializeField] TextMeshProUGUI levelDescription;
    [SerializeField] GameObject gameUICanvas;

    [Header("Références joueur & caméra")]
    [SerializeField] CharaController player;
    [SerializeField] Camera mainCamera;

    [Header("Course automatique (transitions entre niveaux)")]
    [Tooltip("Sécurité : si le joueur n'atteint pas la distance de flicker en X secondes, on déclenche quand même")]
    [SerializeField] float autoRunSafetyTimeout = 5f;
    [Tooltip("Tolérance d'arrivée pour la course de sortie (distance X)")]
    [SerializeField] float arrivalTolerance = 0.3f;

    [Header("Timings — Découpage de la séquence de 5s")]
    [Tooltip("Durée du flicker in (apparition clignotante) du titre")]
    [SerializeField] float titleFlickerInDuration = 0.5f;
    [Tooltip("Délai entre la stabilisation du titre et le début du flicker in de la description")]
    [SerializeField] float delayBeforeDescription = 0.5f;
    [Tooltip("Durée du flicker in (apparition clignotante) de la description")]
    [SerializeField] float descriptionFlickerInDuration = 0.5f;
    [Tooltip("Temps de lecture du titre + description affichés stables")]
    [SerializeField] float readingDuration = 2f;
    [Tooltip("Durée du flicker out de la description")]
    [SerializeField] float descriptionFlickerOutDuration = 0.5f;
    [Tooltip("Durée du flicker out du titre")]
    [SerializeField] float titleFlickerOutDuration = 0.5f;
    [Tooltip("Pause finale (écran noir) avant fin de la séquence")]
    [SerializeField] float endBlackHold = 0.5f;

    [Header("Flicker (apparition / disparition clignotante)")]
    [Tooltip("Intervalle MIN entre chaque clignotement")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [Tooltip("Intervalle MAX entre chaque clignotement")]
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Header("Audio (optionnel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip transitionSound;

    // ════════════════════════════════════════════════════════════
    //  État
    // ════════════════════════════════════════════════════════════

    bool isTransitioning;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // État initial : tout caché
        if (transitionOverlay != null) transitionOverlay.SetActive(true);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (crtEffect != null) crtEffect.SetActive(false);
        if (levelTitle != null) { levelTitle.text = ""; levelTitle.gameObject.SetActive(false); }
        if (levelDescription != null) { levelDescription.text = ""; levelDescription.gameObject.SetActive(false); }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Transition complète entre deux niveaux :
    /// course auto → écran de titre → téléport → course auto → reprise du contrôle.
    /// </summary>
    public void StartTransition(LevelData target, float autoRunDir, float runDistance)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[LevelTransitionManager] Transition déjà en cours, ignorée.");
            return;
        }
        if (target == null)
        {
            Debug.LogError("[LevelTransitionManager] LevelData null !");
            return;
        }
        if (player == null)
        {
            Debug.LogError("[LevelTransitionManager] Référence joueur manquante !");
            return;
        }

        StartCoroutine(FullTransitionSequence(target, autoRunDir, runDistance));
    }

    /// <summary>
    /// Intro de démarrage du jeu : affiche juste l'écran de titre du niveau,
    /// puis rend le contrôle au joueur sans course automatique.
    /// À appeler au lancement de la scène (depuis un GameStarter par exemple).
    /// </summary>
    public void StartIntro(LevelData target)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[LevelTransitionManager] Transition déjà en cours, ignorée.");
            return;
        }
        if (target == null)
        {
            Debug.LogError("[LevelTransitionManager] LevelData null !");
            return;
        }

        StartCoroutine(IntroSequence(target));
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence complète (transition entre niveaux)
    // ════════════════════════════════════════════════════════════

    IEnumerator FullTransitionSequence(LevelData target, float autoRunDir, float runDistance)
    {
        isTransitioning = true;

        // ── Phase 1 : Verrouillage joueur ───────────────────────
        player.SetInvincible(true);
        player.SetAutoRun(true, autoRunDir);

        // ── Phase 2 : Course automatique d'entrée ───────────────
        float startX = player.transform.position.x;
        float targetX = startX + (runDistance * autoRunDir);
        float elapsed = 0f;

        while (elapsed < autoRunSafetyTimeout)
        {
            float currentX = player.transform.position.x;
            bool reached = (autoRunDir > 0) ? currentX >= targetX : currentX <= targetX;
            if (reached) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Phase 3 : Activation de l'écran noir + CRT ──────────
        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound, 0.6f);

        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // ── Phase 4 : Séquence titre + description (5s) ─────────
        yield return StartCoroutine(TitleSequence(target));

        // ── Phase 5 : Téléportation pendant le noir ─────────────
        player.SetAutoRun(false, autoRunDir);
        player.TeleportTo(target.spawnPosition);

        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(
                target.spawnPosition.x,
                target.spawnPosition.y,
                camPos.z
            );
        }

        if (target.ambientMusic != null && audioSource != null)
        {
            audioSource.clip = target.ambientMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // ── Phase 6 : Désactivation écran noir + CRT ────────────
        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        // ── Phase 7 : Course automatique de sortie ──────────────
        float exitDir = Mathf.Sign(target.autoRunEndPosition.x - target.spawnPosition.x);
        if (Mathf.Abs(exitDir) < 0.01f) exitDir = 1f;

        player.SetAutoRun(true, exitDir);

        elapsed = 0f;
        while (elapsed < autoRunSafetyTimeout)
        {
            float currentX = player.transform.position.x;
            float remainingX = target.autoRunEndPosition.x - currentX;

            if (Mathf.Abs(remainingX) <= arrivalTolerance ||
                Mathf.Sign(remainingX) != exitDir)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Phase 8 : Rendu du contrôle ─────────────────────────
        player.SetAutoRun(false, 0f);
        player.SetInvincible(false);

        isTransitioning = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence d'intro (démarrage du jeu sur l'Usine)
    // ════════════════════════════════════════════════════════════

    IEnumerator IntroSequence(LevelData target)
    {
        isTransitioning = true;

        // Le joueur est verrouillé sans course (juste figé) pendant l'intro
        if (player != null)
        {
            player.SetInvincible(true);
            player.SetAutoRun(false, 0f);
        }

        // Téléport au spawn dès le départ (au cas où le joueur n'est pas déjà au bon endroit)
        if (player != null) player.TeleportTo(target.spawnPosition);
        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(
                target.spawnPosition.x,
                target.spawnPosition.y,
                camPos.z
            );
        }

        // Activation de l'écran noir + CRT
        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound, 0.6f);

        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // Musique du niveau (si configurée)
        if (target.ambientMusic != null && audioSource != null)
        {
            audioSource.clip = target.ambientMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Séquence titre + description (5s)
        yield return StartCoroutine(TitleSequence(target));

        // Désactivation écran noir + CRT
        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        // Rendu du contrôle au joueur (sans course auto)
        if (player != null) player.SetInvincible(false);

        isTransitioning = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence titre + description (5s)
    //  Découpage exact :
    //    0.0s ─ flicker in titre
    //    0.5s ─ titre stable
    //    1.0s ─ flicker in description
    //    1.5s ─ description stable
    //    1.5s → 3.5s ─ tout stable (lecture)
    //    3.5s ─ flicker out description
    //    4.0s ─ flicker out titre
    //    4.5s ─ écran noir
    //    5.0s ─ fin
    // ════════════════════════════════════════════════════════════

    IEnumerator TitleSequence(LevelData target)
    {
        if (levelTitle == null) yield break;

        // Setup texte
        levelTitle.text = target.levelName;
        if (levelDescription != null) levelDescription.text = target.levelDescription;

        // ── Flicker IN titre ────────────────────────────────────
        yield return StartCoroutine(FlickerObjectIn(levelTitle.gameObject, titleFlickerInDuration));

        // Délai avant la description
        yield return new WaitForSeconds(delayBeforeDescription);

        // ── Flicker IN description ──────────────────────────────
        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectIn(levelDescription.gameObject, descriptionFlickerInDuration));

        // ── Lecture (tout stable) ───────────────────────────────
        yield return new WaitForSeconds(readingDuration);

        // ── Flicker OUT description ─────────────────────────────
        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectOut(levelDescription.gameObject, descriptionFlickerOutDuration));

        // ── Flicker OUT titre ───────────────────────────────────
        yield return StartCoroutine(FlickerObjectOut(levelTitle.gameObject, titleFlickerOutDuration));

        // ── Pause finale écran noir ─────────────────────────────
        yield return new WaitForSeconds(endBlackHold);
    }

    // ════════════════════════════════════════════════════════════
    //  Flicker IN / OUT — clignotement à l'apparition/disparition
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Fait apparaître un GameObject avec un effet de flicker (clignotements rapides),
    /// puis le laisse stable visible à la fin.
    /// </summary>
    IEnumerator FlickerObjectIn(GameObject go, float duration)
    {
        if (go == null) yield break;

        float elapsed = 0f;
        bool visible = false;

        while (elapsed < duration)
        {
            visible = !visible;
            go.SetActive(visible);

            if (audioSource != null && flickerSound != null && visible)
                audioSource.PlayOneShot(flickerSound, 0.3f);

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // État final : visible et stable
        go.SetActive(true);
    }

    /// <summary>
    /// Fait disparaître un GameObject avec un effet de flicker (clignotements rapides),
    /// puis le laisse caché à la fin.
    /// </summary>
    IEnumerator FlickerObjectOut(GameObject go, float duration)
    {
        if (go == null) yield break;

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            go.SetActive(visible);

            if (audioSource != null && flickerSound != null && !visible)
                audioSource.PlayOneShot(flickerSound, 0.3f);

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // État final : caché et stable
        go.SetActive(false);
    }
}