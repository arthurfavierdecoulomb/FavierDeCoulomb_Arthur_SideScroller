using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BossMusicSequencer : MonoBehaviour
{
    public static BossMusicSequencer Instance { get; private set; }

    [System.Serializable]
    public class MusicSegment
    {
        public string id;
        public AudioClip clip;
        public bool loop = true;
        public bool autoChainToNext;
    }

    [System.Serializable]
    public class DuckSettings
    {
        [Range(0f, 1f)] public float volumeFactor = 0.25f;
        [Range(0.1f, 1f)] public float pitch = 0.55f;
        public float inDuration = 0.9f;
        public float outDuration = 0.6f;
    }

    [Header("Segments")]
    [SerializeField] private List<MusicSegment> segments = new List<MusicSegment>();

    [Header("Sortie")]
    [SerializeField] private AudioMixerGroup outputGroup;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Enchaînement")]
    [SerializeField] private float scheduleAheadTime = 1f;
    [SerializeField] private float startDelay = 0.1f;
    [SerializeField] private float crossfadeDuration = 0.6f;

    [Header("Sourdine à la mort")]
    [SerializeField] private DuckSettings deathDuck = new DuckSettings { volumeFactor = 0.25f, pitch = 0.55f, inDuration = 0.9f, outDuration = 0.6f };

    [Header("Sourdine en pause")]
    [SerializeField] private DuckSettings pauseDuck = new DuckSettings { volumeFactor = 0.5f, pitch = 0.6f, inDuration = 0.35f, outDuration = 0.35f };

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public event System.Action<string> OnSegmentStarted;
    public event System.Action<string> OnSegmentFinished;

    public string CurrentSegmentId => IdAt(currentIndex);
    public string QueuedSegmentId => pendingSource >= 0 && pendingIndex != currentIndex ? IdAt(pendingIndex) : IdAt(queuedIndex);
    public bool IsPlaying => isPlaying;
    public bool IsDucked => deathActive || pauseActive || deathWeight > 0f || pauseWeight > 0f;

    private readonly AudioSource[] sources = new AudioSource[2];
    private readonly float[] sourceGains = { 1f, 1f };

    private int activeSource;
    private int currentIndex = -1;
    private int queuedIndex = -1;
    private bool isPlaying;
    private bool stopAtEnd;
    private double currentEndTime;

    private int pendingSource = -1;
    private int pendingIndex = -1;
    private double pendingStartTime;

    private bool deathActive;
    private bool pauseActive;
    private float deathWeight;
    private float pauseWeight;
    private bool schedulingSuspended;
    private float masterFade = 1f;

    private Coroutine crossfadeRoutine;
    private Coroutine fadeOutRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].loop = false;
            sources[i].spatialBlend = 0f;
            sources[i].outputAudioMixerGroup = outputGroup;
        }

        ApplyAudio();
        LogSetup();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LogSetup()
    {
        if (outputGroup == null)
            Debug.LogError($"[BossMusicSequencer] '{name}' : Output Group vide. Assigne le groupe Music du MainMixer, sinon le réglage de volume musique du joueur n'aura aucun effet.", this);

        HashSet<string> seen = new HashSet<string>();

        foreach (MusicSegment segment in segments)
        {
            if (segment == null)
                continue;

            if (string.IsNullOrEmpty(segment.id))
                Debug.LogError($"[BossMusicSequencer] '{name}' : un segment n'a pas d'id.", this);
            else if (!seen.Add(segment.id))
                Debug.LogError($"[BossMusicSequencer] '{name}' : l'id '{segment.id}' existe en double.", this);

            if (segment.clip == null)
                Debug.LogError($"[BossMusicSequencer] '{name}' : le segment '{segment.id}' n'a pas de clip.", this);
        }
    }

    private void Update()
    {
        UpdateDucks();
        ApplyAudio();
        CommitPendingIfStarted();

        if (!isPlaying)
            return;

        if (stopAtEnd)
        {
            if (!sources[activeSource].isPlaying)
                FinishPlayback();

            return;
        }

        if (schedulingSuspended)
        {
            KeepAliveWhileDucked();
            return;
        }

        if (crossfadeRoutine != null || pendingSource >= 0)
            return;

        if (AudioSettings.dspTime > currentEndTime - scheduleAheadTime)
            ScheduleNext();
    }

    public bool HasSegment(string id)
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i] != null && segments[i].id == id)
                return true;

        return false;
    }

    public void Play(string id)
    {
        int index = IndexOf(id);

        if (index < 0 || (isPlaying && index == currentIndex))
            return;

        StopFadeOut();
        StopEverything();
        StartOn(0, index, AudioSettings.dspTime + startDelay, true);
    }

    public void PlayImmediate(string id)
    {
        int index = IndexOf(id);

        if (index < 0 || (isPlaying && index == currentIndex))
            return;

        StopFadeOut();
        CancelPending();
        StopCrossfade();

        queuedIndex = -1;

        int from = activeSource;
        int to = 1 - activeSource;

        PrepareSource(to, index);
        sourceGains[to] = 0f;
        ApplyAudio();
        sources[to].Play();

        activeSource = to;
        currentIndex = index;
        currentEndTime = AudioSettings.dspTime + ClipDuration(segments[index].clip);
        isPlaying = true;
        stopAtEnd = false;

        Announce();

        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(from, to));
    }

    public void QueueSegment(string id)
    {
        int index = IndexOf(id);

        if (index < 0)
            return;

        if (!isPlaying)
        {
            Play(id);
            return;
        }

        if (index == queuedIndex || (pendingSource >= 0 && index == pendingIndex))
            return;

        if (index == currentIndex && queuedIndex < 0 && pendingSource < 0 && !stopAtEnd)
            return;

        CancelPending();
        queuedIndex = index;
        stopAtEnd = false;
    }

    public void FadeOutAndStop(float duration)
    {
        StopFadeOut();
        fadeOutRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, duration)));
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        ApplyAudio();
    }

    public void SetDeathDuck(bool active)
    {
        if (deathActive == active)
            return;

        deathActive = active;
        OnDuckChanged(active, "mort");
    }

    public void SetPauseDuck(bool active)
    {
        if (pauseActive == active)
            return;

        pauseActive = active;
        OnDuckChanged(active, "pause");
    }

    private void OnDuckChanged(bool active, string reason)
    {
        if (active && !schedulingSuspended)
        {
            schedulingSuspended = true;
            CancelPending();
        }

        Log($"sourdine '{reason}' {(active ? "activée" : "relâchée")}.");
    }

    private void UpdateDucks()
    {
        deathWeight = StepWeight(deathWeight, deathActive, deathDuck);
        pauseWeight = StepWeight(pauseWeight, pauseActive, pauseDuck);

        if (schedulingSuspended && !deathActive && !pauseActive && deathWeight <= 0f && pauseWeight <= 0f)
            ResumeScheduling();
    }

    private float StepWeight(float weight, bool active, DuckSettings settings)
    {
        float target = active ? 1f : 0f;
        float duration = active ? settings.inDuration : settings.outDuration;
        float step = duration <= 0f ? 1f : Time.unscaledDeltaTime / duration;

        return Mathf.MoveTowards(weight, target, step);
    }

    private void ResumeScheduling()
    {
        schedulingSuspended = false;

        AudioSource source = sources[activeSource];

        if (!isPlaying || source.clip == null || !source.isPlaying)
            return;

        double remaining = (double)(source.clip.samples - source.timeSamples) / source.clip.frequency;
        currentEndTime = AudioSettings.dspTime + System.Math.Max(0.05d, remaining);
    }

    private void KeepAliveWhileDucked()
    {
        if (sources[activeSource].isPlaying)
            return;

        int next = ConsumeNextIndex();

        if (next < 0)
        {
            FinishPlayback();
            return;
        }

        StartOn(activeSource, next, AudioSettings.dspTime, next != currentIndex);
    }

    private void ApplyAudio()
    {
        float duckVolume = Mathf.Lerp(1f, deathDuck.volumeFactor, deathWeight) * Mathf.Lerp(1f, pauseDuck.volumeFactor, pauseWeight);
        float duckPitch = Mathf.Lerp(1f, deathDuck.pitch, deathWeight) * Mathf.Lerp(1f, pauseDuck.pitch, pauseWeight);
        float baseVolume = volume * duckVolume * masterFade;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].volume = baseVolume * sourceGains[i];
            sources[i].pitch = duckPitch;
        }
    }

    private int ConsumeNextIndex()
    {
        if (queuedIndex >= 0)
        {
            int queued = queuedIndex;
            queuedIndex = -1;
            return queued;
        }

        if (currentIndex < 0 || currentIndex >= segments.Count)
            return -1;

        MusicSegment current = segments[currentIndex];

        if (current.loop)
            return currentIndex;

        if (current.autoChainToNext && currentIndex + 1 < segments.Count)
            return currentIndex + 1;

        return -1;
    }

    private void ScheduleNext()
    {
        int next = ConsumeNextIndex();

        if (next < 0)
        {
            stopAtEnd = true;
            return;
        }

        int target = 1 - activeSource;

        PrepareSource(target, next);
        sources[target].PlayScheduled(currentEndTime);

        pendingSource = target;
        pendingIndex = next;
        pendingStartTime = currentEndTime;
    }

    private void CommitPendingIfStarted()
    {
        if (pendingSource < 0 || AudioSettings.dspTime < pendingStartTime)
            return;

        bool changed = pendingIndex != currentIndex;

        activeSource = pendingSource;
        currentIndex = pendingIndex;
        currentEndTime = pendingStartTime + ClipDuration(segments[currentIndex].clip);

        pendingSource = -1;
        pendingIndex = -1;

        if (changed)
            Announce();
    }

    private void CancelPending()
    {
        CommitPendingIfStarted();

        if (pendingSource < 0)
            return;

        sources[pendingSource].Stop();

        if (pendingIndex != currentIndex && queuedIndex < 0)
            queuedIndex = pendingIndex;

        pendingSource = -1;
        pendingIndex = -1;
    }

    private void StartOn(int sourceSlot, int index, double startTime, bool announce)
    {
        PrepareSource(sourceSlot, index);
        sources[sourceSlot].PlayScheduled(startTime);

        activeSource = sourceSlot;
        currentIndex = index;
        currentEndTime = startTime + ClipDuration(segments[index].clip);
        isPlaying = true;
        stopAtEnd = false;

        if (announce)
            Announce();
    }

    private void PrepareSource(int sourceSlot, int index)
    {
        sources[sourceSlot].clip = segments[index].clip;
        sourceGains[sourceSlot] = 1f;
        ApplyAudio();
    }

    private IEnumerator CrossfadeRoutine(int from, int to)
    {
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            sourceGains[to] = t;
            sourceGains[from] = 1f - t;

            yield return null;
        }

        sources[from].Stop();
        sourceGains[from] = 1f;
        sourceGains[to] = 1f;

        crossfadeRoutine = null;
    }

    private void StopCrossfade()
    {
        if (crossfadeRoutine == null)
            return;

        StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = null;

        int other = 1 - activeSource;

        sources[other].Stop();
        sourceGains[other] = 1f;
        sourceGains[activeSource] = 1f;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            masterFade = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        fadeOutRoutine = null;
        StopEverything();
        masterFade = 1f;

        Log("fondu de sortie terminé, musique arrêtée.");
    }

    private void StopFadeOut()
    {
        if (fadeOutRoutine == null)
            return;

        StopCoroutine(fadeOutRoutine);
        fadeOutRoutine = null;
        masterFade = 1f;
    }

    private void FinishPlayback()
    {
        string finished = CurrentSegmentId;

        Log($"'{finished}' terminé, plus rien à enchaîner.");
        StopEverything();

        OnSegmentFinished?.Invoke(finished);
    }

    private void StopEverything()
    {
        StopCrossfade();

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].Stop();
            sourceGains[i] = 1f;
        }

        pendingSource = -1;
        pendingIndex = -1;
        queuedIndex = -1;
        currentIndex = -1;
        isPlaying = false;
        stopAtEnd = false;
    }

    private void Announce()
    {
        string id = CurrentSegmentId;
        Log($"segment '{id}' lancé.");
        OnSegmentStarted?.Invoke(id);
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i] != null && segments[i].id == id)
                return i;

        Debug.LogWarning($"[BossMusicSequencer] '{name}' : aucun segment avec l'id '{id}'.", this);
        return -1;
    }

    private string IdAt(int index)
    {
        return index >= 0 && index < segments.Count && segments[index] != null ? segments[index].id : "";
    }

    private double ClipDuration(AudioClip clip)
    {
        if (clip == null)
            return 0d;

        return (double)clip.samples / clip.frequency;
    }

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[BossMusicSequencer] {message}", this);
    }
}