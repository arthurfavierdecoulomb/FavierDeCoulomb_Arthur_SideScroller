using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider voiceSlider;
    [SerializeField] Slider ambienceSlider;
    [SerializeField] Slider sfxSlider;

    [Header("Affichage en jeu")]
    [SerializeField] Slider hudOpacitySlider;

    [Header("Divers")]
    [SerializeField] Button resetButton;

    bool initializing;

    void Awake()
    {
        BindSlider(masterSlider, value => SettingsManager.Instance.SetMasterVolume(value));
        BindSlider(musicSlider, value => SettingsManager.Instance.SetMusicVolume(value));
        BindSlider(voiceSlider, value => SettingsManager.Instance.SetVoiceVolume(value));
        BindSlider(ambienceSlider, value => SettingsManager.Instance.SetAmbienceVolume(value));
        BindSlider(sfxSlider, value => SettingsManager.Instance.SetSfxVolume(value));
        BindSlider(hudOpacitySlider, value => SettingsManager.Instance.SetHudOpacity(value));

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);
    }

    void OnEnable()
    {
        RefreshFromSettings();
    }

    void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.Save();
    }

    void OnResetClicked()
    {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.ResetToDefaults();
        RefreshFromSettings();
    }

    void RefreshFromSettings()
    {
        if (SettingsManager.Instance == null) return;

        initializing = true;

        SetSliderValue(masterSlider, SettingsManager.Instance.MasterVolume);
        SetSliderValue(musicSlider, SettingsManager.Instance.MusicVolume);
        SetSliderValue(voiceSlider, SettingsManager.Instance.VoiceVolume);
        SetSliderValue(ambienceSlider, SettingsManager.Instance.AmbienceVolume);
        SetSliderValue(sfxSlider, SettingsManager.Instance.SfxVolume);
        SetSliderValue(hudOpacitySlider, SettingsManager.Instance.HudOpacity);

        initializing = false;
    }

    void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(value =>
        {
            if (initializing) return;
            if (SettingsManager.Instance == null) return;
            action(value);
        });
    }

    static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null) slider.value = value;
    }
}