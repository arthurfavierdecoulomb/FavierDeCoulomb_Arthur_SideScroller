using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OxiOPhaseTransition : MonoBehaviour
{
    public enum MusicMode
    {
        Play,
        Crossfade,
        Queue
    }

    public enum ScreenReturn
    {
        AvantTransformation,
        ApresTransformation,
        Jamais
    }

    [System.Serializable]
    public class PhaseStep
    {
        public string label = "Phase 1 -> 2";
        public int fromPhase = 1;
        public string dialogueSequenceId = "oxio_phase2";
        public int nextPhase = 2;
        public bool isFinalPhase;

        [Header("Transformation")]
        public bool playTransformation = true;
        public float delayBeforeTransformation = 0.4f;
        public float delayAfterTransformation = 0.6f;

        [Header("Musique")]
        public string transformationMusicId = "boss_transformation";
        public MusicMode transformationMusicMode = MusicMode.Crossfade;
        public string phaseMusicId = "boss_euphorie";
        public MusicMode phaseMusicMode = MusicMode.Queue;

        [Header("Voix")]
        public AudioClip transformationVoice;
        public float voiceDelay = 0f;

        [Header("Écran suspendu")]
        public ScreenReturn screenReturn = ScreenReturn.ApresTransformation;

        public UnityEvent onTransitionStart;
        public UnityEvent onTransformationStart;
        public UnityEvent onTransitionEnd;
    }

    [Header("Références")]
    [SerializeField] private OxiOBossDirector director;
    [SerializeField] private OxiO_Animation oxiAnimation;
    [SerializeField] private OxiOScreenUI screenUI;
    [SerializeField] private BossDialogueManager dialogue;

    [Header("Transitions")]
    [SerializeField] private List<PhaseStep> steps = new List<PhaseStep>();

    [Header("Voix")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Caméra")]
    [SerializeField] private CameraFocus cameraFocus;
    [SerializeField] private string dialogueFocusId = "oxio";
    [SerializeField] private string transformationFocusId = "oxio";

    [Header("Branchement")]
    [SerializeField] private bool autoSubscribe = true;

    [Header("Rythme")]
    [SerializeField] private float delayAfterLastCut = 1.2f;
    [SerializeField] private bool waitForSlicedAnimation = true;
    [SerializeField] private float slicedTimeout = 6f;
    [SerializeField] private float delayBeforeDialogue = 0.6f;
    [SerializeField] private float delayBeforeNextPhase = 1f;

    [Header("Écran suspendu")]
    [SerializeField] private bool hideScreenDuringDialogue = true;
    [SerializeField] private float screenReturnDelay = 0.8f;

    [Header("Sécurité")]
    [SerializeField] private float dialogueTimeout = 120f;
    [SerializeField] private float transformationTimeout = 20f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public bool IsRunning { get; private set; }

    private string waitedSequenceId;
    private bool sequenceFinished;
    private Coroutine routine;

    private void Awake()
    {
        if (director == null)
            director = GetComponentInParent<OxiOBossDirector>();

        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        if (dialogue == null)
            dialogue = BossDialogueManager.Instance;

        LogSetup();
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (director == null)
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : aucun OxiOBossDirector trouvé, la transition ne se déclenchera jamais.", this);

        if (steps.Count == 0)
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : la liste Steps est vide. Ajoute au moins une entrée (From Phase 1 -> Next Phase 2).", this);

        if (oxiAnimation == null && waitForSlicedAnimation)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Oxi Animation non assigné, l'attente de l'animation sliced sera ignorée.", this);

        if (cameraFocus == null)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Camera Focus non assigné, la caméra restera sur Azu pendant le dialogue et la transformation.", this);

        if (screenUI == null && hideScreenDuringDialogue)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Screen UI non assigné, l'écran restera visible pendant le dialogue.", this);

        foreach (PhaseStep step in steps)
        {
            if (step == null)
                continue;

            if (!step.isFinalPhase && step.nextPhase <= step.fromPhase)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : l'étape '{step.label}' repart en phase {step.nextPhase} depuis la phase {step.fromPhase}. Boucle possible.", this);
        }
    }

    private void OnEnable()
    {
        if (autoSubscribe && director != null)
            director.onPhaseEnd.AddListener(RunTransition);

        if (dialogue == null)
            dialogue = BossDialogueManager.Instance;

        if (dialogue != null)
            dialogue.OnSequenceFinished += HandleSequenceFinished;
    }

    private void OnDisable()
    {
        if (autoSubscribe && director != null)
            director.onPhaseEnd.RemoveListener(RunTransition);

        if (dialogue != null)
            dialogue.OnSequenceFinished -= HandleSequenceFinished;
    }

    public void RunTransition()
    {
        if (IsRunning)
        {
            if (logDiagnostics)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : transition déjà en cours, appel ignoré. (Le director est-il branché deux fois ?)", this);

            return;
        }

        if (director == null)
            return;

        PhaseStep step = FindStep(director.CurrentPhase);

        if (step == null)
        {
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : aucune étape configurée pour la phase {director.CurrentPhase}. Le combat s'arrête ici.", this);
            return;
        }

        routine = StartCoroutine(TransitionRoutine(step));
    }

    private PhaseStep FindStep(int phase)
    {
        foreach (PhaseStep step in steps)
            if (step != null && step.fromPhase == phase)
                return step;

        return null;
    }

    private IEnumerator TransitionRoutine(PhaseStep step)
    {
        IsRunning = true;

        if (logDiagnostics)
            Debug.Log($"[OxiOPhaseTransition] Transition '{step.label}' : phase {step.fromPhase} terminée.", this);

        step.onTransitionStart?.Invoke();

        director.StopFight();

        if (cameraFocus != null)
            cameraFocus.FocusOn(dialogueFocusId);

        if (screenUI != null)
            screenUI.CancelEcoCountdown();

        if (delayAfterLastCut > 0f)
            yield return new WaitForSeconds(delayAfterLastCut);

        if (waitForSlicedAnimation && oxiAnimation != null)
        {
            float elapsed = 0f;

            while (oxiAnimation.IsSlicing && elapsed < slicedTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= slicedTimeout && logDiagnostics)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : l'animation sliced n'est jamais sortie de son état après {slicedTimeout}s. Vérifie qu'elle n'est pas en loop.", this);
        }

        if (hideScreenDuringDialogue && screenUI != null)
            screenUI.Hide();

        if (delayBeforeDialogue > 0f)
            yield return new WaitForSeconds(delayBeforeDialogue);

        yield return PlayDialogue(step.dialogueSequenceId);

        if (step.isFinalPhase)
        {
            if (cameraFocus != null)
                cameraFocus.ReleaseFocus();

            step.onTransitionEnd?.Invoke();
            IsRunning = false;
            routine = null;
            yield break;
        }

        if (step.screenReturn == ScreenReturn.AvantTransformation)
            yield return ReturnScreen();

        yield return PlayTransformation(step);

        if (step.screenReturn == ScreenReturn.ApresTransformation)
            yield return ReturnScreen();

        if (cameraFocus != null)
            cameraFocus.ReleaseFocus();

        director.SetPhase(step.nextPhase);

        if (delayBeforeNextPhase > 0f)
            yield return new WaitForSeconds(delayBeforeNextPhase);

        director.StartFight();

        step.onTransitionEnd?.Invoke();

        if (logDiagnostics)
            Debug.Log($"[OxiOPhaseTransition] Phase {step.nextPhase} lancée.", this);

        IsRunning = false;
        routine = null;
    }

    private IEnumerator ReturnScreen()
    {
        if (!hideScreenDuringDialogue || screenUI == null)
            yield break;

        screenUI.Show();

        if (screenReturnDelay > 0f)
            yield return new WaitForSeconds(screenReturnDelay);
    }

    private IEnumerator PlayTransformation(PhaseStep step)
    {
        if (step.delayBeforeTransformation > 0f)
            yield return new WaitForSeconds(step.delayBeforeTransformation);

        if (cameraFocus != null && step.playTransformation)
            cameraFocus.FocusOn(transformationFocusId);

        PlayMusic(step.transformationMusicId, step.transformationMusicMode);

        if (!step.playTransformation || oxiAnimation == null)
        {
            if (step.playTransformation && oxiAnimation == null)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Oxi Animation non assigné, la transformation est sautée.", this);

            PlayMusic(step.phaseMusicId, step.phaseMusicMode);
            yield break;
        }

        step.onTransformationStart?.Invoke();

        PlayVoice(step);

        bool done = false;
        System.Action handler = () => done = true;

        oxiAnimation.OnTransformationComplete += handler;
        oxiAnimation.TransformToEuphoria();

        float elapsed = 0f;

        while (!done && elapsed < transformationTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        oxiAnimation.OnTransformationComplete -= handler;

        if (!done)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : la transformation ne s'est jamais terminée après {transformationTimeout}s. Vérifie que l'état '{"Oxi_euphorie_transformation"}' n'est pas en loop.", this);

        PlayMusic(step.phaseMusicId, step.phaseMusicMode);

        if (step.delayAfterTransformation > 0f)
            yield return new WaitForSeconds(step.delayAfterTransformation);
    }

    private void PlayVoice(PhaseStep step)
    {
        if (step.transformationVoice == null)
            return;

        if (voiceSource == null)
        {
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : un clip de voix est réglé mais aucun Voice Source n'est assigné.", this);
            return;
        }

        if (step.voiceDelay > 0f)
            StartCoroutine(DelayedVoice(step));
        else
            voiceSource.PlayOneShot(step.transformationVoice);
    }

    private IEnumerator DelayedVoice(PhaseStep step)
    {
        yield return new WaitForSeconds(step.voiceDelay);

        if (voiceSource != null && step.transformationVoice != null)
            voiceSource.PlayOneShot(step.transformationVoice);
    }

    private void PlayMusic(string id, MusicMode mode)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (BossMusicSequencer.Instance == null)
        {
            if (logDiagnostics)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : aucun BossMusicSequencer dans la scène, le segment '{id}' est ignoré.", this);

            return;
        }

        if (mode == MusicMode.Crossfade)
            BossMusicSequencer.Instance.PlayImmediate(id);
        else if (mode == MusicMode.Queue)
            BossMusicSequencer.Instance.QueueSegment(id);
        else
            BossMusicSequencer.Instance.Play(id);
    }

    private IEnumerator PlayDialogue(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId))
            yield break;

        if (dialogue == null)
            dialogue = BossDialogueManager.Instance;

        if (dialogue == null)
        {
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : aucun BossDialogueManager dans la scène, le dialogue de transition est sauté.", this);
            yield break;
        }

        waitedSequenceId = sequenceId;
        sequenceFinished = false;

        dialogue.PlaySequence(sequenceId);

        yield return null;

        float elapsed = 0f;

        while (!sequenceFinished && elapsed < dialogueTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!sequenceFinished)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : la séquence '{sequenceId}' ne s'est jamais terminée après {dialogueTimeout}s. Vérifie que l'id existe dans le BossDialogueManager.", this);

        waitedSequenceId = null;
    }

    private void HandleSequenceFinished(string id)
    {
        if (waitedSequenceId != null && id == waitedSequenceId)
            sequenceFinished = true;
    }

    public void CancelTransition()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        IsRunning = false;
        waitedSequenceId = null;
    }
}