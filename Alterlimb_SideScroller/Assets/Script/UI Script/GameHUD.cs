using TMPro;
using UnityEngine;

public enum HudTimerFormat { HoursMinutesSeconds, MinutesSecondsCentiseconds }

public class GameHud : MonoBehaviour
{
    [Header("Racine")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform container;

    [Header("Bloc morts")]
    [SerializeField] GameObject deathGroup;
    [SerializeField] TMP_Text deathLabel;
    [SerializeField, Range(1, 6)] int deathDigits = 4;

    [Header("Bloc chronomètre")]
    [SerializeField] GameObject timerGroup;
    [SerializeField] TMP_Text timerLabel;
    [SerializeField] HudTimerFormat timerFormat = HudTimerFormat.HoursMinutesSeconds;

    [Header("Alignement")]
    [SerializeField] TMP_Text[] alignedLabels;
    [SerializeField] Vector2 margin = new Vector2(32f, 24f);

    [Header("Pause")]
    [SerializeField] bool hideWhenPaused = true;
    [SerializeField] float fadeDuration = 0.15f;

    readonly char[] timerBuffer = new char[16];
    readonly char[] deathBuffer = new char[8];

    int lastTimerTick = -1;
    int lastDeaths = -1;
    bool showTimer;
    bool showDeaths;
    bool subscribed;

    bool IsPaused => Time.timeScale <= 0.0001f;

    void OnEnable()
    {
        TrySubscribe();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void OnDisable()
    {
        if (subscribed && SettingsManager.Instance != null)
            SettingsManager.Instance.OnHudSettingsChanged -= ApplyHudSettings;
        subscribed = false;
    }

    void LateUpdate()
    {
        TrySubscribe();
        RefreshAlpha();

        if (hideWhenPaused && IsPaused) return;
        if (SettingsManager.Instance == null) return;
        if (GameStats.Instance == null) return;

        if (showTimer) RefreshTimer();
        if (showDeaths) RefreshDeaths();
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.OnHudSettingsChanged += ApplyHudSettings;
        subscribed = true;
        ApplyHudSettings();
    }

    void RefreshAlpha()
    {
        if (canvasGroup == null) return;

        float target = TargetAlpha();
        canvasGroup.alpha = fadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime / fadeDuration);
    }

    float TargetAlpha()
    {
        if (SettingsManager.Instance == null) return 0f;
        if (hideWhenPaused && IsPaused) return 0f;
        if (!showTimer && !showDeaths) return 0f;
        return SettingsManager.Instance.HudOpacity;
    }

    void RefreshTimer()
    {
        float time = SettingsManager.Instance.TimerMode == TimerDisplayMode.Level
            ? GameStats.Instance.LevelTime
            : GameStats.Instance.ElapsedTime;

        int centiTotal = Mathf.FloorToInt(Mathf.Max(time, 0f) * 100f);
        int totalSeconds = centiTotal / 100;

        int tick = timerFormat == HudTimerFormat.MinutesSecondsCentiseconds ? centiTotal : totalSeconds;
        if (tick == lastTimerTick) return;
        lastTimerTick = tick;

        int first;
        int second;
        int third;

        if (timerFormat == HudTimerFormat.MinutesSecondsCentiseconds)
        {
            first = totalSeconds / 60;
            second = totalSeconds % 60;
            third = centiTotal % 100;
        }
        else
        {
            first = totalSeconds / 3600;
            second = (totalSeconds % 3600) / 60;
            third = totalSeconds % 60;
        }

        int length = 0;
        length = WriteNumber(timerBuffer, length, first, 2);
        timerBuffer[length++] = ':';
        length = WriteNumber(timerBuffer, length, second, 2);
        timerBuffer[length++] = ':';
        length = WriteNumber(timerBuffer, length, third, 2);

        timerLabel.SetCharArray(timerBuffer, 0, length);
    }

    void RefreshDeaths()
    {
        int deaths = SettingsManager.Instance.DeathMode == DeathDisplayMode.Level
            ? GameStats.Instance.LevelDeaths
            : GameStats.Instance.DeathCount;

        if (deaths == lastDeaths) return;
        lastDeaths = deaths;

        int length = WriteNumber(deathBuffer, 0, Mathf.Max(deaths, 0), deathDigits);
        deathLabel.SetCharArray(deathBuffer, 0, length);
    }

    void ApplyHudSettings()
    {
        if (SettingsManager.Instance == null) return;

        showTimer = SettingsManager.Instance.TimerMode != TimerDisplayMode.Off;
        showDeaths = SettingsManager.Instance.DeathMode != DeathDisplayMode.Off;

        if (timerGroup != null) timerGroup.SetActive(showTimer);
        if (deathGroup != null) deathGroup.SetActive(showDeaths);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        lastTimerTick = -1;
        lastDeaths = -1;

        ApplyCorner(SettingsManager.Instance.Corner);
    }

    void ApplyCorner(HudCorner corner)
    {
        Vector2 anchor;
        TextAlignmentOptions alignment;

        switch (corner)
        {
            case HudCorner.TopRight:
                anchor = new Vector2(1f, 1f);
                alignment = TextAlignmentOptions.Right;
                break;
            case HudCorner.BottomLeft:
                anchor = new Vector2(0f, 0f);
                alignment = TextAlignmentOptions.Left;
                break;
            case HudCorner.BottomRight:
                anchor = new Vector2(1f, 0f);
                alignment = TextAlignmentOptions.Right;
                break;
            default:
                anchor = new Vector2(0f, 1f);
                alignment = TextAlignmentOptions.Left;
                break;
        }

        if (container != null)
        {
            container.anchorMin = anchor;
            container.anchorMax = anchor;
            container.pivot = anchor;
            container.anchoredPosition = new Vector2(
                Mathf.Lerp(margin.x, -margin.x, anchor.x),
                Mathf.Lerp(margin.y, -margin.y, anchor.y));
        }

        if (alignedLabels == null) return;
        for (int i = 0; i < alignedLabels.Length; i++)
        {
            if (alignedLabels[i] != null)
                alignedLabels[i].alignment = alignment;
        }
    }

    static int WriteNumber(char[] buffer, int index, int value, int minDigits)
    {
        int digits = 1;
        int scale = 10;
        while (value >= scale && digits < 9)
        {
            digits++;
            scale *= 10;
        }
        if (digits < minDigits) digits = minDigits;

        for (int i = digits - 1; i >= 0; i--)
        {
            buffer[index + i] = (char)('0' + value % 10);
            value /= 10;
        }
        return index + digits;
    }
}