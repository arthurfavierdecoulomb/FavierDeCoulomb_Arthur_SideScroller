using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class LevelTransitionManager : MonoBehaviour
{
    public static LevelTransitionManager Instance { get; private set; }

    [System.Serializable]
    public class TransitionTakeover
    {
        public string label = "Oxi-O";
        public LevelData level;

        [Header("Déclenchement")]
        public float delayBeforeTakeover = 1.2f;
        public float takeoverFlickerDuration = 0.45f;

        [Header("Couleurs")]
        public Color titleColor = new Color(1f, 0.85f, 0.1f);
        public Color descriptionColor = new Color(1f, 0.85f, 0.1f);
        public Color promptColor = new Color(1f, 0.85f, 0.1f);

        [Header("CRT")]
        public Material crtMaterial;
        public Texture crtTexture;
        public Color crtTint = Color.white;
        public GameObject crtOverrideObject;

        [Header("Titre détourné")]
        public bool glitchTitle = true;
        public string corruptedTitle = "OXI-O";
        public float titleGlitchDuration = 1.1f;
        public float titleGlitchInterval = 0.045f;

        [Header("Description réécrite")]
        public bool rewriteDescription = true;
        [TextArea(2, 5)]
        public string corruptedDescription = "JE T'ATTENDS, AZU.";
        public float delayBeforeRewrite = 0.6f;
        public float eraseDuration = 0.7f;
        public float typeInterval = 0.055f;

        [Header("Son")]
        public AudioClip takeoverSound;
        public AudioClip typeSound;
    }

    [Header("Références UI")]
    [SerializeField] GameObject transitionOverlay;
    [SerializeField] GameObject blackBackground;
    [SerializeField] GameObject crtEffect;
    [SerializeField] TextMeshProUGUI levelTitle;
    [SerializeField] TextMeshProUGUI levelDescription;
    [SerializeField] TextMeshProUGUI continuePrompt;
    [SerializeField] GameObject gameUICanvas;

    [Header("Invite de continuation")]
    [SerializeField] GameObject continuePromptRoot;
    [SerializeField] RectTransform continuePromptRect;
    [SerializeField] float promptSlideDistance = 420f;
    [SerializeField] float promptSlideDuration = 0.45f;
    [SerializeField] RectTransform continueIconRect;
    [SerializeField] float iconPopDuration = 0.25f;
    [SerializeField] float iconPopOvershoot = 1.4f;
    [SerializeField] bool iconPulse = true;
    [SerializeField] float iconPulseScale = 1.12f;
    [SerializeField] float iconPulseDuration = 0.9f;

    [Header("Décompte sous l'invite")]
    [SerializeField] float promptRiseOffset = 4f;
    [SerializeField] float promptRiseDuration = 0.25f;
    [SerializeField] GameObject countdownRoot;
    [SerializeField] TextMeshProUGUI countdownLabel;
    [SerializeField] string countdownFormat = "( {0} SECONDES AVANT LA DISPARITION AUTOMATIQUE DE CET ÉCRAN )";

    [Header("Références joueur & caméra")]
    [SerializeField] CharaController player;
    [SerializeField] Camera mainCamera;

    [Header("Prises de contrôle")]
    [SerializeField] List<TransitionTakeover> takeovers = new List<TransitionTakeover>();
    [SerializeField] string glitchCharacters = "!<>-_\\/[]{}—=+*^?#________ØÆ0123456789";

    [Header("Diagnostic")]
    [SerializeField] bool logDiagnostics = true;

    [Header("Course automatique (transitions entre niveaux)")]
    [SerializeField] float autoRunSafetyTimeout = 5f;
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


    [Header("Audio — bruitages")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip transitionSound;

    [Header("Audio — musique des niveaux")]
    [SerializeField] float musicFadeOutDuration = 2f;
    [SerializeField] float musicFadeInDuration = 1.5f;

    bool isTransitioning;

    Color baseTitleColor = Color.white;
    Color baseDescriptionColor = Color.white;
    Color basePromptColor = Color.white;
    Color baseCountdownColor = Color.white;

    Vector2 promptBasePosition;
    Vector3 iconBaseScale = Vector3.one;
    Coroutine iconPulseRoutine;

    Graphic crtGraphic;
    RawImage crtRawImage;
    Material baseCrtMaterial;
    Texture baseCrtTexture;
    Color baseCrtColor = Color.white;

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
        CachePromptDefaults();

        if (continuePromptRoot != null) { continuePromptRoot.SetActive(false); }

        CacheVisualDefaults();
        LogSetup();
    }

    void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (crtEffect == null)
            Debug.LogError("[LevelTransitionManager] Le champ Crt Effect est vide : aucun effet CRT ne s'affichera.", this);

        if (blackBackground == null)
            Debug.LogError("[LevelTransitionManager] Le champ Black Background est vide : le fond noir ne sera jamais affiché ni retiré.", this);

        if (levelTitle == null)
            Debug.LogError("[LevelTransitionManager] Le champ Level Title est vide : la carte de niveau ne s'affichera pas.", this);

        if (continuePromptRoot == null)
            Debug.LogWarning("[LevelTransitionManager] Continue Prompt Root est vide : aucune invite ne sera affichée ni animée.", this);

        if (countdownRoot == null)
            Debug.LogWarning("[LevelTransitionManager] Countdown Root est vide : le texte du décompte restera visible en permanence au lieu d'apparaître après l'animation.", this);

        if (countdownLabel == null)
            Debug.LogWarning("[LevelTransitionManager] Countdown Label est vide : le décompte ne sera jamais écrit.", this);

        if (countdownRoot != null && continuePromptRoot != null && !countdownRoot.transform.IsChildOf(continuePromptRoot.transform))
            Debug.LogWarning("[LevelTransitionManager] Countdown Root n'est pas un enfant de Continue Prompt Root : il ne suivra pas la remontée du panel.", this);

        if (Mathf.Abs(promptRiseOffset) < 0.5f)
            Debug.LogWarning($"[LevelTransitionManager] Prompt Rise Offset vaut {promptRiseOffset}, la remontée sera invisible. Essaie 20 à 40 en unités de Canvas.", this);

        foreach (TransitionTakeover takeover in takeovers)
        {
            if (takeover == null)
                continue;

            if (takeover.level == null)
                Debug.LogWarning($"[LevelTransitionManager] La prise de contrôle '{takeover.label}' n'a pas de LevelData, elle ne se déclenchera jamais.", this);

            if (IsInvalidOverride(takeover))
                Debug.LogError($"[LevelTransitionManager] La prise de contrôle '{takeover.label}' : le Crt Override Object est le Crt Effect lui-même ou son parent. Il désactiverait le CRT normal. Mets-y une COPIE indépendante, ou vide le champ.", this);
        }
    }

    void CachePromptDefaults()
    {
        if (continuePromptRect == null && continuePromptRoot != null)
            continuePromptRect = continuePromptRoot.GetComponent<RectTransform>();

        if (continuePromptRect != null)
            promptBasePosition = continuePromptRect.anchoredPosition;

        if (continueIconRect != null)
            iconBaseScale = continueIconRect.localScale;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);
    }

    bool IsInvalidOverride(TransitionTakeover takeover)
    {
        if (takeover == null || takeover.crtOverrideObject == null || crtEffect == null)
            return false;

        if (takeover.crtOverrideObject == crtEffect)
            return true;

        return crtEffect.transform.IsChildOf(takeover.crtOverrideObject.transform);
    }

    void CacheVisualDefaults()
    {
        if (levelTitle != null) baseTitleColor = levelTitle.color;
        if (levelDescription != null) baseDescriptionColor = levelDescription.color;
        if (continuePrompt != null) basePromptColor = continuePrompt.color;
        if (countdownLabel != null) baseCountdownColor = countdownLabel.color;

        if (crtEffect == null) return;

        crtGraphic = crtEffect.GetComponentInChildren<Graphic>(true);
        crtRawImage = crtGraphic as RawImage;

        if (crtGraphic != null)
        {
            baseCrtMaterial = crtGraphic.material;
            baseCrtColor = crtGraphic.color;
        }
        else
        {
            Debug.LogWarning("[LevelTransitionManager] Aucun Image ou RawImage trouvé sous l'objet CRT : la prise de contrôle ne pourra pas changer son material.", this);
        }

        if (crtRawImage != null)
            baseCrtTexture = crtRawImage.texture;

        foreach (TransitionTakeover takeover in takeovers)
            if (takeover != null && takeover.crtOverrideObject != null && !IsInvalidOverride(takeover))
                takeover.crtOverrideObject.SetActive(false);
    }

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

    IEnumerator FullTransitionSequence(LevelData target, float autoRunDir, float runDistance)
    {
        isTransitioning = true;

        player.SetInvincible(true);
        player.SetAutoRun(true, autoRunDir);

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(musicFadeOutDuration);

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

        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound, 0.6f);

        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        yield return StartCoroutine(TitleSequence(target));

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

        FailSafeRestore();

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

        if (target.ambientMusic != null && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.PlayMusic(target.ambientMusic, musicFadeInDuration);

        player.SetAutoRun(false, 0f);
        player.SetInvincible(false);

        isTransitioning = false;
    }

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

        FailSafeRestore();

        if (target.ambientMusic != null && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.PlayMusic(target.ambientMusic, musicFadeInDuration);

        if (player != null) player.SetInvincible(false);

        isTransitioning = false;
    }

    IEnumerator TitleSequence(LevelData target)
    {
        if (levelTitle == null) yield break;

        TransitionTakeover takeover = FindTakeover(target);

        levelTitle.text = target.levelName;
        if (levelDescription != null) levelDescription.text = target.levelDescription;

        yield return StartCoroutine(FlickerObjectIn(levelTitle.gameObject, titleFlickerInDuration));

        yield return new WaitForSeconds(delayBeforeDescription);

        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectIn(levelDescription.gameObject, descriptionFlickerInDuration));

        if (takeover != null)
            yield return StartCoroutine(TakeoverRoutine(takeover));

        yield return new WaitForSeconds(delayBeforeContinuePrompt);

        yield return StartCoroutine(ShowPromptRoutine());

        yield return StartCoroutine(WaitForContinueKey());

        HidePrompt();

        if (levelDescription != null)
            yield return StartCoroutine(FlickerObjectOut(levelDescription.gameObject, descriptionFlickerOutDuration));

        yield return StartCoroutine(FlickerObjectOut(levelTitle.gameObject, titleFlickerOutDuration));

        yield return new WaitForSeconds(endBlackHold);

        if (takeover != null)
            RestoreVisuals(takeover);

        if (logDiagnostics)
            Debug.Log("[LevelTransitionManager] Séquence de carte terminée.", this);
    }

    void FailSafeRestore()
    {
        foreach (TransitionTakeover takeover in takeovers)
            if (takeover != null && takeover.crtOverrideObject != null && !IsInvalidOverride(takeover))
                takeover.crtOverrideObject.SetActive(false);

        HidePrompt();

        if (levelTitle != null) { levelTitle.color = baseTitleColor; levelTitle.enabled = true; }
        if (levelDescription != null) { levelDescription.color = baseDescriptionColor; levelDescription.enabled = true; }
        if (continuePrompt != null) continuePrompt.color = basePromptColor;
        if (countdownLabel != null) countdownLabel.color = baseCountdownColor;

        if (crtGraphic != null)
        {
            crtGraphic.material = baseCrtMaterial;
            crtGraphic.color = baseCrtColor;
        }

        if (crtRawImage != null)
            crtRawImage.texture = baseCrtTexture;

        if (crtEffect != null) crtEffect.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        if (logDiagnostics)
            Debug.Log("[LevelTransitionManager] Carte refermée, fond noir et CRT désactivés.", this);
    }

    TransitionTakeover FindTakeover(LevelData target)
    {
        foreach (TransitionTakeover takeover in takeovers)
            if (takeover != null && takeover.level == target)
                return takeover;

        return null;
    }

    IEnumerator TakeoverRoutine(TransitionTakeover takeover)
    {
        if (takeover.delayBeforeTakeover > 0f)
            yield return new WaitForSeconds(takeover.delayBeforeTakeover);

        if (audioSource != null && takeover.takeoverSound != null)
            audioSource.PlayOneShot(takeover.takeoverSound, 0.8f);

        yield return StartCoroutine(TakeoverFlickerRoutine(takeover));

        ApplyTakeoverVisuals(takeover);

        if (takeover.glitchTitle)
            yield return StartCoroutine(GlitchTextRoutine(levelTitle, takeover.corruptedTitle, takeover));

        if (!takeover.rewriteDescription || levelDescription == null)
            yield break;

        if (takeover.delayBeforeRewrite > 0f)
            yield return new WaitForSeconds(takeover.delayBeforeRewrite);

        yield return StartCoroutine(EraseTextRoutine(levelDescription, takeover.eraseDuration));
        yield return StartCoroutine(TypeTextRoutine(levelDescription, takeover.corruptedDescription, takeover));
    }

    IEnumerator TakeoverFlickerRoutine(TransitionTakeover takeover)
    {
        float duration = Mathf.Max(0.01f, takeover.takeoverFlickerDuration);
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;

            if (levelTitle != null) levelTitle.enabled = visible;
            if (levelDescription != null) levelDescription.enabled = visible;

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        if (levelTitle != null) levelTitle.enabled = true;
        if (levelDescription != null) levelDescription.enabled = true;
    }

    void ApplyTakeoverVisuals(TransitionTakeover takeover)
    {
        try
        {
            ApplyTakeoverVisualsInternal(takeover);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelTransitionManager] Échec de la prise de contrôle visuelle : {e.Message}", this);
        }
    }

    void ApplyTakeoverVisualsInternal(TransitionTakeover takeover)
    {
        if (levelTitle != null) levelTitle.color = takeover.titleColor;
        if (levelDescription != null) levelDescription.color = takeover.descriptionColor;
        if (continuePrompt != null) continuePrompt.color = takeover.promptColor;
        if (countdownLabel != null) countdownLabel.color = takeover.promptColor;

        if (takeover.crtOverrideObject != null && !IsInvalidOverride(takeover))
        {
            takeover.crtOverrideObject.SetActive(true);
            if (crtEffect != null) crtEffect.SetActive(false);
            return;
        }

        if (crtGraphic == null)
            return;

        if (takeover.crtMaterial != null)
            crtGraphic.material = takeover.crtMaterial;

        if (crtRawImage != null && takeover.crtTexture != null)
            crtRawImage.texture = takeover.crtTexture;

        crtGraphic.color = takeover.crtTint;
    }

    void RestoreVisuals(TransitionTakeover takeover)
    {
        try
        {
            RestoreVisualsInternal(takeover);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelTransitionManager] Échec de la restauration visuelle : {e.Message}", this);
        }
    }

    void RestoreVisualsInternal(TransitionTakeover takeover)
    {
        if (levelTitle != null) levelTitle.color = baseTitleColor;
        if (levelDescription != null) levelDescription.color = baseDescriptionColor;
        if (continuePrompt != null) continuePrompt.color = basePromptColor;
        if (countdownLabel != null) countdownLabel.color = baseCountdownColor;

        if (takeover.crtOverrideObject != null && !IsInvalidOverride(takeover))
        {
            takeover.crtOverrideObject.SetActive(false);

            if (crtEffect != null)
                crtEffect.SetActive(true);
        }

        if (levelTitle != null) levelTitle.enabled = true;
        if (levelDescription != null) levelDescription.enabled = true;

        if (crtGraphic == null)
            return;

        crtGraphic.material = baseCrtMaterial;
        crtGraphic.color = baseCrtColor;

        if (crtRawImage != null)
            crtRawImage.texture = baseCrtTexture;
    }

    IEnumerator GlitchTextRoutine(TextMeshProUGUI label, string finalText, TransitionTakeover takeover)
    {
        if (label == null || string.IsNullOrEmpty(finalText)) yield break;

        float duration = Mathf.Max(0.05f, takeover.titleGlitchDuration);
        float interval = Mathf.Max(0.01f, takeover.titleGlitchInterval);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            int settled = Mathf.FloorToInt(progress * finalText.Length);

            label.text = BuildGlitchedText(finalText, settled);

            if (audioSource != null && takeover.typeSound != null && Random.value < 0.3f)
                audioSource.PlayOneShot(takeover.typeSound, 0.2f);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        label.text = finalText;
    }

    string BuildGlitchedText(string finalText, int settled)
    {
        if (string.IsNullOrEmpty(glitchCharacters))
            return finalText;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(finalText.Length);

        for (int i = 0; i < finalText.Length; i++)
        {
            if (i < settled || finalText[i] == ' ')
                builder.Append(finalText[i]);
            else
                builder.Append(glitchCharacters[Random.Range(0, glitchCharacters.Length)]);
        }

        return builder.ToString();
    }

    IEnumerator EraseTextRoutine(TextMeshProUGUI label, float duration)
    {
        if (label == null) yield break;

        string current = label.text;

        if (current.Length == 0) yield break;

        float interval = Mathf.Max(0.005f, duration / current.Length);

        while (current.Length > 0)
        {
            current = current.Substring(0, current.Length - 1);
            label.text = current;
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator TypeTextRoutine(TextMeshProUGUI label, string text, TransitionTakeover takeover)
    {
        if (label == null || string.IsNullOrEmpty(text)) yield break;

        label.text = "";
        float interval = Mathf.Max(0.005f, takeover.typeInterval);

        foreach (char c in text)
        {
            label.text += c;

            if (audioSource != null && takeover.typeSound != null && c != ' ')
                audioSource.PlayOneShot(takeover.typeSound, 0.25f);

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator WaitForContinueKey()
    {
        float elapsed = 0f;
        float lastDisplayedSecond = -1f;

        while (elapsed < continueTimeout)
        {
            if (Input.GetKeyDown(continueKey))
                yield break;

            if (countdownLabel != null)
            {
                float remaining = Mathf.Ceil(continueTimeout - elapsed);
                if (!Mathf.Approximately(remaining, lastDisplayedSecond))
                {
                    countdownLabel.text = FormatCountdown(remaining);
                    lastDisplayedSecond = remaining;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[LevelTransitionManager] Timeout 'continuer' atteint, poursuite automatique.");
    }

    string FormatCountdown(float secondsRemaining)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(secondsRemaining));
        return string.Format(countdownFormat, s.ToString("00"));
    }

    IEnumerator ShowPromptRoutine()
    {
        if (continuePromptRoot == null)
            yield break;

        continuePromptRoot.SetActive(true);

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (continueIconRect != null)
            continueIconRect.localScale = Vector3.zero;

        if (continuePromptRect != null)
        {
            Vector2 start = promptBasePosition + Vector2.right * promptSlideDistance;
            continuePromptRect.anchoredPosition = start;

            float duration = Mathf.Max(0.01f, promptSlideDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                continuePromptRect.anchoredPosition = Vector2.Lerp(start, promptBasePosition, eased);
                yield return null;
            }

            continuePromptRect.anchoredPosition = promptBasePosition;
        }

        if (continueIconRect != null)
        {
            float duration = Mathf.Max(0.01f, iconPopDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                continueIconRect.localScale = iconBaseScale * EaseOutBack(t, iconPopOvershoot);
                yield return null;
            }

            continueIconRect.localScale = iconBaseScale;

            if (iconPulse)
                iconPulseRoutine = StartCoroutine(IconPulseRoutine());
        }

        if (continuePromptRect != null && Mathf.Abs(promptRiseOffset) > 0.001f)
        {
            Vector2 risen = promptBasePosition + Vector2.up * promptRiseOffset;
            float duration = Mathf.Max(0.01f, promptRiseDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                continuePromptRect.anchoredPosition = Vector2.Lerp(promptBasePosition, risen, eased);
                yield return null;
            }

            continuePromptRect.anchoredPosition = risen;
        }

        if (countdownRoot != null)
            countdownRoot.SetActive(true);

        if (logDiagnostics)
            Debug.Log($"[LevelTransitionManager] Invite affichée. Décompte {(countdownRoot != null ? "activé" : "ABSENT")}, remontée de {promptRiseOffset}.", this);
    }

    IEnumerator IconPulseRoutine()
    {
        float time = 0f;
        float period = Mathf.Max(0.05f, iconPulseDuration);

        while (true)
        {
            time += Time.deltaTime;
            float wave = (Mathf.Sin(time * Mathf.PI * 2f / period) + 1f) * 0.5f;
            continueIconRect.localScale = iconBaseScale * Mathf.Lerp(1f, iconPulseScale, wave);
            yield return null;
        }
    }

    float EaseOutBack(float t, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot - 1f) * 1.70158f;
        float c3 = c1 + 1f;
        float inv = t - 1f;
        return 1f + c3 * inv * inv * inv + c1 * inv * inv;
    }

    void HidePrompt()
    {
        if (iconPulseRoutine != null)
        {
            StopCoroutine(iconPulseRoutine);
            iconPulseRoutine = null;
        }

        if (continueIconRect != null)
            continueIconRect.localScale = iconBaseScale;

        if (continuePromptRect != null)
            continuePromptRect.anchoredPosition = promptBasePosition;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);

        if (continuePromptRoot != null)
            continuePromptRoot.SetActive(false);
    }

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