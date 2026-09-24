using System.Collections.Generic;
using UnityEngine;

public class OxiOMusicDirector : MonoBehaviour
{
    public static OxiOMusicDirector Instance { get; private set; }

    private enum Move
    {
        Play,
        Crossfade,
        Queue
    }

    [System.Serializable]
    public class DialogueCue
    {
        public string sequenceId;
        public int lineIndex;
        public string segmentId;
    }

    [Header("Références")]
    [SerializeField] private BossMusicSequencer sequencer;
    [SerializeField] private OxiOBossDirector director;
    [SerializeField] private OxiOCore core;
    [SerializeField] private OxiO_Animation oxiAnimation;
    [SerializeField] private BossDialogueManager dialogue;

    [Header("Segments")]
    [SerializeField] private string bossRoomSegmentId = "boss_decouverte";
    [SerializeField] private string transformationSegmentId = "euphorie_transform";
    [SerializeField] private string[] segmentIdByRemainingCores = { "core_0", "core_1", "core_2", "core_3", "core_4" };

    [Header("Musiques de dialogue")]
    [SerializeField]
    private List<DialogueCue> dialogueCues = new List<DialogueCue>
    {
        new DialogueCue { sequenceId = "boss_intro", lineIndex = 5, segmentId = "oxi_vérité" },
        new DialogueCue { sequenceId = "dialogue_interlude", lineIndex = 0, segmentId = "dialogue_interlude" }
    };

    [Header("Structure du combat")]
    [SerializeField] private int phaseCount = 2;

    [Header("Mort du joueur")]
    [SerializeField] private bool duckOnDeath = true;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private BossMusicSequencer Sequencer => sequencer != null ? sequencer : BossMusicSequencer.Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        if (core == null)
            core = FindAnyObjectByType<OxiOCore>();

        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

