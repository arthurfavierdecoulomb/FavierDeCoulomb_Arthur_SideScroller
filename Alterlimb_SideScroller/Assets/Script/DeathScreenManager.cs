using UnityEngine;
using System.Collections;
using TMPro;

public class DeathAnimationManager : MonoBehaviour
{
    public static DeathAnimationManager Instance { get; private set; }

    [Header("Références UI")]
    [SerializeField] GameObject deathOverlay;
    [SerializeField] CanvasGroup backgroundGroup;
    [SerializeField] CanvasGroup textGroup;
    [SerializeField] RectTransform textRect;
    [SerializeField] TextMeshProUGUI deathText;
    [SerializeField] GameObject crtEffect;
    [SerializeField] CanvasGroup crtGroup;
    [Range(0f, 1f)]
    [SerializeField] float crtMaxAlpha = 1f;
    [SerializeField] GameObject gameUICanvas;

    [Header("Texte")]
    [SerializeField] string message = "Déconnecté...";

    [Header("Caméra")]
    [SerializeField] Camera targetCamera;
    [SerializeField] MonoBehaviour cameraFollowScript;
    [SerializeField] Transform player;
    [SerializeField] string playerTag = "Player";
    [SerializeField] float cameraReturnDuration = 0.8f;
    [SerializeField] AnimationCurve cameraReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Tremblement de caméra")]
    [SerializeField] bool useCameraShake = true;
    [SerializeField] float shakeDuration = 0.45f;
    [SerializeField] float shakeMagnitude = 0.35f;
    [SerializeField] float shakeInterval = 0.02f;
    [SerializeField] AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Ralenti")]
    [SerializeField] bool useSlowMotion = true;
    [Range(0.01f, 1f)]
    [SerializeField] float slowMotionScale = 0.2f;
    [SerializeField] float slowMotionEnterDuration = 0.35f;
    [SerializeField] float slowMotionExitDuration = 0.5f;

