using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PauseAudioManager : MonoBehaviour
{
    public static PauseAudioManager Instance { get; private set; }

    [Header("Musique du boss")]
    [SerializeField] private BossMusicSequencer bossMusic;
    [SerializeField] private bool useSequencerMuffle = true;
    [SerializeField] private bool restoreMusicOnResume = true;

    [Header("Autres musiques à ralentir")]
    [SerializeField] private List<AudioSource> pitchDownSources = new List<AudioSource>();
    [Range(0.1f, 1f)]
    [SerializeField] private float pausedPitch = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] private float pausedVolumeFactor = 0.5f;
    [SerializeField] private float blendDuration = 0.35f;

    [Header("Sons coupés pendant la pause")]
    [SerializeField] private bool pauseAllOtherSources = true;
    [SerializeField] private List<AudioSource> alwaysPause = new List<AudioSource>();
    [SerializeField] private List<AudioSource> neverPause = new List<AudioSource>();

    [Header("Détection")]
    [SerializeField] private bool autoDetectTimeScale = false;
    [SerializeField] private bool ignoreHitStop = true;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = false;

    public bool IsPaused { get; private set; }

    private readonly List<AudioSource> suspended = new List<AudioSource>();
    private readonly Dictionary<AudioSource, float> basePitch = new Dictionary<AudioSource, float>();
    private readonly Dictionary<AudioSource, float> baseVolume = new Dictionary<AudioSource, float>();

    private Coroutine blendRoutine;
    private bool musicMuffledByPause;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (bossMusic == null)
            bossMusic = BossMusicSequencer.Instance;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!autoDetectTimeScale)
            return;

        bool paused = Time.timeScale == 0f;

        if (paused && ignoreHitStop && CameraShake.HitStopActive)
            paused = false;

        if (paused == IsPaused)
            return;

        IsPaused = paused;

        if (paused)
            ApplyPause();
        else
            ApplyResume();
    }

    public void EnterPause()
    {
        if (IsPaused)
            return;

        IsPaused = true;
        ApplyPause();
    }

    public void ExitPause()
    {
        if (!IsPaused)
            return;

        IsPaused = false;
        ApplyResume();
    }

    private void ApplyPause()
    {
        if (bossMusic == null)
            bossMusic = BossMusicSequencer.Instance;

        if (useSequencerMuffle && bossMusic != null && !bossMusic.IsMuffled)
        {
            bossMusic.MuffleMusic(pausedVolumeFactor, pausedPitch, blendDuration);
            musicMuffledByPause = true;
        }

        SuspendSources();

        if (blendRoutine != null)
            StopCoroutine(blendRoutine);

        blendRoutine = StartCoroutine(BlendRoutine(true));

        if (logDiagnostics)
            Debug.Log($"[PauseAudioManager] Pause : {suspended.Count} source(s) suspendue(s).", this);
    }

    private void ApplyResume()
    {
        if (musicMuffledByPause && restoreMusicOnResume && bossMusic != null)
            bossMusic.UnmuffleMusic();

        musicMuffledByPause = false;

        ResumeSources();

        if (blendRoutine != null)
            StopCoroutine(blendRoutine);

        blendRoutine = StartCoroutine(BlendRoutine(false));
    }

    private void SuspendSources()
    {
        suspended.Clear();

        foreach (AudioSource source in alwaysPause)
            TrySuspend(source);

        if (!pauseAllOtherSources)
            return;

        AudioSource[] all = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);

        foreach (AudioSource source in all)
            TrySuspend(source);
    }

    private void TrySuspend(AudioSource source)
    {
        if (source == null || suspended.Contains(source))
            return;

        if (neverPause.Contains(source))
            return;

        if (pitchDownSources.Contains(source))
            return;

        if (bossMusic != null && source.gameObject == bossMusic.gameObject)
            return;

        if (!source.isPlaying)
            return;

        source.Pause();
        suspended.Add(source);
    }

    private void ResumeSources()
    {
        foreach (AudioSource source in suspended)
            if (source != null)
                source.UnPause();

        suspended.Clear();
    }

    private IEnumerator BlendRoutine(bool toPaused)
    {
        CacheBaseValues();

        float duration = Mathf.Max(0.01f, blendDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            ApplyBlend(toPaused ? t : 1f - t);

            yield return null;
        }

        ApplyBlend(toPaused ? 1f : 0f);
        blendRoutine = null;
    }

    private void CacheBaseValues()
    {
        foreach (AudioSource source in pitchDownSources)
        {
            if (source == null)
                continue;

            if (!basePitch.ContainsKey(source))
                basePitch[source] = source.pitch;

            if (!baseVolume.ContainsKey(source))
                baseVolume[source] = source.volume;
        }
    }

    private void ApplyBlend(float amount)
    {
        foreach (AudioSource source in pitchDownSources)
        {
            if (source == null)
                continue;

            float pitch = basePitch.ContainsKey(source) ? basePitch[source] : 1f;
            float volume = baseVolume.ContainsKey(source) ? baseVolume[source] : 1f;

            source.pitch = Mathf.Lerp(pitch, pitch * pausedPitch, amount);
            source.volume = Mathf.Lerp(volume, volume * pausedVolumeFactor, amount);
        }
    }
}