        if (dialogue == null)
            dialogue = FindAnyObjectByType<BossDialogueManager>();
    }

    private void Start()
    {
        LogSetup();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (director != null)
            director.onFightStart.AddListener(HandleFightStart);

        if (core != null)
            core.OnCoreRemoved += HandleCoreRemoved;

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted += HandleTransformationStarted;
            oxiAnimation.OnTransformationComplete += HandleTransformationComplete;
            oxiAnimation.OnFinalFall += HandleFinalFall;
        }

        if (dialogue != null)
            dialogue.OnLineStarted += HandleLineStarted;

        CharaController.OnPlayerDied += HandlePlayerDied;
        SpawnManager.OnPlayerRespawn += HandlePlayerRespawn;
    }

    private void OnDisable()
    {
        if (director != null)
            director.onFightStart.RemoveListener(HandleFightStart);

        if (core != null)
            core.OnCoreRemoved -= HandleCoreRemoved;

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted -= HandleTransformationStarted;
            oxiAnimation.OnTransformationComplete -= HandleTransformationComplete;
            oxiAnimation.OnFinalFall -= HandleFinalFall;
        }

        if (dialogue != null)
            dialogue.OnLineStarted -= HandleLineStarted;

        CharaController.OnPlayerDied -= HandlePlayerDied;
        SpawnManager.OnPlayerRespawn -= HandlePlayerRespawn;
    }

    private void LogSetup()
    {
        if (Sequencer == null)
            Debug.LogError($"[OxiOMusicDirector] '{name}' : aucun BossMusicSequencer trouvé, aucune musique ne jouera.", this);

        if (director == null)
            Debug.LogError($"[OxiOMusicDirector] '{name}' : aucun OxiOBossDirector trouvé, la musique ne suivra pas le combat.", this);

        if (core == null)
            Debug.LogError($"[OxiOMusicDirector] '{name}' : aucun OxiOCore trouvé, la musique ne changera pas quand un noyau tombe.", this);

        if (oxiAnimation == null)
            Debug.LogError($"[OxiOMusicDirector] '{name}' : aucun OxiO_Animation trouvé, la musique de transformation ne se lancera pas.", this);

        if (dialogue == null && dialogueCues.Count > 0)
            Debug.LogError($"[OxiOMusicDirector] '{name}' : aucun BossDialogueManager trouvé, les musiques de dialogue ne se lanceront pas.", this);

        if (Sequencer == null)
            return;

        CheckSegment(bossRoomSegmentId, "Boss Room Segment Id");
        CheckSegment(transformationSegmentId, "Transformation Segment Id");

        for (int i = 0; i < segmentIdByRemainingCores.Length; i++)
            CheckSegment(segmentIdByRemainingCores[i], $"Segment Id By Remaining Cores [{i}]");

        for (int i = 0; i < dialogueCues.Count; i++)
        {
            DialogueCue cue = dialogueCues[i];

            if (cue == null)
                continue;

            CheckSegment(cue.segmentId, $"Dialogue Cues [{i}] ('{cue.sequenceId}', réplique {cue.lineIndex})");
        }
    }

    private void CheckSegment(string id, string field)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[OxiOMusicDirector] '{name}' : le champ '{field}' est vide.", this);
            return;
        }

        if (!Sequencer.HasSegment(id))
            Debug.LogError($"[OxiOMusicDirector] '{name}' : '{field}' = '{id}', mais ce segment n'existe pas dans le BossMusicSequencer.", this);
    }

    public void EnterBossRoom()
    {
        Request(bossRoomSegmentId, Move.Play, "entrée dans la salle du boss");
    }

    private void HandleLineStarted(string sequenceId, int lineIndex)
    {
        foreach (DialogueCue cue in dialogueCues)
        {
            if (cue == null || cue.sequenceId != sequenceId || cue.lineIndex != lineIndex)
                continue;

            Request(cue.segmentId, Move.Crossfade, $"réplique {lineIndex} de '{sequenceId}'");
        }
    }

    private void HandleFightStart()
    {
        int remaining = RemainingCores();

        if (remaining <= 0)
            return;

        Request(SegmentFor(remaining), Move.Crossfade, $"début de combat, phase {director.CurrentPhase}, {remaining} noyau(x) en vie");
    }

    private void HandleCoreRemoved()
    {
        int remaining = RemainingCores();

        if (remaining < 0)
            return;

        if (remaining == 0)
        {
            Log("dernier noyau coupé : la musique de victoire attend la chute d'Oxi-O.");
            return;
        }

        if (core.PhaseDepleted)
        {
            Log("noyau coupé, fin de phase : la musique attend le dialogue d'interlude.");
            return;
        }

        Request(SegmentFor(remaining), Move.Crossfade, $"noyau coupé, {remaining} en vie");
    }

    private void HandleFinalFall()
    {
        Request(SegmentFor(0), Move.Crossfade, "chute d'Oxi-O, victoire");
    }

    private void HandleTransformationStarted()
    {
        Request(transformationSegmentId, Move.Crossfade, "début de la transformation");
    }

    private void HandleTransformationComplete()
    {
        int remaining = RemainingCores();

        if (remaining <= 0)
            return;

        Request(SegmentFor(remaining), Move.Crossfade, $"fin de la transformation, {remaining} noyau(x) en vie");
    }

    private void HandlePlayerDied()
    {
        if (duckOnDeath && Sequencer != null)
            Sequencer.SetDeathDuck(true);
    }

    private void HandlePlayerRespawn()
    {
        if (Sequencer != null)
            Sequencer.SetDeathDuck(false);
    }

    private int RemainingCores()
    {
        if (director == null || core == null)
            return -1;

        int perPhase = core.CutsDoneThisPhase + core.CutsRemainingThisPhase;
        int total = phaseCount * perPhase;
        int done = (director.CurrentPhase - 1) * perPhase + core.CutsDoneThisPhase;

        return Mathf.Max(0, total - done);
    }

    private string SegmentFor(int remaining)
    {
        if (segmentIdByRemainingCores == null || segmentIdByRemainingCores.Length == 0)
            return "";

        int index = Mathf.Clamp(remaining, 0, segmentIdByRemainingCores.Length - 1);
        return segmentIdByRemainingCores[index];
    }

    private void Request(string id, Move move, string reason)
    {
        BossMusicSequencer target = Sequencer;

        if (target == null || string.IsNullOrEmpty(id))
            return;

        if (target.CurrentSegmentId == id || target.QueuedSegmentId == id)
        {
            Log($"{reason} → '{id}' déjà en cours ou en attente, rien à faire.");
            return;
        }

        Log($"{reason} → '{id}' ({move}).");

        if (move == Move.Crossfade)
            target.PlayImmediate(id);
        else if (move == Move.Queue)
            target.QueueSegment(id);
        else
            target.Play(id);
    }

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[OxiOMusicDirector] {message}", this);
    }
}