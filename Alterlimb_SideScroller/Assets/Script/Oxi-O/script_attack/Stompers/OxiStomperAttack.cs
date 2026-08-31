using System.Collections;
using UnityEngine;

public class StomperAttack : OxiOAttack
{
    public enum StompPattern
    {
        Single,
        Double,
        Simultaneous,
        Random
    }

    [Header("Zone de stompers")]
    [SerializeField] private OxiZoneStomp stompZone;
    [SerializeField] private StompPattern pattern = StompPattern.Random;

    [Header("Simultané")]
    [SerializeField] private int simultaneousCountMin = 2;
    [SerializeField] private int simultaneousCountMax = 3;
    [SerializeField] private int simultaneousMinPhase = 1;

    [Header("Répétition")]
    [SerializeField] private int repeatMin = 1;
    [SerializeField] private int repeatMax = 1;
    [SerializeField] private float delayBetweenRepeats = 0.5f;

    protected override IEnumerator Run(int currentPhase)
    {
        if (stompZone == null)
            yield break;

        int repeats = Random.Range(repeatMin, repeatMax + 1);

        for (int i = 0; i < repeats; i++)
        {
            yield return RunPattern(ResolvePattern(currentPhase));

            if (i < repeats - 1)
                yield return new WaitForSeconds(delayBetweenRepeats);
        }
    }

    private StompPattern ResolvePattern(int currentPhase)
    {
        if (pattern != StompPattern.Random)
            return pattern;

        bool canDoSimultaneous = currentPhase >= simultaneousMinPhase && stompZone.StomperCount > 1;
        int roll = Random.Range(0, canDoSimultaneous ? 3 : 2);

        if (roll == 0) return StompPattern.Single;
        if (roll == 1) return StompPattern.Double;
        return StompPattern.Simultaneous;
    }

    private IEnumerator RunPattern(StompPattern resolved)
    {
        if (resolved == StompPattern.Single)
        {
            yield return stompZone.PlaySingle();
            yield break;
        }

        if (resolved == StompPattern.Double)
        {
            yield return stompZone.PlayDouble();
            yield break;
        }

        yield return stompZone.PlaySimultaneous(Random.Range(simultaneousCountMin, simultaneousCountMax + 1));
    }

    public override void Interrupt()
    {
        base.Interrupt();

        if (stompZone != null)
            stompZone.StopAndReset();
    }
}