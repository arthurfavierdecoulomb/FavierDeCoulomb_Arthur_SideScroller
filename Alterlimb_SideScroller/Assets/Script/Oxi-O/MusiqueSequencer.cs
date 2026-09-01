using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Segments")]
    [SerializeField] List<MusicSegment> segments = new List<MusicSegment>();

    [Header("Lecture")]
    [SerializeField] bool playOnStart = true;
    [SerializeField] string firstSegmentId = "1";
    [Range(0f, 1f)]
    [SerializeField] float volume = 0.8f;
    [SerializeField] float scheduleAheadTime = 1f;
    [SerializeField] float startDelay = 0.1f;

    [Header("Anti-doublon")]
    [SerializeField] bool ignoreIfSameSegmentAlreadyPlaying = true;

    [Header("Transition immédiate")]
    [SerializeField] float crossfadeDuration = 0.6f;

    [Header("Sourdine")]
    [Range(0f, 1f)]
    [SerializeField] float muffledFactor = 0.25f;
    [SerializeField] float muffleDuration = 0.4f;
    [SerializeField] float unmuffleDuration = 0.6f;

    [Header("Ralentissement")]
    [SerializeField] bool pitchDownOnMuffle = true;
    [Range(0.1f, 1f)]
    [SerializeField] float muffledPitch = 0.55f;
    [SerializeField] float pitchDownDuration = 0.9f;
    [SerializeField] float pitchUpDuration = 0.5f;
    [SerializeField] bool suspendSchedulingWhileMuffled = true;

    public event System.Action<string> OnSegmentStarted;

    public string CurrentSegmentId => currentIndex >= 0 && currentIndex < segments.Count ? segments[currentIndex].id : "";
    public bool IsPlaying => isPlaying;
    public bool IsMuffled => muffleFactor < 0.999f;

    AudioSource[] sources = new AudioSource[2];
    int sourceIndex;
    int currentIndex = -1;
    int queuedIndex = -1;
    double nextEventTime;
    bool isPlaying;
    bool schedulingSuspended;

    Coroutine fadeRoutine;
    Coroutine muffleRoutine;
    Coroutine pitchRoutine;

    float muffleFactor = 1f;
    float pitchFactor = 1f;

    void Awake()
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
            sources[i].volume = CurrentVolume();
            sources[i].pitch = CurrentPitch();
        }
    }

    void Start()
    {
        if (playOnStart)
            Play(firstSegmentId);
    }

    void Update()
    {
        if (!isPlaying || schedulingSuspended) return;

        if (AudioSettings.dspTime > nextEventTime - scheduleAheadTime)
            ScheduleNext();
    }

    public void Play(string id)
    {
        int index = IndexOf(id);
        if (index < 0) return;

        if (ignoreIfSameSegmentAlreadyPlaying && isPlaying && currentIndex == index)
            return;

        StopImmediate();

        currentIndex = index;
        sourceIndex = 0;
        queuedIndex = -1;
        nextEventTime = AudioSettings.dspTime + startDelay;

        sources[0].clip = segments[index].clip;
        sources[0].volume = CurrentVolume();
        sources[0].pitch = CurrentPitch();
        sources[0].PlayScheduled(nextEventTime);

        nextEventTime += ClipDuration(segments[index].clip);
        isPlaying = true;

        OnSegmentStarted?.Invoke(segments[index].id);
    }

    public void QueueSegment(string id)
    {
        int index = IndexOf(id);
        if (index < 0) return;

        if (isPlaying && currentIndex == index && queuedIndex < 0)
            return;

        if (!isPlaying)
        {
            Play(id);
            return;
        }

        queuedIndex = index;
    }

    public void PlayImmediate(string id)
    {
        int index = IndexOf(id);
        if (index < 0) return;

        if (ignoreIfSameSegmentAlreadyPlaying && isPlaying && currentIndex == index)
            return;

        StartCoroutine(CrossfadeRoutine(index));
    }

    public void ForcePlay(string id)
    {
        int index = IndexOf(id);
        if (index < 0) return;

        StopImmediate();

        currentIndex = index;
        sourceIndex = 0;
        queuedIndex = -1;
        nextEventTime = AudioSettings.dspTime + startDelay;

        sources[0].clip = segments[index].clip;
        sources[0].volume = CurrentVolume();
        sources[0].pitch = CurrentPitch();
        sources[0].PlayScheduled(nextEventTime);

        nextEventTime += ClipDuration(segments[index].clip);
        isPlaying = true;

        OnSegmentStarted?.Invoke(segments[index].id);
    }

    public void AdvanceToNext()
    {
        if (currentIndex < 0 || currentIndex + 1 >= segments.Count) return;
        QueueSegment(segments[currentIndex + 1].id);
    }

    public void FadeOutAndStop(float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        ApplyAudio();
    }

    public void MuffleMusic()
    {
        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(muffledFactor, muffleDuration));

        if (!pitchDownOnMuffle)
            return;

        if (suspendSchedulingWhileMuffled)
            schedulingSuspended = true;

        if (pitchRoutine != null) StopCoroutine(pitchRoutine);
        pitchRoutine = StartCoroutine(PitchRoutine(muffledPitch, pitchDownDuration, false));
    }

    public void MuffleMusic(float volumeFactor, float pitch, float duration)
    {
        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(Mathf.Clamp01(volumeFactor), Mathf.Max(0.01f, duration)));
        if (suspendSchedulingWhileMuffled)
            schedulingSuspended = true;
        if (pitchRoutine != null) StopCoroutine(pitchRoutine);
        pitchRoutine = StartCoroutine(PitchRoutine(Mathf.Clamp(pitch, 0.05f, 3f), Mathf.Max(0.01f, duration), false));
    }

    public void UnmuffleMusic()
    {
        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(1f, unmuffleDuration));

        if (!pitchDownOnMuffle)
            return;

        if (pitchRoutine != null) StopCoroutine(pitchRoutine);
        pitchRoutine = StartCoroutine(PitchRoutine(1f, pitchUpDuration, true));
    }

    public void SetPitchImmediate(float value)
    {
        if (pitchRoutine != null) StopCoroutine(pitchRoutine);

        pitchFactor = Mathf.Clamp(value, 0.05f, 3f);
        ApplyAudio();

        if (Mathf.Approximately(pitchFactor, 1f))
        {
            ResyncSchedule();
            schedulingSuspended = false;
        }
        else if (suspendSchedulingWhileMuffled)
        {
            schedulingSuspended = true;
        }
    }

    IEnumerator MuffleRoutine(float target, float duration)
    {
        float start = muffleFactor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            muffleFactor = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            ApplyAudio();
            yield return null;
        }

        muffleFactor = target;
        ApplyAudio();
        muffleRoutine = null;
    }

    IEnumerator PitchRoutine(float target, float duration, bool resyncAtEnd)
    {
        float start = pitchFactor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            pitchFactor = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            ApplyAudio();
            yield return null;
        }

        pitchFactor = target;
        ApplyAudio();

        if (resyncAtEnd)
        {
            ResyncSchedule();
            schedulingSuspended = false;
        }

        pitchRoutine = null;
    }

    void ResyncSchedule()
    {
        AudioSource source = sources[sourceIndex];

        if (source == null || source.clip == null || !source.isPlaying)
            return;

        double remaining = source.clip.length - source.time;
        nextEventTime = AudioSettings.dspTime + System.Math.Max(0.05d, remaining);
    }

    float CurrentVolume() => volume * muffleFactor;
    float CurrentPitch() => pitchFactor;

    void ApplyAudio()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;

            sources[i].volume = CurrentVolume();
            sources[i].pitch = CurrentPitch();
        }
    }

    void ScheduleNext()
    {
        int next;

        if (queuedIndex >= 0)
        {
            next = queuedIndex;
            queuedIndex = -1;
        }
        else if (segments[currentIndex].loop)
        {
            next = currentIndex;
        }
        else if (segments[currentIndex].autoChainToNext && currentIndex + 1 < segments.Count)
        {
            next = currentIndex + 1;
        }
        else
        {
            isPlaying = false;
            return;
        }

        sourceIndex = 1 - sourceIndex;
        AudioSource source = sources[sourceIndex];
        source.clip = segments[next].clip;
        source.volume = CurrentVolume();
        source.pitch = CurrentPitch();
        source.PlayScheduled(nextEventTime);

        nextEventTime += ClipDuration(segments[next].clip);

        bool changed = next != currentIndex;
        currentIndex = next;

        if (changed)
            OnSegmentStarted?.Invoke(segments[next].id);
    }

    IEnumerator CrossfadeRoutine(int index)
    {
        AudioSource oldSource = sources[sourceIndex];

        sourceIndex = 1 - sourceIndex;
        AudioSource newSource = sources[sourceIndex];

        newSource.clip = segments[index].clip;
        newSource.volume = 0f;
        newSource.pitch = CurrentPitch();
        newSource.Play();

        currentIndex = index;
        queuedIndex = -1;
        nextEventTime = AudioSettings.dspTime + ClipDuration(segments[index].clip);
        isPlaying = true;

        OnSegmentStarted?.Invoke(segments[index].id);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, crossfadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            newSource.volume = CurrentVolume() * t;
            oldSource.volume = CurrentVolume() * (1f - t);
            yield return null;
        }

        newSource.volume = CurrentVolume();
        oldSource.Stop();
        oldSource.volume = CurrentVolume();
    }

    IEnumerator FadeOutRoutine(float duration)
    {
        float elapsed = 0f;
        float startFactor = muffleFactor;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            muffleFactor = startFactor * (1f - Mathf.Clamp01(elapsed / duration));
            ApplyAudio();
            yield return null;
        }

        StopImmediate();

        muffleFactor = 1f;
        pitchFactor = 1f;
        schedulingSuspended = false;
        ApplyAudio();

        fadeRoutine = null;
    }

    void StopImmediate()
    {
        isPlaying = false;
        queuedIndex = -1;

        for (int i = 0; i < sources.Length; i++)
            sources[i].Stop();
    }

    int IndexOf(string id)
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i].id == id) return i;

        Debug.LogWarning($"[BossMusicSequencer] Aucun segment avec l'id '{id}'.");
        return -1;
    }

    double ClipDuration(AudioClip clip)
    {
        if (clip == null) return 0d;
        return (double)clip.samples / clip.frequency;
    }
}