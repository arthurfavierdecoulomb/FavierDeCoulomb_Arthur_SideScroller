using System.Collections;
using UnityEngine;

public class SpikeFloorAttack : OxiOAttack
{
    public enum SpikePattern
    {
        Wave,
        Scattered,
        Sweep,
        Random
    }

    [Header("Sol à piques")]
    [SerializeField] private OxiSolSpike spikeFloor;
    [SerializeField] private SpikePattern pattern = SpikePattern.Random;

    [Header("Éparpillé")]
    [SerializeField] private int scatteredCountMin = 3;
    [SerializeField] private int scatteredCountMax = 6;

    [Header("Répétition")]
    [SerializeField] private int repeatMin = 1;
    [SerializeField] private int repeatMax = 2;
    [SerializeField] private float delayBetweenRepeats = 0.4f;

    protected override IEnumerator Run(int currentPhase)
    {
        if (spikeFloor == null)
            yield break;

        int repeats = Random.Range(repeatMin, repeatMax + 1);

        for (int i = 0; i < repeats; i++)
        {
            yield return RunPattern(ResolvePattern());

            if (i < repeats - 1)
                yield return new WaitForSeconds(delayBetweenRepeats);
        }
    }

    private SpikePattern ResolvePattern()
    {
        if (pattern != SpikePattern.Random)
            return pattern;

        int roll = Random.Range(0, 3);

        if (roll == 0) return SpikePattern.Wave;
        if (roll == 1) return SpikePattern.Scattered;
        return SpikePattern.Sweep;
    }

    private IEnumerator RunPattern(SpikePattern resolved)
    {
        if (resolved == SpikePattern.Wave)
        {
            yield return spikeFloor.PlayWave();
            yield break;
        }

        if (resolved == SpikePattern.Scattered)
        {
            yield return spikeFloor.PlayScattered(Random.Range(scatteredCountMin, scatteredCountMax + 1));
            yield break;
        }

        yield return spikeFloor.PlaySweep(Random.value < 0.5f);
    }

    public override void Interrupt()
    {
        base.Interrupt();

        if (spikeFloor != null)
            spikeFloor.StopAndReset();
    }
}