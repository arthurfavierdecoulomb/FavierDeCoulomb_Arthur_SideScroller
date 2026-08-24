using TMPro;
using UnityEngine;

public class PauseMenuStats : MonoBehaviour
{
    [Header("Bloc morts")]
    [SerializeField] GameObject deathGroup;
    [SerializeField] TMP_Text deathLabel;
    [SerializeField, Range(1, 6)] int deathDigits = 4;

    [Header("Bloc chronomètre")]
    [SerializeField] GameObject timerGroup;
    [SerializeField] TMP_Text timerLabel;
    [SerializeField] bool showCentiseconds = true;

    [Header("Comportement")]
    [SerializeField] bool followHudSettings = true;

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        ApplyVisibility();

        if (GameStats.Instance == null) return;

        if (timerLabel != null)
            timerLabel.text = FormatTime(TimerSource());

        if (deathLabel != null)
            deathLabel.text = DeathSource().ToString(new string('0', deathDigits));
    }

    void ApplyVisibility()
    {
        bool showTimer = true;
        bool showDeaths = true;

        if (followHudSettings && SettingsManager.Instance != null)
        {
            showTimer = SettingsManager.Instance.TimerMode != TimerDisplayMode.Off;
            showDeaths = SettingsManager.Instance.DeathMode != DeathDisplayMode.Off;
        }

        if (timerGroup != null) timerGroup.SetActive(showTimer);
        if (deathGroup != null) deathGroup.SetActive(showDeaths);
    }

    float TimerSource()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.TimerMode == TimerDisplayMode.Level)
            return GameStats.Instance.LevelTime;

        return GameStats.Instance.ElapsedTime;
    }

    int DeathSource()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.DeathMode == DeathDisplayMode.Level)
            return GameStats.Instance.LevelDeaths;

        return GameStats.Instance.DeathCount;
    }

    string FormatTime(float time)
    {
        int centiTotal = Mathf.FloorToInt(Mathf.Max(time, 0f) * 100f);
        int centiseconds = centiTotal % 100;
        int totalSeconds = centiTotal / 100;
        int seconds = totalSeconds % 60;
        int minutes = (totalSeconds / 60) % 60;
        int hours = totalSeconds / 3600;

        if (showCentiseconds)
            return $"{minutes:00}:{seconds:00}:{centiseconds:00}";

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}