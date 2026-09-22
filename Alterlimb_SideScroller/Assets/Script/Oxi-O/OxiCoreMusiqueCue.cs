using UnityEngine;

public class OxiOCoreMusicCue : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private OxiOCore core;
    [SerializeField] private OxiOBossDirector director;

    [Header("Segments par noyau")]
    [SerializeField] private string[] segmentIdsByCore = { "core_4", "core_3", "core_2", "core_1" };
    [SerializeField] private int cutsPerPhase = 2;

    public enum PlayMode
    {
        Play,
        Crossfade,
        Queue
    }

    [Header("Mode de lecture")]
    [SerializeField] private PlayMode playMode = PlayMode.Crossfade;

    private void Awake()
    {
        if (core == null)
            core = GetComponent<OxiOCore>();

        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        if (core == null)
            Debug.LogError($"[OxiOCoreMusicCue] '{name}' : aucun OxiOCore trouvé.", this);

        if (director == null)
            Debug.LogError($"[OxiOCoreMusicCue] '{name}' : aucun OxiOBossDirector trouvé.", this);
    }

    private void OnEnable()
    {
        if (core != null)
            core.OnCoreRemoved += HandleCoreRemoved;
    }

    private void OnDisable()
    {
        if (core != null)
            core.OnCoreRemoved -= HandleCoreRemoved;
    }

    private void HandleCoreRemoved()
    {
        if (core == null || director == null)
            return;

        int index = (director.CurrentPhase - 1) * cutsPerPhase + (core.CutsDoneThisPhase - 1);

        if (index < 0 || index >= segmentIdsByCore.Length)
        {
            Debug.LogWarning($"[OxiOCoreMusicCue] '{name}' : index {index} hors de segmentIdsByCore (phase {director.CurrentPhase}, coupe {core.CutsDoneThisPhase}).", this);
            return;
        }

        string id = segmentIdsByCore[index];

        if (string.IsNullOrEmpty(id) || BossMusicSequencer.Instance == null)
            return;

        if (playMode == PlayMode.Crossfade)
            BossMusicSequencer.Instance.PlayImmediate(id);
        else if (playMode == PlayMode.Queue)
            BossMusicSequencer.Instance.QueueSegment(id);
        else
            BossMusicSequencer.Instance.Play(id);
    }
}