using UnityEngine;
using UnityEngine.Audio;

public enum TimerDisplayMode { Off, Level, Total }
public enum DeathDisplayMode { Off, Level, Total }
public enum HudCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    const string MasterVolumeKey = "settings.volume.master";
    const string MusicVolumeKey = "settings.volume.music";
    const string VoiceVolumeKey = "settings.volume.voice";
    const string AmbienceVolumeKey = "settings.volume.ambience";
    const string SfxVolumeKey = "settings.volume.sfx";
    const string TimerModeKey = "settings.hud.timer";
    const string DeathModeKey = "settings.hud.deaths";
    const string HudCornerKey = "settings.hud.corner";
    const string HudOpacityKey = "settings.hud.opacity";

    [Header("Mixer")]
    [SerializeField] AudioMixer mixer;

    [Header("Paramètres exposés du mixer")]
    [SerializeField] string masterParameter = "MasterVolume";
    [SerializeField] string musicParameter = "MusicVolume";
    [SerializeField] string voiceParameter = "VoiceVolume";
    [SerializeField] string ambienceParameter = "AmbienceVolume";
    [SerializeField] string sfxParameter = "SfxVolume";

    [Header("Valeurs par défaut")]
    [Range(0f, 1f)][SerializeField] float defaultMasterVolume = 1f;
    [Range(0f, 1f)][SerializeField] float defaultMusicVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] float defaultVoiceVolume = 1f;
    [Range(0f, 1f)][SerializeField] float defaultAmbienceVolume = 0.7f;
    [Range(0f, 1f)][SerializeField] float defaultSfxVolume = 0.9f;
    [Range(0f, 1f)][SerializeField] float defaultHudOpacity = 0.85f;

    float masterVolume;
    float musicVolume;
    float voiceVolume;
    float ambienceVolume;
    float sfxVolume;
    TimerDisplayMode timerMode;
    DeathDisplayMode deathMode;
    HudCorner hudCorner;
    float hudOpacity;

    public event System.Action OnHudSettingsChanged;

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float VoiceVolume => voiceVolume;
    public float AmbienceVolume => ambienceVolume;
    public float SfxVolume => sfxVolume;
    public TimerDisplayMode TimerMode => timerMode;
    public DeathDisplayMode DeathMode => deathMode;
    public HudCorner Corner => hudCorner;
    public float HudOpacity => hudOpacity;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        Load();
    }

    void Start()
    {
        ApplyAllVolumes();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        ApplyVolume(masterParameter, masterVolume);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        ApplyVolume(musicParameter, musicVolume);
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VoiceVolumeKey, voiceVolume);
        ApplyVolume(voiceParameter, voiceVolume);
    }

    public void SetAmbienceVolume(float value)
    {
        ambienceVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(AmbienceVolumeKey, ambienceVolume);
        ApplyVolume(ambienceParameter, ambienceVolume);
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        ApplyVolume(sfxParameter, sfxVolume);
    }

    public void SetTimerMode(TimerDisplayMode mode)
    {
        timerMode = mode;
        PlayerPrefs.SetInt(TimerModeKey, (int)mode);
        OnHudSettingsChanged?.Invoke();
    }

    public void SetDeathMode(DeathDisplayMode mode)
    {
        deathMode = mode;
        PlayerPrefs.SetInt(DeathModeKey, (int)mode);
        OnHudSettingsChanged?.Invoke();
    }

    public void SetCorner(HudCorner corner)
    {
        hudCorner = corner;
        PlayerPrefs.SetInt(HudCornerKey, (int)corner);
        OnHudSettingsChanged?.Invoke();
    }

    public void SetHudOpacity(float value)
    {
        hudOpacity = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(HudOpacityKey, hudOpacity);
        OnHudSettingsChanged?.Invoke();
    }

    public void ResetToDefaults()
    {
        SetMasterVolume(defaultMasterVolume);
        SetMusicVolume(defaultMusicVolume);
        SetVoiceVolume(defaultVoiceVolume);
        SetAmbienceVolume(defaultAmbienceVolume);
        SetSfxVolume(defaultSfxVolume);
        SetTimerMode(TimerDisplayMode.Off);
        SetDeathMode(DeathDisplayMode.Off);
        SetCorner(HudCorner.TopLeft);
        SetHudOpacity(defaultHudOpacity);
        Save();
    }

    public void Save()
    {
        PlayerPrefs.Save();
    }

    void Load()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        voiceVolume = PlayerPrefs.GetFloat(VoiceVolumeKey, defaultVoiceVolume);
        ambienceVolume = PlayerPrefs.GetFloat(AmbienceVolumeKey, defaultAmbienceVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume);
        //timerMode = (TimerDisplayMode)PlayerPrefs.GetInt(TimerModeKey, (int)TimerDisplayMode.Off);
        //deathMode = (DeathDisplayMode)PlayerPrefs.GetInt(DeathModeKey, (int)DeathDisplayMode.Off);
        timerMode = (TimerDisplayMode)PlayerPrefs.GetInt(TimerModeKey, (int)TimerDisplayMode.Total);
        deathMode = (DeathDisplayMode)PlayerPrefs.GetInt(DeathModeKey, (int)DeathDisplayMode.Total);
        //petit test pour voir si ça fonctionne
        hudCorner = (HudCorner)PlayerPrefs.GetInt(HudCornerKey, (int)HudCorner.TopLeft);
        hudOpacity = PlayerPrefs.GetFloat(HudOpacityKey, defaultHudOpacity);
    }

    void ApplyAllVolumes()
    {
        ApplyVolume(masterParameter, masterVolume);
        ApplyVolume(musicParameter, musicVolume);
        ApplyVolume(voiceParameter, voiceVolume);
        ApplyVolume(ambienceParameter, ambienceVolume);
        ApplyVolume(sfxParameter, sfxVolume);
    }

    void ApplyVolume(string parameter, float value)
    {
        if (mixer == null) return;
        if (string.IsNullOrEmpty(parameter)) return;
        mixer.SetFloat(parameter, LinearToDecibels(value));
    }

    static float LinearToDecibels(float value)
    {
        if (value <= 0.0001f) return -80f;
        return Mathf.Log10(value) * 20f;
    }
}