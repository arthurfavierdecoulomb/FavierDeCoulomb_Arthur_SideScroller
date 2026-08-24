using System.Collections;
using UnityEngine;

public class PanelAnimator : MonoBehaviour
{
    [Header("Cible")]
    [SerializeField] GameObject panelObject;
    [SerializeField] RectTransform panelTransform;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Durées")]
    [SerializeField] float openDuration = 0.25f;
    [SerializeField] float closeDuration = 0.18f;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Mouvement")]
    [SerializeField] Vector2 slideOffset = new Vector2(0f, -60f);
    [SerializeField, Range(0.5f, 1f)] float startScale = 0.92f;

    [Header("Courbes")]
    [SerializeField] AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Départ")]
    [SerializeField] bool openOnStart;

    Vector2 basePosition;
    AnimationCurve activeCurve;
    Coroutine routine;
    float progress;

    public bool IsOpen => progress > 0.5f;

    void Awake()
    {
        if (panelObject == null && panelTransform != null) panelObject = panelTransform.gameObject;
        if (panelTransform != null) basePosition = panelTransform.anchoredPosition;

        activeCurve = openCurve;
        progress = openOnStart ? 1f : 0f;

        ApplyProgress();

        if (panelObject != null) panelObject.SetActive(openOnStart);
    }

    public void Open()
    {
        if (panelObject != null) panelObject.SetActive(true);
        AnimateTo(1f);
    }

    public void Close()
    {
        AnimateTo(0f);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void AnimateTo(float targetProgress)
    {
        if (routine != null) StopCoroutine(routine);

        activeCurve = targetProgress > progress ? openCurve : closeCurve;
        routine = StartCoroutine(AnimateRoutine(targetProgress));
    }

    IEnumerator AnimateRoutine(float targetProgress)
    {
        float start = progress;
        float duration = targetProgress > start ? openDuration : closeDuration;
        float total = duration * Mathf.Abs(targetProgress - start);

        if (total > 0.0001f)
        {
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                progress = Mathf.Lerp(start, targetProgress, Mathf.Clamp01(elapsed / total));
                ApplyProgress();
                yield return null;
            }
        }

        progress = targetProgress;
        ApplyProgress();

        if (progress <= 0f && panelObject != null)
            panelObject.SetActive(false);

        routine = null;
    }

    void ApplyProgress()
    {
        float eased = activeCurve != null ? activeCurve.Evaluate(progress) : progress;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = eased;
            canvasGroup.interactable = progress >= 1f;
            canvasGroup.blocksRaycasts = progress > 0f;
        }

        if (panelTransform != null)
        {
            panelTransform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, eased);
            panelTransform.anchoredPosition = basePosition + slideOffset * (1f - eased);
        }
    }
}