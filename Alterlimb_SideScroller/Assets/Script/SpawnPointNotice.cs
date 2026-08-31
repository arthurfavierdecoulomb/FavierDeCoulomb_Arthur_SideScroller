using System.Collections;
using TMPro;
using UnityEngine;

public class SpawnPointNotice : MonoBehaviour
{
    public static SpawnPointNotice Instance { get; private set; }

    [Header("Panneau")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Vector2 hiddenOffset = new Vector2(-340f, 0f);

    [Header("Timing")]
    [SerializeField] private float slideInDuration = 0.35f;
    [SerializeField] private float holdDuration = 1.8f;
    [SerializeField] private float slideOutDuration = 0.28f;

    [Header("Icône")]
    [SerializeField] private Transform icon;
    [SerializeField] private float iconPopScale = 1.4f;
    [SerializeField] private float iconPopDuration = 0.28f;
    [SerializeField] private float iconPopDelay = 0.15f;

    [Header("Texte")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string defaultMessage = "SPAWNPOINT ENREGISTRÉ";

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private Vector3 iconBaseScale = Vector3.one;
    private Coroutine currentRoutine;

    private void Awake()
    {
        Instance = this;

        if (panel == null)
            panel = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        shownPosition = panel.anchoredPosition;
        hiddenPosition = shownPosition + hiddenOffset;

        if (icon != null)
            iconBaseScale = icon.localScale;

        panel.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show()
    {
        Show(null);
    }

    public void Show(string message)
    {
        if (label != null)
            label.text = string.IsNullOrEmpty(message) ? defaultMessage : message;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(NoticeRoutine());
    }

    private IEnumerator NoticeRoutine()
    {
        if (icon != null)
            icon.localScale = Vector3.zero;

        float elapsed = 0f;

        while (elapsed < slideInDuration)
        {
            float t = elapsed / slideInDuration;
            float eased = EaseOutCubic(t);

            panel.anchoredPosition = Vector2.Lerp(hiddenPosition, shownPosition, eased);
            canvasGroup.alpha = eased;

            elapsed += Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = shownPosition;
        canvasGroup.alpha = 1f;

        if (icon != null)
        {
            yield return new WaitForSeconds(iconPopDelay);
            yield return PopIcon();
        }

        yield return new WaitForSeconds(holdDuration);

        elapsed = 0f;
        Vector2 start = panel.anchoredPosition;

        while (elapsed < slideOutDuration)
        {
            float t = elapsed / slideOutDuration;

            panel.anchoredPosition = Vector2.Lerp(start, hiddenPosition, t * t);
            canvasGroup.alpha = 1f - t;

            elapsed += Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;
        currentRoutine = null;
    }

    private IEnumerator PopIcon()
    {
        float elapsed = 0f;

        while (elapsed < iconPopDuration)
        {
            float t = elapsed / iconPopDuration;
            float scale = EaseOutBack(t) * iconPopScale;

            icon.localScale = iconBaseScale * Mathf.Lerp(scale, 1f, t * t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        icon.localScale = iconBaseScale;
    }

    private static float EaseOutCubic(float t)
    {
        float inverted = 1f - t;
        return 1f - inverted * inverted * inverted;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.7f;
        float inverted = t - 1f;
        return 1f + (overshoot + 1f) * inverted * inverted * inverted + overshoot * inverted * inverted;
    }
}