using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class FuseGUI : MonoBehaviour
{
    [Header("Références texte")]
    [SerializeField] TextMeshProUGUI fuseCountText;
    [SerializeField] TextMeshProUGUI onFuseCountText;

    [Header("Icône volante")]
    [SerializeField] RectTransform flyingIcon;
    [SerializeField] RectTransform iconTarget;

    [Header("Animation — Bounce au centre")]
    [SerializeField] float bounceScale = 1.4f;
    [SerializeField] float bounceDuration = 0.4f;
    [SerializeField] float pauseDuration = 0.25f;

    [Header("Animation — Vol vers le compteur")]
    [SerializeField] float flyDuration = 0.5f;
    [SerializeField] float flyArcHeight = 80f;

    [Header("Slide (remplace le flicker)")]
    [SerializeField] Vector2 hiddenOffset = new Vector2(-400f, 0f);
    [SerializeField] float slideInDuration = 0.3f;
    [SerializeField] float slideOutDuration = 0.25f;

    [Header("Audio")]
    [SerializeField] AudioMixerGroup sfxOutput;
    [SerializeField] AudioClip[] pickupAppearClips;
    [Range(0f, 1f)]
    [SerializeField] float pickupAppearVolume = 0.8f;
    [SerializeField] AudioClip[] countIncrementClips;
    [Range(0f, 1f)]
    [SerializeField] float countIncrementVolume = 0.7f;
    [SerializeField] Vector2 countPitchRange = new Vector2(0.95f, 1.05f);

    CanvasGroup canvasGroup;
    RectTransform rectTransform;
    Vector2 shownAnchoredPos;
    bool isVisible;
    Coroutine slideCoroutine;
    AudioSource sfxSource;
    AudioClip lastSfxClip;

    int displayedCount;

    readonly Queue<int> pickupQueue = new Queue<int>();
    bool isPlayingPickup;

    Vector2 screenCenter;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        shownAnchoredPos = rectTransform.anchoredPosition;
        rectTransform.anchoredPosition = shownAnchoredPos + hiddenOffset;
        canvasGroup.alpha = 1f;
        isVisible = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.outputAudioMixerGroup = sfxOutput;

        if (flyingIcon != null)
            flyingIcon.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        FuseManager.OnAllFusesInstalledStatic += HandleAllFusesInstalled;

        if (FuseManager.Instance != null)
            SubscribeToManager();
        else
            StartCoroutine(WaitForManager());
    }

    void OnDisable()
    {
        FuseManager.OnAllFusesInstalledStatic -= HandleAllFusesInstalled;

        if (FuseManager.Instance != null)
        {
            FuseManager.Instance.OnFuseCollected -= HandleFuseCollected;
            FuseManager.Instance.OnFuseInstalled -= HandleFuseInstalled;
        }
    }

    IEnumerator WaitForManager()
    {
        while (FuseManager.Instance == null)
            yield return null;
        SubscribeToManager();
    }

    void SubscribeToManager()
    {
        FuseManager.Instance.OnFuseCollected += HandleFuseCollected;
        FuseManager.Instance.OnFuseInstalled += HandleFuseInstalled;

        if (onFuseCountText != null)
            onFuseCountText.text = $"/{FuseManager.Instance.TotalFuses}";

        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();
    }

    void HandleFuseCollected()
    {
        pickupQueue.Enqueue(1);

        if (!isPlayingPickup)
            StartCoroutine(ProcessPickupQueue());
    }

    void HandleFuseInstalled()
    {
        displayedCount = Mathf.Max(0, displayedCount - 1);
        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();
    }

    void HandleAllFusesInstalled()
    {
        StartSlide(appearing: false);
    }

    IEnumerator ProcessPickupQueue()
    {
        isPlayingPickup = true;

        while (pickupQueue.Count > 0)
        {
            pickupQueue.Dequeue();
            yield return StartCoroutine(PlayPickupAnimation());
        }

        isPlayingPickup = false;
    }

    IEnumerator PlayPickupAnimation()
    {
        if (!isVisible)
        {
            isVisible = true;
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            yield return StartCoroutine(SlideRoutine(appearing: true));
        }

        if (flyingIcon == null || iconTarget == null)
        {
            IncrementDisplayedCount();
            yield break;
        }

        screenCenter = Vector2.zero;

        flyingIcon.gameObject.SetActive(true);
        flyingIcon.anchoredPosition = screenCenter;
        flyingIcon.localScale = Vector3.zero;
        PlaySfx(pickupAppearClips, pickupAppearVolume, 1f);

        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;
            float scale = BounceScale(t);
            flyingIcon.localScale = Vector3.one * scale;

            elapsed += Time.deltaTime;
            yield return null;
        }
        flyingIcon.localScale = Vector3.one;

        yield return new WaitForSeconds(pauseDuration);

        Vector2 startPos = flyingIcon.anchoredPosition;
        Vector2 endPos = GetTargetAnchoredPosition();

        elapsed = 0f;
        while (elapsed < flyDuration)
        {
            float t = elapsed / flyDuration;
            float easedT = t * t;

            Vector2 linearPos = Vector2.Lerp(startPos, endPos, easedT);
            float arc = Mathf.Sin(easedT * Mathf.PI) * flyArcHeight;
            flyingIcon.anchoredPosition = linearPos + Vector2.up * arc;

            flyingIcon.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, easedT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        flyingIcon.gameObject.SetActive(false);
        IncrementDisplayedCount();
    }

    float BounceScale(float t)
    {
        float overshoot = Mathf.Sin(t * Mathf.PI) * (bounceScale - 1f);
        return 1f + overshoot;
    }

    Vector2 GetTargetAnchoredPosition()
    {
        RectTransform iconParent = flyingIcon.parent as RectTransform;
        if (iconParent == null) return Vector2.zero;

        Vector2 worldPos = iconTarget.position;
        Vector2 localPos = iconParent.InverseTransformPoint(worldPos);
        return localPos;
    }

    void IncrementDisplayedCount()
    {
        displayedCount++;
        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();

        PlaySfx(countIncrementClips, countIncrementVolume, Random.Range(countPitchRange.x, countPitchRange.y));
    }

    void PlaySfx(AudioClip[] clips, float volume, float pitch)
    {
        if (sfxSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastSfxClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastSfxClip = clip;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    void StartSlide(bool appearing)
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideRoutine(appearing));
    }

    IEnumerator SlideRoutine(bool appearing)
    {
        isVisible = appearing;

        Vector2 hiddenPos = shownAnchoredPos + hiddenOffset;
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 end = appearing ? shownAnchoredPos : hiddenPos;
        float duration = appearing ? slideInDuration : slideOutDuration;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rectTransform.anchoredPosition = Vector2.Lerp(start, end, p);
            yield return null;
        }
        rectTransform.anchoredPosition = end;
        slideCoroutine = null;
    }
}