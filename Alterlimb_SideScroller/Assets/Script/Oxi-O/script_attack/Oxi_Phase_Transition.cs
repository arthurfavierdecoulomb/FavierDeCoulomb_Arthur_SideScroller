using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OxiOPhaseTransition : MonoBehaviour
{
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

    [Header("Corps d'Oxi-O")]
    [SerializeField] private OxiBodyMover bodyMotion;

    [Header("Caméra")]
    [SerializeField] private CameraFocus cameraFocus;
    [SerializeField] private string dialogueFocusId = "dialogue_interlude";
    [SerializeField] private string transformationFocusId = "dialogue_interlude";
    [SerializeField] private string finaleFocusId = "focus_oxi";

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

    [Header("Fin du combat")]
    [SerializeField] private float fallCueTimeout = 10f;

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

        if (oxiAnimation == null)
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : Oxi Animation non assigné, la transformation et la chute finale ne pourront pas être suivies.", this);

        if (cameraFocus == null)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Camera Focus non assigné, la caméra restera sur Azu pendant les transitions.", this);

        if (screenUI == null)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Screen UI non assigné, l'écran suspendu ne bougera pas pendant les transitions.", this);

        bool hasFinalStep = false;

        foreach (PhaseStep step in steps)
        {
            if (step == null)
                continue;

            if (step.isFinalPhase)
                hasFinalStep = true;

            if (!step.isFinalPhase && step.nextPhase <= step.fromPhase)
                Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : l'étape '{step.label}' repart en phase {step.nextPhase} depuis la phase {step.fromPhase}. Boucle possible.", this);
        }

        if (hasFinalStep && bodyMotion == null)
            Debug.LogError($"[OxiOPhaseTransition] '{name}' : Body Motion non assigné, Oxi-O ne tombera pas à la fin du combat.", this);
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

        Log($"Transition '{step.label}' : phase {step.fromPhase} terminée.");

        step.onTransitionStart?.Invoke();

        if (step.isFinalPhase)
            yield return FinaleRoutine();
        else
            yield return PhaseChangeRoutine(step);

        step.onTransitionEnd?.Invoke();

        IsRunning = false;
        routine = null;
    }

    private IEnumerator FinaleRoutine()
    {
        director.StopFightKeepContainment();

        if (screenUI != null)
            screenUI.CancelEcoCountdown();

        if (cameraFocus != null)
            cameraFocus.FocusOn(finaleFocusId);

        yield return WaitForFallCue();

        if (bodyMotion != null)
            bodyMotion.FallAway();

        if (cameraFocus != null)
            cameraFocus.ReleaseFocus();

        if (screenUI != null)
            screenUI.Hide();

        director.ConcludeFight();

        Log("Oxi-O tombe : caméra rendue à Azu, fin du combat.");
    }

    private IEnumerator WaitForFallCue()
    {
        if (oxiAnimation == null)
            yield break;

        if (oxiAnimation.HasFallen)
            yield break;

        bool fell = false;
        System.Action handler = () => fell = true;

        oxiAnimation.OnFinalFall += handler;

        float elapsed = 0f;

        while (!fell && elapsed < fallCueTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        oxiAnimation.OnFinalFall -= handler;

        if (fell)
            yield break;

        Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : l'Animation Event 'OxiFall' n'a jamais été reçu après {fallCueTimeout}s. Chute déclenchée par sécurité. Vérifie l'event dans l'animation du dernier coup.", this);
        oxiAnimation.TriggerFinalFall();
    }

    private IEnumerator PhaseChangeRoutine(PhaseStep step)
    {
        director.StopFight();

        if (bodyMotion != null)
            bodyMotion.RiseToRest();

        if (cameraFocus != null)
            cameraFocus.FocusOn(dialogueFocusId);

        if (screenUI != null)
            screenUI.CancelEcoCountdown();

        if (delayAfterLastCut > 0f)
            yield return new WaitForSeconds(delayAfterLastCut);

        yield return WaitForSlicedAnimation();

        if (hideScreenDuringDialogue && screenUI != null)
            screenUI.Hide();

        if (delayBeforeDialogue > 0f)
            yield return new WaitForSeconds(delayBeforeDialogue);

        yield return PlayDialogue(step.dialogueSequenceId);

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

        Log($"Phase {step.nextPhase} lancée.");
    }

    private IEnumerator WaitForSlicedAnimation()
    {
        if (!waitForSlicedAnimation || oxiAnimation == null)
            yield break;

        float elapsed = 0f;

        while (oxiAnimation.IsSlicing && elapsed < slicedTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= slicedTimeout && logDiagnostics)
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : l'animation sliced n'est jamais sortie de son état après {slicedTimeout}s. Vérifie qu'elle n'est pas en loop.", this);
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

        if (!step.playTransformation)
            yield break;

        if (oxiAnimation == null)
        {
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : Oxi Animation non assigné, la transformation est sautée.", this);
            yield break;
        }

        if (cameraFocus != null)
            cameraFocus.FocusOn(transformationFocusId);

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
            Debug.LogWarning($"[OxiOPhaseTransition] '{name}' : la transformation ne s'est jamais terminée après {transformationTimeout}s. Vérifie que son état n'est pas en loop.", this);

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

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[OxiOPhaseTransition] {message}", this);
    }
}