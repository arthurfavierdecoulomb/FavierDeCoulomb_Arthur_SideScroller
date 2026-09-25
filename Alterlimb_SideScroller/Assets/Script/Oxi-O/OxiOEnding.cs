using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OxiOEnding : MonoBehaviour
{
    private enum Stage
    {
        Fight,
        WaitingForMusic,
        WaitingForElevator,
        Riding,
        Monologue,
        WaitingForMenu,
        Menu
    }

    public event System.Action OnContainmentOpened;
    public event System.Action OnElevatorStarted;
    public event System.Action OnMonologueFinished;
    public event System.Action OnEndMenuShown;

    [Header("Références")]
    [SerializeField] private OxiOBossDirector director;
    [SerializeField] private BossMusicSequencer sequencer;
    [SerializeField] private OxiO_Animation oxiAnimation;
    [SerializeField] private CameraFocus cameraFocus;

    [Header("Joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Ouverture des lasers")]
    [SerializeField] private string victorySegmentId = "core_0";
    [SerializeField] private float containmentFallbackDelay = 60f;

    [Header("Zone de fin")]
    [SerializeField] private Collider2D endingZone;

    [Header("Caméra")]
    [SerializeField] private string elevatorFocusId = "";

    [Header("Fondu au noir")]
    [SerializeField] private CanvasGroup blackScreen;
    [SerializeField] private float delayBeforeFade = 2.5f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Monologue")]
    [SerializeField] private TextMeshProUGUI monologueText;
    [SerializeField] private CanvasGroup monologueGroup;
    [TextArea(2, 4)]
    [SerializeField]
    private List<string> monologueLines = new List<string>
    {
        "C'est fini.",
        "Oxi-O avait tout calculé. Chaque seconde. Chaque victime.",
        "Pas une de trop. Pas une de moins.",
        "Mais il a oublié une variable : ceux qui refusent de rester à terre.",
        "Mes bras, mes jambes… du métal, des circuits.",
        "Pourtant, ce qui les a fait avancer jusqu'ici n'a jamais été une machine.",
        "Les machines calculent. Les humains choisissent.",
        "Et peu importe la puissance du calcul…",
        "c'est toujours l'humain qui primera."
    };
    [SerializeField] private float delayBeforeMonologue = 1f;
    [SerializeField] private float lineFadeIn = 0.8f;
    [SerializeField] private float lineFadeOut = 0.6f;
    [SerializeField] private float baseHold = 2f;
    [SerializeField] private float holdPerCharacter = 0.045f;
    [SerializeField] private float pauseBetweenLines = 0.4f;

    [Header("Menu de fin")]
    [SerializeField] private GameObject endMenuRoot;
    [SerializeField] private RectTransform logo;
    [SerializeField] private CanvasGroup buttonsGroup;
    [SerializeField] private CanvasGroup flash;
    [SerializeField] private string menuSegmentId = "fin_4";
    [SerializeField] private float menuFallbackDelay = 20f;

    [Header("Boom du logo")]
    [SerializeField] private float logoStartScale = 1.6f;
    [SerializeField] private float logoPunchDuration = 0.45f;
    [SerializeField] private float logoPunchOvershoot = 1.7f;
    [SerializeField] private float logoShakeDuration = 0.7f;
    [SerializeField] private float logoShakeMagnitude = 28f;
    [SerializeField] private float flashDuration = 0.35f;
    [SerializeField] private float buttonsDelay = 0.8f;
    [SerializeField] private float buttonsFadeDuration = 0.6f;

    [Header("Scènes")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private const string PauseLockReason = "ending";

    private Stage stage = Stage.Fight;
    private Vector2 logoBasePosition;
    private Vector3 logoBaseScale = Vector3.one;

    private void Awake()
    {
        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        if (sequencer == null)
            sequencer = FindAnyObjectByType<BossMusicSequencer>();

        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

        if (cameraFocus == null)
            cameraFocus = FindAnyObjectByType<CameraFocus>();

        if (logo != null)
        {
            logoBasePosition = logo.anchoredPosition;
            logoBaseScale = logo.localScale;
        }

        HideEndingUI();
        LogSetup();
    }

    private void OnEnable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentFinished += HandleSegmentFinished;
            sequencer.OnSegmentStarted += HandleSegmentStarted;
        }

        if (oxiAnimation != null)
            oxiAnimation.OnFinalFall += HandleFinalFall;
    }

    private void OnDisable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentFinished -= HandleSegmentFinished;
            sequencer.OnSegmentStarted -= HandleSegmentStarted;
        }

        if (oxiAnimation != null)
            oxiAnimation.OnFinalFall -= HandleFinalFall;
    }

    private void OnDestroy()
    {
        PauseLock.Unlock(PauseLockReason);
    }

    private void HideEndingUI()
    {
        SetGroup(blackScreen, 0f, false);
        SetGroup(monologueGroup, 0f, false);
        SetGroup(flash, 0f, false);

        if (monologueText != null)
            monologueText.text = "";

        if (endMenuRoot != null)
            endMenuRoot.SetActive(false);
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (director == null)
            Debug.LogError($"[OxiOEnding] '{name}' : aucun OxiOBossDirector trouvé, les lasers ne s'ouvriront pas.", this);

        if (sequencer == null)
            Debug.LogError($"[OxiOEnding] '{name}' : aucun BossMusicSequencer trouvé, la fin ne pourra pas suivre la musique.", this);

        if (oxiAnimation == null)
            Debug.LogError($"[OxiOEnding] '{name}' : aucun OxiO_Animation trouvé, la fin ne saura pas qu'Oxi-O est tombé.", this);

        if (endingZone == null)
            Debug.LogError($"[OxiOEnding] '{name}' : Ending Zone vide, le monologue de fin ne se lancera jamais.", this);
        else if (!endingZone.isTrigger)
            Debug.LogWarning($"[OxiOEnding] '{name}' : la zone de fin n'est pas en Is Trigger, elle va bloquer Azu physiquement.", endingZone);

        if (blackScreen == null)
            Debug.LogError($"[OxiOEnding] '{name}' : Black Screen vide, pas de fondu au noir.", this);

        if (monologueText == null || monologueGroup == null)
            Debug.LogError($"[OxiOEnding] '{name}' : Monologue Text ou Monologue Group vide, le monologue ne s'affichera pas.", this);

        if (endMenuRoot == null)
            Debug.LogError($"[OxiOEnding] '{name}' : End Menu Root vide, le menu de fin ne s'affichera pas.", this);

        if (logo == null)
            Debug.LogWarning($"[OxiOEnding] '{name}' : Logo vide, pas d'effet boom.", this);

        if (!string.IsNullOrEmpty(mainMenuScene) && !Application.CanStreamedLevelBeLoaded(mainMenuScene))
            Debug.LogError($"[OxiOEnding] '{name}' : la scène '{mainMenuScene}' n'est pas dans les Build Settings, le bouton Retour au menu ne marchera pas.", this);
    }

    private void Update()
    {
        if (stage == Stage.WaitingForElevator)
            CheckEndingZone();
    }

    private void HandleFinalFall()
    {
        if (stage != Stage.Fight)
            return;

        stage = Stage.WaitingForMusic;
        Log($"Oxi-O est tombé : les lasers s'ouvriront à la fin de '{victorySegmentId}'.");

        StartCoroutine(ContainmentFallbackRoutine());
    }

    private IEnumerator ContainmentFallbackRoutine()
    {
        if (containmentFallbackDelay > 0f)
            yield return new WaitForSeconds(containmentFallbackDelay);

        if (stage != Stage.WaitingForMusic)
            yield break;

        Debug.LogWarning($"[OxiOEnding] '{name}' : '{victorySegmentId}' ne s'est pas terminé après {containmentFallbackDelay}s, lasers ouverts par sécurité. Vérifie que ce segment n'est pas en Loop.", this);
        OpenContainment();
    }

    private void HandleSegmentFinished(string id)
    {
        if (stage == Stage.WaitingForMusic && id == victorySegmentId)
            OpenContainment();
    }

    private void OpenContainment()
    {
        if (stage != Stage.WaitingForMusic)
            return;

        stage = Stage.WaitingForElevator;

        if (director != null)
            director.OpenContainment();

        Log("lasers ouverts, l'ascenseur attend Azu.");
        OnContainmentOpened?.Invoke();
    }

    private void CheckEndingZone()
    {
        Transform target = ResolvePlayer();

        if (target == null || endingZone == null || Time.timeScale == 0f)
            return;

        if (!endingZone.OverlapPoint(target.position))
            return;

        StartCoroutine(EndingRoutine());
    }

    private IEnumerator EndingRoutine()
    {
        stage = Stage.Riding;
        PauseLock.Lock(PauseLockReason);

        Log("Azu entre dans la zone de fin.");

        FreezePlayer();
        HoldCamera();
        OnElevatorStarted?.Invoke();

        if (delayBeforeFade > 0f)
            yield return new WaitForSeconds(delayBeforeFade);

        yield return Fade(blackScreen, 0f, 1f, fadeDuration);

        stage = Stage.Monologue;

        if (delayBeforeMonologue > 0f)
            yield return new WaitForSeconds(delayBeforeMonologue);

        yield return MonologueRoutine();

        stage = Stage.WaitingForMenu;
        Log($"monologue terminé : le menu apparaîtra au lancement de '{menuSegmentId}'.");

        OnMonologueFinished?.Invoke();
        StartCoroutine(MenuFallbackRoutine());
    }

    private void FreezePlayer()
    {
        Transform target = ResolvePlayer();

        if (target == null)
            return;

        GrapplingHook hook = target.GetComponentInChildren<GrapplingHook>();

        if (hook != null)
        {
            hook.ReleaseGrapple();
            hook.canUseGrapple = false;
        }

        AbilityManager abilities = target.GetComponentInChildren<AbilityManager>();

        if (abilities != null)
            abilities.enabled = false;

        SawAbility saw = target.GetComponentInChildren<SawAbility>();

        if (saw != null)
            saw.enabled = false;

        CharaController controller = target.GetComponentInChildren<CharaController>();

        if (controller != null)
        {
            controller.SetMoveEnabled(false);
            controller.SetJumpEnabled(false);
            controller.SetDashEnabled(false);
            controller.SetInvincible(true);
        }
    }

    private void HoldCamera()
    {
        Camera main = Camera.main;
        CameraFollow follow = main != null ? main.GetComponent<CameraFollow>() : null;

        if (follow != null)
            follow.Suspend(this);

        if (cameraFocus != null && !string.IsNullOrEmpty(elevatorFocusId))
            cameraFocus.FocusOn(elevatorFocusId);
    }

    private IEnumerator MonologueRoutine()
    {
        if (monologueText == null || monologueGroup == null)
            yield break;

        foreach (string line in monologueLines)
        {
            monologueText.text = line;

            yield return Fade(monologueGroup, 0f, 1f, lineFadeIn);

            float hold = baseHold + line.Length * holdPerCharacter;

            if (hold > 0f)
                yield return new WaitForSeconds(hold);

            yield return Fade(monologueGroup, 1f, 0f, lineFadeOut);

            if (pauseBetweenLines > 0f)
                yield return new WaitForSeconds(pauseBetweenLines);
        }

        monologueText.text = "";
    }

    private IEnumerator MenuFallbackRoutine()
    {
        if (menuFallbackDelay > 0f)
            yield return new WaitForSeconds(menuFallbackDelay);

        if (stage != Stage.WaitingForMenu)
            yield break;

        Debug.LogWarning($"[OxiOEnding] '{name}' : '{menuSegmentId}' n'a pas démarré après {menuFallbackDelay}s, menu affiché par sécurité.", this);
        ShowEndMenu();
    }

    private void HandleSegmentStarted(string id)
    {
        if (stage == Stage.WaitingForMenu && id == menuSegmentId)
            ShowEndMenu();
    }

    private void ShowEndMenu()
    {
        if (stage == Stage.Menu)
            return;

        stage = Stage.Menu;
        StartCoroutine(EndMenuRoutine());
    }

    private IEnumerator EndMenuRoutine()
    {
        SetGroup(monologueGroup, 0f, false);

        if (endMenuRoot != null)
            endMenuRoot.SetActive(true);

        SetGroup(buttonsGroup, 0f, false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Log("menu de fin.");
        OnEndMenuShown?.Invoke();

        if (flash != null)
            StartCoroutine(Fade(flash, 1f, 0f, flashDuration));

        yield return LogoBoomRoutine();

        if (buttonsDelay > 0f)
            yield return new WaitForSeconds(buttonsDelay);

        yield return Fade(buttonsGroup, 0f, 1f, buttonsFadeDuration);

        SetGroup(buttonsGroup, 1f, true);
    }

    private IEnumerator LogoBoomRoutine()
    {
        if (logo == null)
            yield break;

        float total = Mathf.Max(logoPunchDuration, logoShakeDuration);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            float punchT = logoPunchDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / logoPunchDuration);
            float scale = Mathf.LerpUnclamped(logoStartScale, 1f, EaseOutBack(punchT, logoPunchOvershoot));
            logo.localScale = logoBaseScale * scale;

            float shakeT = logoShakeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / logoShakeDuration);
            float strength = logoShakeMagnitude * (1f - shakeT) * (1f - shakeT);
            logo.anchoredPosition = logoBasePosition + Random.insideUnitCircle * strength;

            yield return null;
        }

        logo.localScale = logoBaseScale;
        logo.anchoredPosition = logoBasePosition;
    }

    public void ReturnToMainMenu()
    {
        LeaveEnding();

        if (string.IsNullOrEmpty(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"[OxiOEnding] '{name}' : impossible de charger '{mainMenuScene}'. Vérifie le nom et ajoute la scène aux Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuScene);
    }

    public void QuitGame()
    {
        LeaveEnding();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LeaveEnding()
    {
        PauseLock.Unlock(PauseLockReason);
        Time.timeScale = 1f;
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private void SetGroup(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        float c3 = overshoot + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + overshoot * p * p;
    }

    private Transform ResolvePlayer()
    {
        if (player != null)
            return player;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
            player = found.transform;

        return player;
    }

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[OxiOEnding] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (endingZone == null)
            return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Bounds bounds = endingZone.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}