using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpikeMoverAttack : OxiOAttack
{
    public enum Direction
    {
        VersLeJoueur,
        Random,
        GaucheVersDroite,
        DroiteVersGauche,
        Alternance
    }

    [Header("Piques mobiles")]
    [SerializeField] private List<OxiSpikeMover> movers = new List<OxiSpikeMover>();
    [SerializeField] private Direction direction = Direction.VersLeJoueur;

    [Header("Joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Répétition")]
    [SerializeField] private int passesMin = 1;
    [SerializeField] private int passesMax = 1;
    [SerializeField] private float delayBetweenPasses = 1f;
    [SerializeField] private bool overlapPasses = true;
    [SerializeField] private bool lockDirectionPerAttack = true;
    [SerializeField] private float warnOverride = -1f;

    [Header("Double passage")]
    [SerializeField] private int pincerMinPhase = 99;
    [SerializeField] private float pincerOffset = 0.5f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private bool lastWasLeft;

    private void Awake()
    {
        if (logDiagnostics && movers.Count == 0)
            Debug.LogError($"[SpikeMoverAttack] '{name}' : aucun OxiSpikeMover dans la liste, l'attaque ne fera rien.", this);
    }

    protected override IEnumerator Run(int currentPhase)
    {
        if (movers.Count == 0)
            yield break;

        int passes = Random.Range(passesMin, passesMax + 1);

        if (currentPhase >= pincerMinPhase && FreeMoverCount() >= 2)
        {
            yield return RunPincer();
            yield break;
        }

        bool lockedDirection = ResolveDirection();

        if (logDiagnostics)
            Debug.Log($"[SpikeMoverAttack] '{name}' : {passes} passage(s) depuis {(lockedDirection ? "la GAUCHE" : "la DROITE")}.", this);

        if (!overlapPasses)
        {
            for (int i = 0; i < passes; i++)
            {
                yield return RunSingle(lockDirectionPerAttack ? lockedDirection : ResolveDirection());

                if (i < passes - 1)
                    yield return new WaitForSeconds(delayBetweenPasses);
            }

            yield break;
        }

        int[] running = new int[1];

        for (int i = 0; i < passes; i++)
        {
            OxiSpikeMover mover = PickFreeMover();

            if (mover == null)
            {
                if (logDiagnostics)
                    Debug.LogWarning($"[SpikeMoverAttack] '{name}' : passage {i + 1}/{passes} annulé, tous les movers sont occupés. Ajoute un SpikesMovers ou allonge Delay Between Passes.", this);

                break;
            }

            running[0]++;
            StartCoroutine(RunOne(mover, lockDirectionPerAttack ? lockedDirection : ResolveDirection(), running));

            if (i < passes - 1)
                yield return new WaitForSeconds(delayBetweenPasses);
        }

        while (running[0] > 0)
            yield return null;
    }

    private bool ResolveDirection()
    {
        switch (direction)
        {
            case Direction.GaucheVersDroite:
                return true;

            case Direction.DroiteVersGauche:
                return false;

            case Direction.Random:
                return Random.value < 0.5f;
                
            case Direction.Alternance:
                lastWasLeft = !lastWasLeft;
                return lastWasLeft;

            default:
                return !PlayerIsOnRightHalf();
        }
    }

    private IEnumerator RunSingle(bool fromLeft)
    {
        OxiSpikeMover mover = PickFreeMover();

        if (mover == null)
            yield break;

        yield return mover.Travel(fromLeft, warnOverride);
    }

    private IEnumerator RunPincer()
    {
        OxiSpikeMover first = PickFreeMover();
        OxiSpikeMover second = PickFreeMover(first);

        if (first == null || second == null)
        {
            yield return RunSingle(ResolveDirection());
            yield break;
        }

        int[] running = new int[1];

        running[0]++;
        StartCoroutine(RunOne(first, true, running));

        yield return new WaitForSeconds(pincerOffset);

        running[0]++;
        StartCoroutine(RunOne(second, false, running));

        while (running[0] > 0)
            yield return null;
    }

    private IEnumerator RunOne(OxiSpikeMover mover, bool fromLeft, int[] running)
    {
        yield return mover.Travel(fromLeft, warnOverride);
        running[0]--;
    }

    private OxiSpikeMover PickFreeMover(OxiSpikeMover exclude = null)
    {
        foreach (OxiSpikeMover mover in movers)
            if (mover != null && mover != exclude && !mover.IsBusy)
                return mover;

        return null;
    }

    private int FreeMoverCount()
    {
        int free = 0;

        foreach (OxiSpikeMover mover in movers)
            if (mover != null && !mover.IsBusy)
                free++;

        return free;
    }

    private bool PlayerIsOnRightHalf()
    {
        Transform target = ResolvePlayer();

        if (target == null)
            return Random.value < 0.5f;

        return target.position.x > transform.position.x;
    }

    private Transform ResolvePlayer()
    {
        if (player != null)
            return player;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
            player = found.transform;

        return player;
    }

    public override void Interrupt()
    {
        base.Interrupt();

        foreach (OxiSpikeMover mover in movers)
            if (mover != null)
                mover.ForceReset();
    }
}