using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OxiOScreenUI : MonoBehaviour
{
    public enum CoreState
    {
        Off,
        On,
        Destroyed
    }

    [System.Serializable]
    public class CoreSlot
    {
        public Image image;
        public Sprite offSprite;
        public Sprite onSprite;
        public Sprite destroyedSprite;
    }

    [Header("Panneau")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float hiddenOffsetY = 420f;

    [Header("Descente")]
    [SerializeField] private float dropDuration = 0.9f;
    [SerializeField] private float bounceAmplitude = 38f;
    [SerializeField] private int bounceCount = 3;
    [Range(0.1f, 0.9f)]
    [SerializeField] private float bounceDamping = 0.4f;

    [Header("Remontée")]
    [SerializeField] private float retractDuration = 0.35f;

    [Header("Balancement")]
    [SerializeField] private bool idleSway = true;
    [SerializeField] private float swayAmplitude = 3f;
    [SerializeField] private float swaySpeed = 1.1f;
    [SerializeField] private float swayTilt = 0.7f;

    [Header("Titre")]
    [SerializeField] private GameObject titleRoot;
    [SerializeField] private float delayBeforeTitle = 0.25f;
    [SerializeField] private float titleFlickerDuration = 0.7f;

    [Header("Noyaux")]
    [SerializeField] private CoreSlot[] cores;
    [SerializeField] private float delayBeforeCores = 0.35f;
    [SerializeField] private float delayBetweenCoreReveals = 0.28f;
    [SerializeField] private float coreRevealFlickerDuration = 0.4f;
    [SerializeField] private bool lightCoresAfterReveal = true;
    [SerializeField] private float delayBeforeLighting = 0.35f;
    [SerializeField] private float delayBetweenCoreLights = 0.22f;
    [SerializeField] private float coreLightFlickerDuration = 0.3f;
    [SerializeField] private float coreDestroyFlickerDuration = 1.1f;

    [Header("Flicker")]
    [SerializeField] private float flickerIntervalMin = 0.025f;
    [SerializeField] private float flickerIntervalMax = 0.11f;

    [Header("Dialogue")]
    [SerializeField] private bool autoHideDuringDialogue = true;

    [Header("Événements")]
    public UnityEvent onIntroFinished;
    public UnityEvent onCoreDestroyed;
    public UnityEvent onAllCoresDestroyed;

    public bool IsVisible { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool CombatStarted { get; private set; }

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private Vector2 basePosition;
    private bool swayActive;
    private float swayTime;
    private CoreState[] states;
    private Coroutine currentRoutine;
    private bool hiddenByDialogue;

    private void Awake()
    {
        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = panelRect.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = panelRect.gameObject.AddComponent<CanvasGroup>();
        }

        shownPosition = panelRect.anchoredPosition;
        hiddenPosition = shownPosition + Vector2.up * hiddenOffsetY;
        basePosition = hiddenPosition;
        panelRect.anchoredPosition = hiddenPosition;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        states = new CoreState[cores != null ? cores.Length : 0];

        if (titleRoot != null)
            titleRoot.SetActive(false);

        for (int i = 0; i < states.Length; i++)
        {
            states[i] = CoreState.Off;

            if (cores[i] != null && cores[i].image != null)
                cores[i].image.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        HandleDialogueVisibility();

        if (!swayActive || !idleSway)
            return;

        swayTime += Time.deltaTime * swaySpeed;

        float offset = Mathf.Sin(swayTime) * swayAmplitude;
        panelRect.anchoredPosition = basePosition + Vector2.up * offset;
        panelRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(swayTime) * swayTilt);
    }

    private void HandleDialogueVisibility()
    {
        if (!autoHideDuringDialogue || !CombatStarted || IsAnimating)
            return;

        if (BossDialogueManager.Instance == null)
            return;

        bool dialoguePlaying = BossDialogueManager.Instance.IsPlaying;

        if (dialoguePlaying && IsVisible)
        {
            hiddenByDialogue = true;
            Hide();
            return;
        }

        if (!dialoguePlaying && hiddenByDialogue && !IsVisible)
        {
            hiddenByDialogue = false;
            Show();
        }
    }

    public void PlayIntro()
    {
        StopCurrentRoutine();
        CombatStarted = true;
        currentRoutine = StartCoroutine(IntroRoutine());
    }

    public void Show()
    {
        if (IsVisible)
            return;

        StopCurrentRoutine();
        currentRoutine = StartCoroutine(DropRoutine());
    }

    public void Hide()
    {
        if (!IsVisible)
            return;

        StopCurrentRoutine();
        currentRoutine = StartCoroutine(RetractRoutine());
    }

    public void SetCoreState(int index, CoreState state)
    {
        if (!IsValidIndex(index))
            return;

        states[index] = state;
        ApplySprite(index, state);
    }

    public void DestroyCore(int index)
    {
        if (!IsValidIndex(index) || states[index] == CoreState.Destroyed)
            return;

        StartCoroutine(DestroyCoreRoutine(index));
    }

    public void DestroyNextCore()
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i] != CoreState.Destroyed)
            {
                DestroyCore(i);
                return;
            }
        }
    }

    public int RemainingCores()
    {
        int remaining = 0;

        for (int i = 0; i < states.Length; i++)
            if (states[i] != CoreState.Destroyed)
                remaining++;

        return remaining;
    }

    private IEnumerator IntroRoutine()
    {
        yield return DropRoutine();

        yield return new WaitForSeconds(delayBeforeTitle);

        if (titleRoot != null)
            yield return FlickerIn(titleRoot, titleFlickerDuration);

        yield return new WaitForSeconds(delayBeforeCores);

        for (int i = 0; i < states.Length; i++)
        {
            if (cores[i] == null || cores[i].image == null)
                continue;

            SetCoreState(i, CoreState.Off);
            yield return FlickerIn(cores[i].image.gameObject, coreRevealFlickerDuration);
            yield return new WaitForSeconds(delayBetweenCoreReveals);
        }

        if (lightCoresAfterReveal)
        {
            yield return new WaitForSeconds(delayBeforeLighting);

            for (int i = 0; i < states.Length; i++)
            {
                if (cores[i] == null || cores[i].image == null)
                    continue;

                SetCoreState(i, CoreState.On);
                yield return FlickerHold(cores[i].image.gameObject, coreLightFlickerDuration);
                yield return new WaitForSeconds(delayBetweenCoreLights);
            }
        }

        currentRoutine = null;
        onIntroFinished?.Invoke();
    }

    private IEnumerator DestroyCoreRoutine(int index)
    {
        GameObject target = cores[index].image.gameObject;

        yield return FlickerHold(target, coreDestroyFlickerDuration * 0.6f);

        SetCoreState(index, CoreState.Destroyed);

        yield return FlickerHold(target, coreDestroyFlickerDuration * 0.4f);

        onCoreDestroyed?.Invoke();

        if (RemainingCores() == 0)
            onAllCoresDestroyed?.Invoke();
    }

    private IEnumerator DropRoutine()
    {
        IsAnimating = true;
        IsVisible = true;
        swayActive = false;

        panelRect.anchoredPosition = hiddenPosition;
        panelRect.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;

        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            float t = elapsed / dropDuration;
            Vector2 target = Vector2.Lerp(hiddenPosition, shownPosition, EaseOutCubic(t));

            float damping = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);

            panelRect.anchoredPosition = target + Vector2.down * (oscillation * damping * bounceAmplitude);
            panelRect.localRotation = Quaternion.Euler(0f, 0f, oscillation * damping * swayTilt * 2f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        basePosition = shownPosition;
        panelRect.anchoredPosition = shownPosition;
        panelRect.localRotation = Quaternion.identity;

        swayTime = 0f;
        swayActive = true;
        IsAnimating = false;
    }

    private IEnumerator RetractRoutine()
    {
        IsAnimating = true;
        swayActive = false;

        Vector2 start = panelRect.anchoredPosition;
        float startRotation = panelRect.localEulerAngles.z;
        float elapsed = 0f;

        while (elapsed < retractDuration)
        {
            float t = elapsed / retractDuration;
            panelRect.anchoredPosition = Vector2.Lerp(start, hiddenPosition, t * t);
            panelRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRotation, 0f, t));

            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = hiddenPosition;
        panelRect.localRotation = Quaternion.identity;
        canvasGroup.alpha = 0f;

        basePosition = hiddenPosition;
        IsVisible = false;
        IsAnimating = false;
        currentRoutine = null;
    }

    private IEnumerator FlickerIn(GameObject target, float duration)
    {
        float elapsed = 0f;
        bool visible = false;

        while (elapsed < duration)
        {
            visible = !visible;
            target.SetActive(visible);

            float wait = Random.Range(flickerIntervalMin, flickerIntervalMax);
            wait = Mathf.Min(wait, duration - elapsed);

            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        target.SetActive(true);
    }

    private IEnumerator FlickerHold(GameObject target, float duration)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            target.SetActive(visible);

            float wait = Random.Range(flickerIntervalMin, flickerIntervalMax);
            wait = Mathf.Min(wait, duration - elapsed);

            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        target.SetActive(true);
    }

    private void ApplySprite(int index, CoreState state)
    {
        CoreSlot slot = cores[index];

        if (slot == null || slot.image == null)
            return;

        if (state == CoreState.On && slot.onSprite != null)
            slot.image.sprite = slot.onSprite;
        else if (state == CoreState.Destroyed && slot.destroyedSprite != null)
            slot.image.sprite = slot.destroyedSprite;
        else if (slot.offSprite != null)
            slot.image.sprite = slot.offSprite;
    }

    private bool IsValidIndex(int index)
    {
        return states != null && index >= 0 && index < states.Length && cores[index] != null;
    }

    private void StopCurrentRoutine()
    {
        if (currentRoutine == null)
            return;

        StopCoroutine(currentRoutine);
        currentRoutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        float inverted = 1f - t;
        return 1f - inverted * inverted * inverted;
    }
}