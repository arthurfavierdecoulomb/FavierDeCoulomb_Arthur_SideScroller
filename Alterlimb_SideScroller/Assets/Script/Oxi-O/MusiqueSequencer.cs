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

    [Header("Transition immédiate")]
    [SerializeField] float crossfadeDuration = 0.6f;

    [Header("Sourdine")]
    [Range(0f, 1f)]
    [SerializeField] float muffledFactor = 0.25f;
    [SerializeField] float muffleDuration = 0.4f;
    [SerializeField] float unmuffleDuration = 0.6f;

    public event System.Action<string> OnSegmentStarted;

    public string CurrentSegmentId => currentIndex >= 0 && currentIndex < segments.Count ? segments[currentIndex].id : "";
    public bool IsPlaying => isPlaying;

    AudioSource[] sources = new AudioSource[2];
    int sourceIndex;
    int currentIndex = -1;
    int queuedIndex = -1;

    double nextEventTime;
    bool isPlaying;
    Coroutine fadeRoutine;
    Coroutine muffleRoutine;
    float muffleFactor = 1f;

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
        }
    }

    void Start()
    {
        if (playOnStart)
            Play(firstSegmentId);
    }

    void Update()
    {
        if (!isPlaying) return;

        if (AudioSettings.dspTime > nextEventTime - scheduleAheadTime)
            ScheduleNext();
    }

    public void Play(string id)
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
        sources[0].PlayScheduled(nextEventTime);

        nextEventTime += ClipDuration(segments[index].clip);
        isPlaying = true;

        OnSegmentStarted?.Invoke(segments[index].id);
    }

    public void QueueSegment(string id)
    {
        int index = IndexOf(id);
        if (index < 0) return;

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

        StartCoroutine(CrossfadeRoutine(index));
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
        ApplyVolume();
    }

    public void MuffleMusic()
    {
        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(muffledFactor, muffleDuration));
    }

    public void UnmuffleMusic()
    {
        if (muffleRoutine != null) StopCoroutine(muffleRoutine);
        muffleRoutine = StartCoroutine(MuffleRoutine(1f, unmuffleDuration));
    }

    IEnumerator MuffleRoutine(float target, float duration)
    {
        float start = muffleFactor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            muffleFactor = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            ApplyVolume();
            yield return null;
        }

        muffleFactor = target;
        ApplyVolume();
        muffleRoutine = null;
    }

    float CurrentVolume() => volume * muffleFactor;

    void ApplyVolume()
    {
        for (int i = 0; i < sources.Length; i++)
            if (sources[i] != null) sources[i].volume = CurrentVolume();
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
            ApplyVolume();
            yield return null;
        }

        StopImmediate();

        muffleFactor = 1f;
        ApplyVolume();

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