using UnityEngine;
using UnityEngine.Audio;

public enum UiSoundKind { Click, Arrow, Close, Reset, Back, Hover }

public class UiAudio : MonoBehaviour
{
    public static UiAudio Instance { get; private set; }

    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup uiGroup;
    [Range(0, 256)]
    [SerializeField] int priority = 0;

    [Header("Persistance")]
    [SerializeField] bool persistAcrossScenes = true;

    [Header("Panneau")]
    [SerializeField] AudioClip[] panelOpenClips;
    [SerializeField] AudioClip[] panelCloseClips;
    [Range(0f, 1f)]
    [SerializeField] float panelVolume = 0.8f;

    [Header("Boutons")]
    [SerializeField] AudioClip[] clickClips;
    [SerializeField] AudioClip[] arrowClips;
    [SerializeField] AudioClip[] closeClips;
    [SerializeField] AudioClip[] resetClips;
    [SerializeField] AudioClip[] backClips;
    [SerializeField] AudioClip[] hoverClips;
    [Range(0f, 1f)]
    [SerializeField] float buttonVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] float hoverVolume = 0.4f;

    [Header("Variation")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.98f, 1.02f);

    AudioSource source;
    AudioClip lastClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = priority;
        source.ignoreListenerPause = true;
        source.ignoreListenerVolume = false;
        source.outputAudioMixerGroup = uiGroup;
    }

    void Start()
    {
        if (uiGroup == null)
            Debug.LogError("[UiAudio] Ui Group non assigné : les sons d'interface ne passeront pas par le mixer.", this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Play(UiSoundKind kind)
    {
        if (Instance != null) Instance.PlayKind(kind);
    }

    public static void PlayPanelOpen()
    {
        if (Instance != null) Instance.PlayClips(Instance.panelOpenClips, Instance.panelVolume);
    }

    public static void PlayPanelClose()
    {
        if (Instance != null) Instance.PlayClips(Instance.panelCloseClips, Instance.panelVolume);
    }

    void PlayKind(UiSoundKind kind)
    {
        switch (kind)
        {
            case UiSoundKind.Arrow: PlayClips(arrowClips, buttonVolume); break;
            case UiSoundKind.Close: PlayClips(closeClips, buttonVolume); break;
            case UiSoundKind.Reset: PlayClips(resetClips, buttonVolume); break;
            case UiSoundKind.Back: PlayClips(backClips, buttonVolume); break;
            case UiSoundKind.Hover: PlayClips(hoverClips, hoverVolume); break;
            default: PlayClips(clickClips, buttonVolume); break;
        }
    }

    void PlayClips(AudioClip[] clips, float volume)
    {
        if (source == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastClip = clip;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume);
    }
}