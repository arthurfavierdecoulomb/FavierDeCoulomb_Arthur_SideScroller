using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OxiSolSpike : MonoBehaviour
{
    [Header("Segments")]
    [SerializeField] private bool collectSegmentsInChildren = true;
    [SerializeField] private List<OxiSpikeSegment> segments = new List<OxiSpikeSegment>();

    [Header("Joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Timing")]
    [SerializeField] private float warnDuration = 0.75f;
    [SerializeField] private float stayDuration = 1.1f;

    [Header("Zone sûre")]
    [SerializeField] private int safeWindowSize = 3;
    [SerializeField] private int minSafeOffsetFromPlayer = 2;
    [SerializeField] private int maxSafeOffsetFromPlayer = 5;
    [SerializeField] private float playerRunSpeed = 7f;

    [Header("Balayage")]
    [SerializeField] private float sweepDelayBetweenSegments = 0.12f;
    [SerializeField] private float sweepStayDuration = 0.35f;

    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public int SegmentCount => segments.Count;

    private void Awake()
    {
        if (collectSegmentsInChildren)
        {
            segments.Clear();
            segments.AddRange(GetComponentsInChildren<OxiSpikeSegment>(true));
        }

        segments.Sort((a, b) => a.WorldX.CompareTo(b.WorldX));

        safeWindowSize = Mathf.Clamp(safeWindowSize, 1, Mathf.Max(1, segments.Count));
    }

    public IEnumerator PlayWave()
    {
        if (isPlaying || segments.Count == 0)
            yield break;

        isPlaying = true;

        int safeStart = PickSafeWindowStart();
        List<OxiSpikeSegment> striking = new List<OxiSpikeSegment>();

        for (int i = 0; i < segments.Count; i++)
        {
            if (i >= safeStart && i < safeStart + safeWindowSize)
                continue;

            striking.Add(segments[i]);
        }

        yield return StrikeTogether(striking, ReachableWarnDuration(safeStart));

        isPlaying = false;
    }

    public IEnumerator PlayScattered(int count)
    {
        if (isPlaying || segments.Count == 0)
            yield break;

        isPlaying = true;

        int safeStart = PickSafeWindowStart();
        List<int> candidates = new List<int>();

        for (int i = 0; i < segments.Count; i++)
        {
            if (i >= safeStart && i < safeStart + safeWindowSize)
                continue;

            candidates.Add(i);
        }

        Shuffle(candidates);

        int taken = Mathf.Clamp(count, 0, candidates.Count);
        List<OxiSpikeSegment> striking = new List<OxiSpikeSegment>();

        for (int i = 0; i < taken; i++)
            striking.Add(segments[candidates[i]]);

        yield return StrikeTogether(striking, ReachableWarnDuration(safeStart));

        isPlaying = false;
    }

    public IEnumerator PlaySweep(bool leftToRight)
    {
        if (isPlaying || segments.Count == 0)
            yield break;

        isPlaying = true;

        int start = leftToRight ? 0 : segments.Count - 1;
        int step = leftToRight ? 1 : -1;

        for (int i = start; i >= 0 && i < segments.Count; i += step)
        {
            StartCoroutine(segments[i].Strike(warnDuration, sweepStayDuration));
            yield return new WaitForSeconds(sweepDelayBetweenSegments);
        }

        yield return new WaitForSeconds(warnDuration + sweepStayDuration + 0.35f);

        isPlaying = false;
    }

    private IEnumerator StrikeTogether(List<OxiSpikeSegment> striking, float appliedWarnDuration)
    {
        foreach (OxiSpikeSegment segment in striking)
            StartCoroutine(segment.Strike(appliedWarnDuration, stayDuration));

        yield return new WaitForSeconds(appliedWarnDuration + stayDuration + 0.35f);
    }

    private int PickSafeWindowStart()
    {
        int maxStart = Mathf.Max(0, segments.Count - safeWindowSize);
        int playerIndex = ClosestSegmentIndexToPlayer();

        int minOffset = Mathf.Min(minSafeOffsetFromPlayer, maxSafeOffsetFromPlayer);
        int magnitude = Random.Range(minOffset, maxSafeOffsetFromPlayer + 1);
        int direction = Random.value < 0.5f ? -1 : 1;

        int desiredCenter = playerIndex + magnitude * direction;

        if (desiredCenter < 0 || desiredCenter >= segments.Count)
            desiredCenter = playerIndex - magnitude * direction;

        int start = desiredCenter - safeWindowSize / 2;

        return Mathf.Clamp(start, 0, maxStart);
    }

    private float ReachableWarnDuration(int safeStart)
    {
        int playerIndex = ClosestSegmentIndexToPlayer();
        int safeEnd = safeStart + safeWindowSize - 1;

        if (playerIndex >= safeStart && playerIndex <= safeEnd)
            return warnDuration;

        int nearestSafe = playerIndex < safeStart ? safeStart : safeEnd;
        float distance = Mathf.Abs(segments[nearestSafe].WorldX - PlayerX());
        float travelTime = distance / Mathf.Max(0.1f, playerRunSpeed);

        return Mathf.Max(warnDuration, travelTime * 1.35f);
    }

    private int ClosestSegmentIndexToPlayer()
    {
        float x = PlayerX();
        int closest = 0;
        float best = float.MaxValue;

        for (int i = 0; i < segments.Count; i++)
        {
            float distance = Mathf.Abs(segments[i].WorldX - x);

            if (distance < best)
            {
                best = distance;
                closest = i;
            }
        }

        return closest;
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

        foreach (OxiSpikeSegment segment in segments)
            segment.ForceReset();
    }

    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}