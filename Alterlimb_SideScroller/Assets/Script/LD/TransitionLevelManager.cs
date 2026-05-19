using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Gère les transitions entre niveaux ET l'intro de démarrage du jeu.
/// 
/// MUSIQUE : déléguée au LevelMusicPlayer.
///   - Au début de la transition  → fondu de sortie de la musique du niveau quitté.
///   - À la fin de la course de sortie → la musique du nouveau niveau démarre
///     (pile quand le joueur reprend le contrôle).
/// 
/// Deux modes :
///   1. StartTransition(level, autoRunDir, runDistance) — transition entre niveaux
///   2. StartIntro(level) — démarrage du jeu
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
    [SerializeField] float titleFlickerInDuration = 0.5f;
    [SerializeField] float delayBeforeDescription = 0.5f;
    [SerializeField] float descriptionFlickerInDuration = 0.5f;
    [SerializeField] float delayBeforeContinuePrompt = 0.8f;

    [Header("Touche pour continuer")]
    [SerializeField] KeyCode continueKey = KeyCode.Return;
    [SerializeField] float continueTimeout = 30f;

    [Header("Timings — Disparition")]
    [SerializeField] float descriptionFlickerOutDuration = 0.5f;
    [SerializeField] float titleFlickerOutDuration = 0.5f;
    [SerializeField] float endBlackHold = 0.5f;

    [Header("Flicker (apparition / disparition clignotante)")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Tooltip("Intervalle (en secondes) du clignotement du texte 'Appuyez sur Entrée'")]
    [SerializeField] float continuePromptBlinkInterval = 0.5f;

    [Header("Audio — bruitages")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip transitionSound;

    [Header("Audio — musique des niveaux")]
    [Tooltip("Durée du fondu de sortie de la musique au début d'une transition")]
    [SerializeField] float musicFadeOutDuration = 2f;
    [Tooltip("Durée du fondu d'entrée de la nouvelle musique de niveau")]
    [SerializeField] float musicFadeInDuration = 1.5f;

    // ════════════════════════════════════════════════════════════
    //  État
    // ════════════════════════════════════════════════════════════

    bool isTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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

        // ── Fondu de sortie de la musique du niveau quitté ──────
        // Démarre dès le début de la transition (pendant la course d'entrée).
        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(musicFadeOutDuration);

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
        // (La musique ne démarre PAS ici — elle attend la fin de la course
        //  de sortie, pour coller au moment où le joueur reprend la main.)
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

        // ── Phase 6 : Désactivation écran noir + CRT ────────────
        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        // ── Phase 7 : Course automatique de sortie ──────────────
        float exitDir = Mathf.Sign(target.exitRunDirection);
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

        // ── La course de sortie est finie → la nouvelle musique démarre ──
        if (target.ambientMusic != null && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.PlayMusic(target.ambientMusic, musicFadeInDuration);

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

        yield return StartCoroutine(TitleSequence(target));

        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        // ── La musique du premier niveau démarre quand le joueur reprend la main ──
        if (target.ambientMusic != null && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.PlayMusic(target.ambientMusic, musicFadeInDuration);

        if (player != null) player.SetInvincible(false);

        isTransitioning = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence titre + description + attente touche
    // ════════════════════════════════════════════════════════════

    IEnumerator TitleSequence(LevelData target)
    {
        if (levelTitle == null) yield break;

        levelTitle.text = target.levelName;
        if (levelDescription != null) levelDescription.text = target.levelDescription;

        yield return StartCoroutine(FlickerObjectIn(levelTitle.gameObject, titleFlickerInDuration));

        yield return new WaitForSeconds(delayBeforeDescription);

        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectIn(levelDescription.gameObject, descriptionFlickerInDuration));

        yield return new WaitForSeconds(delayBeforeContinuePrompt);

        Coroutine blinkRoutine = null;
        if (continuePrompt != null)
        {
            continuePrompt.gameObject.SetActive(true);
            blinkRoutine = StartCoroutine(BlinkContinuePromptRoutine());
        }

        yield return StartCoroutine(WaitForContinueKey());

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        if (continuePrompt != null) continuePrompt.gameObject.SetActive(false);

        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectOut(levelDescription.gameObject, descriptionFlickerOutDuration));

        yield return StartCoroutine(FlickerObjectOut(levelTitle.gameObject, titleFlickerOutDuration));

        yield return new WaitForSeconds(endBlackHold);
    }

    IEnumerator WaitForContinueKey()
    {
        float elapsed = 0f;
        float lastDisplayedSecond = -1f;

        while (elapsed < continueTimeout)
        {
            if (Input.GetKeyDown(continueKey))
                yield break;

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

    string FormatContinuePrompt(float secondsRemaining)
    {
        int s = Mathf.RoundToInt(secondsRemaining);
        return $"Appuyez sur votre touche \"ENTRER\" pour continuer !\nSinon attendez simplement ({s} seconde{(s > 1 ? "s" : "")}) !";
    }

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