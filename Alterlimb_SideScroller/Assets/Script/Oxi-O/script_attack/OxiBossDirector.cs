using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OxiOBossDirector : MonoBehaviour
{
    public enum PatternMode
    {
        RandomAttacks,
        AuthoredPatterns
    }

    [System.Serializable]
    public class AttackStep
    {
        public List<OxiOAttack> simultaneousAttacks = new List<OxiOAttack>();
        public float delayAfter = -1f;
    }

    [System.Serializable]
    public class AttackPattern
    {
        public string patternName = "Pattern";
        public List<AttackStep> steps = new List<AttackStep>();
        public float weight = 1f;
        public int minPhase = 1;
    }

    [Header("Mode")]
    [SerializeField] private PatternMode patternMode = PatternMode.RandomAttacks;

    [Header("Attaques (mode aléatoire)")]
    [SerializeField] private List<OxiOAttack> attacks = new List<OxiOAttack>();
    [SerializeField] private int attacksPerPattern = 3;
    [SerializeField] private bool avoidSameAttackTwice = true;

    [Header("Patterns écrits (mode chorégraphié)")]
    [SerializeField] private List<AttackPattern> patterns = new List<AttackPattern>();
    [SerializeField] private bool avoidSamePatternTwice = true;

    [Header("Rythme")]
    [SerializeField] private float delayBeforeFirstAttack = 1.5f;
    [SerializeField] private float delayBetweenAttacks = 1.2f;
    [SerializeField] private float delayBeforeOverheat = 0.8f;
    [SerializeField] private float delayAfterWindow = 1f;

    [Header("Accélération")]
    [SerializeField] private float delayMultiplierPerFailedWindow = 0.88f;
    [SerializeField] private float minDelayBetweenAttacks = 0.4f;

    [Header("Vulnérabilité")]
    [SerializeField] private OxiOCore core;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private float overheatDuration = 8f;
    [SerializeField] private bool extendWindowWhileCutting = true;

    [Header("Confinement")]
    [SerializeField] private LaserBeam[] containmentLasers = new LaserBeam[0];
    [SerializeField] private float laserIntensityNormal = 1f;
    [SerializeField] private float laserIntensityOverheat = 0.3f;
    [SerializeField] private int laserFlickerOnWindowOpen = 3;

    [Header("Démarrage")]
    [SerializeField] private bool startFightOnEnable = false;

    [Header("Événements")]
    public UnityEvent onFightStart;
    public UnityEvent onOverheatStart;
    public UnityEvent onWindowFailed;
    public UnityEvent onCoreRemoved;
    public UnityEvent onPhaseEnd;

    private int currentPhase = 1;
    private int failedWindows;
    private float currentDelayBetweenAttacks;
    private OxiOAttack lastAttack;
    private AttackPattern lastPattern;
    private Coroutine fightRoutine;
    private bool coreRemoved;

    public bool IsFighting => fightRoutine != null;
    public int CurrentPhase => currentPhase;

    private void OnEnable()
    {
        if (startFightOnEnable)
            StartFight();
    }

    public void StartFight()
    {
        if (fightRoutine != null)
            return;

        currentDelayBetweenAttacks = delayBetweenAttacks;
        failedWindows = 0;
        coreRemoved = false;

        if (abilityManager == null)
            abilityManager = FindAnyObjectByType<AbilityManager>();

        if (core != null)
        {
            core.OnCoreRemoved -= HandleCoreRemoved;
            core.OnCoreRemoved += HandleCoreRemoved;
        }

        if (abilityManager != null)
            abilityManager.SetCombatLock(true);

        foreach (LaserBeam laser in containmentLasers)
        {
            if (laser == null)
                continue;

            laser.SetIntensityMultiplierInstant(laserIntensityNormal);
            laser.PowerUpWithFlicker();
        }

        onFightStart?.Invoke();
        fightRoutine = StartCoroutine(FightLoop());
    }

    public void StopFight()
    {
        if (fightRoutine != null)
        {
            StopCoroutine(fightRoutine);
            fightRoutine = null;
        }

        StopAllCoroutines();
        InterruptAllAttacks();

        if (core != null)
        {
            core.CloseWindow();
            core.OnCoreRemoved -= HandleCoreRemoved;
        }

        if (abilityManager != null)
            abilityManager.SetCombatLock(true);

        foreach (LaserBeam laser in containmentLasers)
            if (laser != null)
                laser.TurnOff();
    }

    public void SetPhase(int phase)
    {
        currentPhase = Mathf.Max(1, phase);
    }

    private IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(delayBeforeFirstAttack);

        while (!coreRemoved)
        {
            if (patternMode == PatternMode.AuthoredPatterns)
                yield return RunAuthoredPattern();
            else
                yield return RunRandomPattern();

            if (coreRemoved)
                break;

            yield return new WaitForSeconds(delayBeforeOverheat);
            yield return RunVulnerabilityWindow();

            if (coreRemoved)
                break;

            yield return new WaitForSeconds(delayAfterWindow);
        }

        fightRoutine = null;
        onPhaseEnd?.Invoke();
    }

    private IEnumerator RunAuthoredPattern()
    {
        AttackPattern pattern = PickPattern();

        if (pattern == null)
        {
            yield return RunRandomPattern();
            yield break;
        }

        lastPattern = pattern;

        for (int i = 0; i < pattern.steps.Count; i++)
        {
            if (coreRemoved)
                yield break;

            AttackStep step = pattern.steps[i];

            yield return RunStep(step);

            if (i >= pattern.steps.Count - 1)
                continue;

            float delay = step.delayAfter < 0f ? currentDelayBetweenAttacks : step.delayAfter;
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator RunStep(AttackStep step)
    {
        if (step == null || step.simultaneousAttacks == null)
            yield break;

        int[] running = new int[1];

        foreach (OxiOAttack attack in step.simultaneousAttacks)
        {
            if (attack == null)
                continue;

            running[0]++;
            StartCoroutine(RunSingleAttack(attack, running));
        }

        while (running[0] > 0 && !coreRemoved)
            yield return null;
    }

    private IEnumerator RunSingleAttack(OxiOAttack attack, int[] running)
    {
        lastAttack = attack;

        yield return attack.Execute(currentPhase);

        running[0]--;
    }

    private IEnumerator RunRandomPattern()
    {
        for (int i = 0; i < attacksPerPattern; i++)
        {
            if (coreRemoved)
                yield break;

            OxiOAttack attack = PickAttack();

            if (attack == null)
            {
                yield return new WaitForSeconds(currentDelayBetweenAttacks);
                continue;
            }

            lastAttack = attack;

            yield return attack.Execute(currentPhase);

            if (i < attacksPerPattern - 1)
                yield return new WaitForSeconds(currentDelayBetweenAttacks);
        }
    }

    private IEnumerator RunVulnerabilityWindow()
    {
        if (core == null)
            yield break;

        foreach (LaserBeam laser in containmentLasers)
        {
            if (laser == null)
                continue;

            laser.SetIntensityMultiplier(laserIntensityOverheat);
            laser.FlickerWhileOn(laserFlickerOnWindowOpen);
        }

        core.OpenWindow();
        onOverheatStart?.Invoke();

        float remaining = overheatDuration;

        while (remaining > 0f && !coreRemoved)
        {
            remaining -= Time.deltaTime;

            if (extendWindowWhileCutting && core.IsCutting && core.NormalizedProgress > 0f)
                remaining = Mathf.Max(remaining, 0.75f);

            yield return null;
        }

        if (!coreRemoved)
        {
            core.CloseWindow();
            failedWindows++;
            currentDelayBetweenAttacks = Mathf.Max(minDelayBetweenAttacks, currentDelayBetweenAttacks * delayMultiplierPerFailedWindow);
            onWindowFailed?.Invoke();
        }

        foreach (LaserBeam laser in containmentLasers)
            if (laser != null)
                laser.SetIntensityMultiplier(laserIntensityNormal);
    }

    private AttackPattern PickPattern()
    {
        List<AttackPattern> candidates = new List<AttackPattern>();
        float totalWeight = 0f;

        foreach (AttackPattern pattern in patterns)
        {
            if (pattern == null || pattern.steps.Count == 0)
                continue;

            if (currentPhase < pattern.minPhase)
                continue;

            if (avoidSamePatternTwice && pattern == lastPattern && patterns.Count > 1)
                continue;

            candidates.Add(pattern);
            totalWeight += Mathf.Max(0f, pattern.weight);
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        foreach (AttackPattern pattern in candidates)
        {
            roll -= Mathf.Max(0f, pattern.weight);

            if (roll <= 0f)
                return pattern;
        }

        return candidates[candidates.Count - 1];
    }

    private OxiOAttack PickAttack()
    {
        List<OxiOAttack> candidates = new List<OxiOAttack>();
        float totalWeight = 0f;

        foreach (OxiOAttack attack in attacks)
        {
            if (attack == null || !attack.IsAvailable(currentPhase))
                continue;

            if (avoidSameAttackTwice && attack == lastAttack && attacks.Count > 1)
                continue;

            candidates.Add(attack);
            totalWeight += attack.Weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        foreach (OxiOAttack attack in candidates)
        {
            roll -= attack.Weight;

            if (roll <= 0f)
                return attack;
        }

        return candidates[candidates.Count - 1];
    }

    private void InterruptAllAttacks()
    {
        foreach (OxiOAttack attack in attacks)
            if (attack != null)
                attack.Interrupt();

        foreach (AttackPattern pattern in patterns)
        {
            if (pattern == null)
                continue;

            foreach (AttackStep step in pattern.steps)
            {
                if (step == null || step.simultaneousAttacks == null)
                    continue;

                foreach (OxiOAttack attack in step.simultaneousAttacks)
                    if (attack != null)
                        attack.Interrupt();
            }
        }
    }

    private void HandleCoreRemoved()
    {
        coreRemoved = true;

        foreach (LaserBeam laser in containmentLasers)
            if (laser != null)
                laser.SetIntensityMultiplier(laserIntensityNormal);

        onCoreRemoved?.Invoke();
    }

    private void OnDisable()
    {
        if (core != null)
            core.OnCoreRemoved -= HandleCoreRemoved;
    }
}