using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OxiZoneStomp : MonoBehaviour
{
    [Header("Stompers")]
    [SerializeField] private bool collectStompersInChildren = true;
    [SerializeField] private List<Stomper> stompers = new List<Stomper>();

    [Header("Joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float aimOffsetMax = 0.6f;

    [Header("Timing")]
    [SerializeField] private float warnDuration = 0.9f;
    [SerializeField] private float extraStrikeWarnMultiplier = 0.6f;
    [SerializeField] private float extraStrikeWarnFloor = 0.35f;

    [Header("Simultané")]
    [SerializeField] private float spreadBetweenStompers = 4f;
    [SerializeField] private float delayBetweenSimultaneousStarts = 0.15f;

    [Header("Retour")]
    [SerializeField] private bool returnHomeAfterStrike = true;

    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public int StomperCount => stompers.Count;

    private void Awake()
    {
        if (collectStompersInChildren)
        {
            stompers.Clear();
            stompers.AddRange(GetComponentsInChildren<Stomper>(true));
        }

        stompers.Sort((a, b) => a.CurrentX.CompareTo(b.CurrentX));
    }

    public IEnumerator PlaySingle()
    {
        yield return PlayStrikes(1);
    }

    public IEnumerator PlayDouble()
    {
        yield return PlayStrikes(2);
    }

    public IEnumerator PlayStrikes(int strikeCount)
    {
        if (isPlaying || stompers.Count == 0)
            yield break;

        isPlaying = true;

        Stomper stomper = PickFreeStomper();

        if (stomper != null)
        {
            float warn = warnDuration;

            for (int i = 0; i < Mathf.Max(1, strikeCount); i++)
            {
                if (i > 0)
                    warn = Mathf.Max(extraStrikeWarnFloor, warn * extraStrikeWarnMultiplier);

                yield return stomper.Strike(AimAtPlayer(), warn);
            }

            if (returnHomeAfterStrike)
                yield return stomper.ReturnHome();
        }

        isPlaying = false;
    }

    public IEnumerator PlaySimultaneous(int count)
    {
        if (isPlaying || stompers.Count == 0)
            yield break;

        isPlaying = true;

        List<Stomper> selected = PickFreeStompers(count);
        int[] running = new int[1];
        float centerX = PlayerX();

        for (int i = 0; i < selected.Count; i++)
        {
            float offset = (i - (selected.Count - 1) * 0.5f) * spreadBetweenStompers;
            float targetX = centerX + offset + Random.Range(-aimOffsetMax, aimOffsetMax);

            running[0]++;
            StartCoroutine(RunOne(selected[i], targetX, running));

            if (i < selected.Count - 1)
                yield return new WaitForSeconds(delayBetweenSimultaneousStarts);
        }

        while (running[0] > 0)
            yield return null;

        isPlaying = false;
    }

    private IEnumerator RunOne(Stomper stomper, float targetX, int[] running)
    {
        yield return stomper.Strike(targetX, warnDuration);

        if (returnHomeAfterStrike)
            yield return stomper.ReturnHome();

        running[0]--;
    }

    private Stomper PickFreeStomper()
    {
        int start = Random.Range(0, stompers.Count);

        for (int i = 0; i < stompers.Count; i++)
        {
            Stomper candidate = stompers[(start + i) % stompers.Count];

            if (candidate != null && !candidate.IsBusy)
                return candidate;
        }

        return null;
    }

    private List<Stomper> PickFreeStompers(int count)
    {
        List<Stomper> free = new List<Stomper>();

        foreach (Stomper stomper in stompers)
            if (stomper != null && !stomper.IsBusy)
                free.Add(stomper);

        while (free.Count > Mathf.Max(1, count))
            free.RemoveAt(Random.Range(0, free.Count));

        return free;
    }

    private float AimAtPlayer()
    {
        float x = PlayerX();

        if (aimOffsetMax > 0f)
            x += Random.Range(-aimOffsetMax, aimOffsetMax);

        return x;
    }

    private float PlayerX()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);

            if (found != null)
                player = found.transform;
        }

        return player != null ? player.position.x : transform.position.x;
    }

    public void StopAndReset()
    {
        StopAllCoroutines();
        isPlaying = false;

        foreach (Stomper stomper in stompers)
            if (stomper != null)
                stomper.ForceReset();
    }
}