using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SettingCyclerTarget { TimerMode, DeathMode, HudCorner }

public class SettingCycler : MonoBehaviour
{
    [Header("Réglage ciblé")]
    [SerializeField] SettingCyclerTarget target;

    [Header("Références")]
    [SerializeField] Button previousButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text valueLabel;

    [Header("Libellés")]
    [SerializeField] string[] customLabels;

    static readonly string[] modeLabels = { "DÉSACTIVÉ", "NIVEAU", "TOTAL" };
    static readonly string[] cornerLabels = { "HAUT GAUCHE", "HAUT DROITE", "BAS GAUCHE", "BAS DROITE" };

    string[] Labels
    {
        get
        {
            if (customLabels != null && customLabels.Length > 0) return customLabels;
            return target == SettingCyclerTarget.HudCorner ? cornerLabels : modeLabels;
        }
    }

    void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    void OnEnable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnHudSettingsChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnHudSettingsChanged -= Refresh;
    }

    void Step(int direction)
    {
        if (SettingsManager.Instance == null) return;

        int count = Labels.Length;
        if (count <= 0) return;

        Apply(((CurrentIndex() + direction) % count + count) % count);
    }

    int CurrentIndex()
    {
        switch (target)
        {
            case SettingCyclerTarget.DeathMode: return (int)SettingsManager.Instance.DeathMode;
            case SettingCyclerTarget.HudCorner: return (int)SettingsManager.Instance.Corner;
            default: return (int)SettingsManager.Instance.TimerMode;
        }
    }

    void Apply(int index)
    {
        switch (target)
        {
            case SettingCyclerTarget.DeathMode:
                SettingsManager.Instance.SetDeathMode((DeathDisplayMode)index);
                break;
            case SettingCyclerTarget.HudCorner:
                SettingsManager.Instance.SetCorner((HudCorner)index);
                break;
            default:
                SettingsManager.Instance.SetTimerMode((TimerDisplayMode)index);
                break;
        }
    }

    void Refresh()
    {
        if (valueLabel == null) return;
        if (SettingsManager.Instance == null) return;

        string[] labels = Labels;
        valueLabel.text = labels[Mathf.Clamp(CurrentIndex(), 0, labels.Length - 1)];
    }
}