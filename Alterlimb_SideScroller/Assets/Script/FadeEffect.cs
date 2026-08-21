using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class FadeEffect : MonoBehaviour
{
    [Header("Durées")]
    [SerializeField] float fadeInDuration = 0.8f;
    [SerializeField] float fadeOutDuration = 0.8f;

    [Header("Interaction")]
    [SerializeField] bool controlRaycasts = true;

    [Header("État de départ")]
    [SerializeField] bool hiddenOnAwake = true;

    CanvasGroup canvasGroup;
    Coroutine fadeRoutine;

    public bool IsVisible => CanvasGroup.alpha > 0.001f;

    CanvasGroup CanvasGroup
    {
        get
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            return canvasGroup;
        }
    }

    void Awake()
    {
        ApplyAlpha(hiddenOnAwake ? 0f : 1f);
    }

    public void FadeIn()
    {
        RestartFade(FadeInRoutine());
    }

    public void FadeOut()
    {
        RestartFade(FadeOutRoutine());
    }

    public IEnumerator FadeInRoutine()
    {
        return FadeToRoutine(1f, fadeInDuration);
    }

    public IEnumerator FadeOutRoutine()
    {
        return FadeToRoutine(0f, fadeOutDuration);
    }

    public void ShowInstantly()
    {
        StopFade();
        ApplyAlpha(1f);
    }

    public void HideInstantly()
    {
        StopFade();
        ApplyAlpha(0f);
    }

    void RestartFade(IEnumerator routine)
    {
        StopFade();
        fadeRoutine = StartCoroutine(routine);
    }

    void StopFade()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    IEnumerator FadeToRoutine(float targetAlpha, float duration)
    {
        float startAlpha = CanvasGroup.alpha;

        if (duration <= 0f)
        {
            ApplyAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            ApplyAlpha(Mathf.Lerp(startAlpha, targetAlpha, smooth));
            yield return null;
        }

        ApplyAlpha(targetAlpha);
        fadeRoutine = null;
    }

    void ApplyAlpha(float alpha)
    {
        CanvasGroup.alpha = alpha;

        if (controlRaycasts)
        {
            bool visible = alpha > 0.001f;
            CanvasGroup.blocksRaycasts = visible;
            CanvasGroup.interactable = visible;
        }
    }
}