    [Header("Fondu du background")]
    [SerializeField] float backgroundFadeInDuration = 0.6f;
    [SerializeField] float backgroundFadeOutDuration = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] float backgroundMaxAlpha = 0.9f;
    [SerializeField] bool blackoutBeforeRespawn = true;
    [SerializeField] float blackoutDuration = 0.2f;
    [SerializeField] bool overlapFadeAndCameraReturn = true;

    [Header("Apparition du texte")]
    [SerializeField] float textStartOffsetY = -450f;
    [SerializeField] float textRiseDuration = 0.7f;
    [Range(0f, 4f)]
    [SerializeField] float bounceOvershoot = 1.7f;
    [SerializeField] float textFadeInDuration = 0.35f;
    [SerializeField] float textFadeOutDuration = 0.5f;

    [Header("Timings")]
    [SerializeField] float delayBeforeText = 0.25f;
    [SerializeField] float holdDuration = 5f;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip deathSound;
    [SerializeField] AudioClip textAppearSound;

    Vector2 textCenterPosition;
    float defaultFixedDeltaTime;
    bool isPlaying;

    float DeltaTime => Time.unscaledDeltaTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetCamera == null) targetCamera = Camera.main;
        if (player == null) FindPlayer();

        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (textRect != null)
            textCenterPosition = textRect.anchoredPosition;

        if (deathText != null)
            deathText.text = message;

        HideEverything();
    }

    void OnDisable()
    {
        RestoreTimeScale();
    }

    void FindPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null) player = go.transform;
    }

    void HideEverything()
    {
        if (backgroundGroup != null) backgroundGroup.alpha = 0f;
        if (textGroup != null) textGroup.alpha = 0f;
        if (crtGroup != null) crtGroup.alpha = 0f;
        if (crtEffect != null) crtEffect.SetActive(false);
        if (textRect != null)
            textRect.anchoredPosition = textCenterPosition + new Vector2(0f, textStartOffsetY);
        if (deathOverlay != null) deathOverlay.SetActive(false);
    }

    public void PlayDeathSequence(System.Action onRespawn, Vector3 checkpointPosition = default)
    {
        if (isPlaying)
        {
            Debug.LogWarning("[DeathAnimationManager] Une séquence est déjà en cours. Ignoré.");
            return;
        }
        StartCoroutine(DeathSequence(onRespawn));
    }

    IEnumerator DeathSequence(System.Action onRespawn)
    {
        isPlaying = true;

        if (GameStats.Instance != null)
            GameStats.Instance.AddDeath();

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.MuffleMusic();

        if (audioSource != null && deathSound != null)
            audioSource.PlayOneShot(deathSound, 0.8f);

        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        if (deathOverlay != null) deathOverlay.SetActive(true);
        if (backgroundGroup != null) backgroundGroup.alpha = 0f;
        if (textGroup != null) textGroup.alpha = 0f;
        if (deathText != null) deathText.text = message;

        if (crtEffect != null) crtEffect.SetActive(true);
        if (crtGroup != null) crtGroup.alpha = 0f;

        if (textRect != null)
            textRect.anchoredPosition = textCenterPosition + new Vector2(0f, textStartOffsetY);

        if (cameraFollowScript != null) cameraFollowScript.enabled = false;

        if (useCameraShake)
            yield return StartCoroutine(ShakeCameraRoutine());

        if (useSlowMotion)
            yield return StartCoroutine(LerpTimeScale(1f, slowMotionScale, slowMotionEnterDuration));

        Coroutine crtFadeIn = StartCoroutine(FadeCanvasGroup(crtGroup, 0f, crtMaxAlpha, backgroundFadeInDuration));
        yield return StartCoroutine(FadeCanvasGroup(backgroundGroup, 0f, backgroundMaxAlpha, backgroundFadeInDuration));
        if (crtFadeIn != null) yield return crtFadeIn;

        yield return WaitFor(delayBeforeText);

        if (audioSource != null && textAppearSound != null)
            audioSource.PlayOneShot(textAppearSound, 0.7f);

        yield return StartCoroutine(TextEnterRoutine());

        yield return WaitFor(holdDuration);

        if (blackoutBeforeRespawn && backgroundMaxAlpha < 1f)
            yield return StartCoroutine(FadeCanvasGroup(backgroundGroup, backgroundMaxAlpha, 1f, blackoutDuration));

        onRespawn?.Invoke();

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.UnmuffleMusic();

        if (player == null) FindPlayer();

        Coroutine timeRestore = null;
        if (useSlowMotion)
            timeRestore = StartCoroutine(LerpTimeScale(Time.timeScale, 1f, slowMotionExitDuration));

        Coroutine cameraReturn = StartCoroutine(ReturnCameraToPlayer(cameraReturnDuration));

        if (!overlapFadeAndCameraReturn)
            yield return cameraReturn;

        Coroutine textFade = StartCoroutine(FadeCanvasGroup(textGroup, 1f, 0f, textFadeOutDuration));
        Coroutine crtFadeOut = StartCoroutine(FadeCanvasGroup(crtGroup, GetAlpha(crtGroup), 0f, backgroundFadeOutDuration));
        yield return StartCoroutine(FadeCanvasGroup(backgroundGroup, GetAlpha(backgroundGroup), 0f, backgroundFadeOutDuration));
        if (textFade != null) yield return textFade;
        if (crtFadeOut != null) yield return crtFadeOut;

        if (cameraReturn != null) yield return cameraReturn;
        if (timeRestore != null) yield return timeRestore;

        RestoreTimeScale();

        if (cameraFollowScript != null) cameraFollowScript.enabled = true;

        HideEverything();
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        isPlaying = false;
    }

    IEnumerator LerpTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            ApplyTimeScale(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyTimeScale(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        ApplyTimeScale(to);
    }

    void ApplyTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * scale;
    }

    void RestoreTimeScale()
    {
        Time.timeScale = 1f;
        if (defaultFixedDeltaTime > 0f)
            Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    IEnumerator ShakeCameraRoutine()
    {
        if (targetCamera == null || shakeDuration <= 0f) yield break;

        Transform cam = targetCamera.transform;
        Vector3 basePosition = cam.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float strength = shakeFalloff.Evaluate(Mathf.Clamp01(elapsed / shakeDuration)) * shakeMagnitude;
            Vector2 offset = Random.insideUnitCircle * strength;

            cam.position = basePosition + new Vector3(offset.x, offset.y, 0f);

            float wait = Mathf.Max(0.001f, shakeInterval);
            float waited = 0f;
            while (waited < wait)
            {
                waited += DeltaTime;
                elapsed += DeltaTime;
                yield return null;
            }
        }

        cam.position = basePosition;
    }

    IEnumerator ReturnCameraToPlayer(float duration)
    {
        if (targetCamera == null || player == null) yield break;

        Transform cam = targetCamera.transform;
        Vector3 startPos = cam.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float t = cameraReturnCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            Vector3 target = new Vector3(player.position.x, player.position.y, startPos.z);
            cam.position = Vector3.LerpUnclamped(startPos, target, t);
            yield return null;
        }

        cam.position = new Vector3(player.position.x, player.position.y, startPos.z);
    }

    IEnumerator TextEnterRoutine()
    {
        if (textRect == null)
        {
            yield return StartCoroutine(FadeCanvasGroup(textGroup, 0f, 1f, textFadeInDuration));
            yield break;
        }

        Vector2 startPos = textCenterPosition + new Vector2(0f, textStartOffsetY);
        textRect.anchoredPosition = startPos;

        Coroutine fade = StartCoroutine(FadeCanvasGroup(textGroup, 0f, 1f, textFadeInDuration));

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, textRiseDuration);

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, bounceOvershoot);
            textRect.anchoredPosition = Vector2.LerpUnclamped(startPos, textCenterPosition, eased);
            yield return null;
        }

        textRect.anchoredPosition = textCenterPosition;

        if (fade != null) yield return fade;
    }

    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * (p * p * p) + c1 * (p * p);
    }

    float GetAlpha(CanvasGroup group) => group != null ? group.alpha : 0f;

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        group.alpha = from;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    IEnumerator WaitFor(float seconds)
    {
        if (seconds <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }
}