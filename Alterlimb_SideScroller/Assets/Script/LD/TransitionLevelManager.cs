using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Gère les transitions entre niveaux ET l'intro de démarrage du jeu.
/// 
/// Deux modes d'utilisation :
///   1. StartTransition(level, autoRunDir, runDistance)
///      → utilisé par les TransitionZone en fin de niveau
///        (course auto + écran + téléport + course auto)
/// 
///   2. StartIntro(level)
///      → utilisé au démarrage du jeu pour afficher le titre du premier niveau
///        sans course automatique (le joueur garde le contrôle immédiatement après)
/// 
/// Touche "continuer" :
///   - Après le flicker in du titre + description, l'écran reste affiché.
///   - Le joueur doit appuyer sur la touche configurée (par défaut Enter)
///     pour déclencher le flicker out et poursuivre la séquence.
///   - Un timeout de sécurité termine la transition si le joueur ne réagit pas.
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
    [Tooltip("Texte d'invite affiché en bas (ex: 'Appuyez sur Entrée pour continuer'). Optionnel.")]
    [SerializeField] TextMeshProUGUI continuePrompt;
    [SerializeField] GameObject gameUICanvas;

    [Header("Références joueur & caméra")]
    [SerializeField] CharaController player;
    [SerializeField] Camera mainCamera;

    [Header("Course automatique (transitions entre niveaux)")]
    [Tooltip("Sécurité : si le joueur n'atteint pas la distance de flicker en X secondes, on déclenche quand même")]
    [SerializeField] float autoRunSafetyTimeout = 5f;
    [Tooltip("Tolérance d'arrivée pour la course de sortie (distance X)")]
    [SerializeField] float arrivalTolerance = 0.3f;

    [Header("Timings — Apparition du titre & description")]
    [Tooltip("Durée du flicker in (apparition clignotante) du titre")]
    [SerializeField] float titleFlickerInDuration = 0.5f;
    [Tooltip("Délai entre la stabilisation du titre et le début du flicker in de la description")]
    [SerializeField] float delayBeforeDescription = 0.5f;
    [Tooltip("Durée du flicker in (apparition clignotante) de la description")]
    [SerializeField] float descriptionFlickerInDuration = 0.5f;
    [Tooltip("Délai entre l'affichage stable de la description et l'apparition du prompt 'continuer'")]
    [SerializeField] float delayBeforeContinuePrompt = 0.8f;

    [Header("Touche pour continuer")]
    [Tooltip("Touche que le joueur doit presser pour terminer l'écran de titre")]
    [SerializeField] KeyCode continueKey = KeyCode.Return;
    [Tooltip("Sécurité : si le joueur n'appuie pas, on continue automatiquement après X secondes")]
    [SerializeField] float continueTimeout = 30f;

    [Header("Timings — Disparition")]
    [SerializeField] float descriptionFlickerOutDuration = 0.5f;
    [SerializeField] float titleFlickerOutDuration = 0.5f;
    [Tooltip("Pause finale (écran noir) avant fin de la séquence")]
    [SerializeField] float endBlackHold = 0.5f;

    [Header("Flicker (apparition / disparition clignotante)")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    
    [Tooltip("Intervalle (en secondes) du clignotement du texte 'Appuyez sur Entrée'")]
    [SerializeField] float continuePromptBlinkInterval = 0.5f;

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
        if (continuePrompt != null) { continuePrompt.gameObject.SetActive(false); }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

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

        // ── Phase 3 : Activation écran noir + CRT ──────────────
        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound, 0.6f);

        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // ── Phase 4 : Séquence titre + description + attente touche
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
        float exitDir = Mathf.Sign(target.exitRunDirection);
        if (Mathf.Abs(exitDir) < 0.01f) exitDir = 1f; // sécurité si valeur 0

        player.SetAutoRun(true, exitDir);

        elapsed = 0f;
        while (elapsed < autoRunSafetyTimeout)
        {
            float currentX = player.transform.position.x;
            float remainingX = target.autoRunEndPosition.x - currentX;

            // Arrivé si la distance restante est < tolérance, ou si on a dépassé
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
    //  Séquence d'intro (démarrage du jeu)
    // ════════════════════════════════════════════════════════════

    IEnumerator IntroSequence(LevelData target)
    {
        isTransitioning = true;

        if (player != null)
        {
            player.SetInvincible(true);
            player.SetAutoRun(false, 0f);
            player.TeleportTo(target.spawnPosition);
        }
        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(
                target.spawnPosition.x,
                target.spawnPosition.y,
                camPos.z
            );
        }

        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound, 0.6f);

        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        if (target.ambientMusic != null && audioSource != null)
        {
            audioSource.clip = target.ambientMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        yield return StartCoroutine(TitleSequence(target));

        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        if (player != null) player.SetInvincible(false);

        isTransitioning = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence titre + description + attente touche
    // ════════════════════════════════════════════════════════════

    IEnumerator TitleSequence(LevelData target)
    {
        if (levelTitle == null) yield break;

        // Setup texte
        levelTitle.text = target.levelName;
        if (levelDescription != null) levelDescription.text = target.levelDescription;

        // ── Flicker IN titre ────────────────────────────────────
        yield return StartCoroutine(FlickerObjectIn(levelTitle.gameObject, titleFlickerInDuration));

        yield return new WaitForSeconds(delayBeforeDescription);

        // ── Flicker IN description ──────────────────────────────
        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectIn(levelDescription.gameObject, descriptionFlickerInDuration));

        // ── Délai avant prompt "continuer" ──────────────────────
        yield return new WaitForSeconds(delayBeforeContinuePrompt);

        // ── Affichage du prompt clignotant + attente touche ─────
        Coroutine blinkRoutine = null;
        if (continuePrompt != null)
        {
            continuePrompt.gameObject.SetActive(true);
            blinkRoutine = StartCoroutine(BlinkContinuePromptRoutine());
        }

        yield return StartCoroutine(WaitForContinueKey());

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        if (continuePrompt != null) continuePrompt.gameObject.SetActive(false);

        // ── Flicker OUT description ─────────────────────────────
        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectOut(levelDescription.gameObject, descriptionFlickerOutDuration));

        // ── Flicker OUT titre ───────────────────────────────────
        yield return StartCoroutine(FlickerObjectOut(levelTitle.gameObject, titleFlickerOutDuration));

        // ── Pause finale écran noir ─────────────────────────────
        yield return new WaitForSeconds(endBlackHold);
    }

    /// <summary>
    /// Attend que le joueur appuie sur la touche "continuer".
    /// Met à jour le texte du prompt chaque seconde avec le temps restant.
    /// Termine automatiquement après continueTimeout secondes par sécurité.
    /// </summary>
    IEnumerator WaitForContinueKey()
    {
        float elapsed = 0f;
        float lastDisplayedSecond = -1f;

        while (elapsed < continueTimeout)
        {
            if (Input.GetKeyDown(continueKey))
                yield break;

            // Mise à jour du texte chaque seconde (économie de perf : on ne reformate
            // que quand la valeur entière change réellement)
            if (continuePrompt != null)
            {
                float remaining = Mathf.Ceil(continueTimeout - elapsed);
                if (!Mathf.Approximately(remaining, lastDisplayedSecond))
                {
                    continuePrompt.text = FormatContinuePrompt(remaining);
                    lastDisplayedSecond = remaining;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[LevelTransitionManager] Timeout 'continuer' atteint, poursuite automatique.");
    }

    /// <summary>
    /// Formate le texte du prompt en intégrant le temps restant.
    /// </summary>
    string FormatContinuePrompt(float secondsRemaining)
    {
        int s = Mathf.RoundToInt(secondsRemaining);
        return $"Appuyez sur votre touche \"ENTRER\" pour continuer !\nSinon attendez simplement ({s} seconde{(s > 1 ? "s" : "")}) !";
    }

    /// <summary>
    /// Fait clignoter le prompt pendant l'attente de la touche.
    /// L'effet est un simple on/off du GameObject à intervalle fixe.
    /// </summary>
    IEnumerator BlinkContinuePromptRoutine()
    {
        if (continuePrompt == null) yield break;

        bool visible = true;
        while (true)
        {
            visible = !visible;
            continuePrompt.gameObject.SetActive(visible);
            yield return new WaitForSeconds(continuePromptBlinkInterval);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Flicker IN / OUT
    // ════════════════════════════════════════════════════════════

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

        go.SetActive(true);
    }

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

        go.SetActive(false);
    }
}