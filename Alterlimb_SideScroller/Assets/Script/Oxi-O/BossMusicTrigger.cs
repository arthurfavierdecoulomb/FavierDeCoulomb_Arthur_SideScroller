using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossMusicTrigger : MonoBehaviour
{
    public enum PlayMode
    {
        Play,
        Crossfade,
        Queue
    }

    [Header("Déclenchement")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private float delayBeforePlay = 0f;

    [Header("Segment")]
    [SerializeField] private string segmentId = "boss_decouverte";
    [SerializeField] private PlayMode mode = PlayMode.Play;

    [Header("Musique du niveau")]
    [SerializeField] private bool fadeOutLevelMusic = true;
    [SerializeField] private float levelMusicFadeDuration = 1.5f;

    private bool triggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce)
            return;

        if (!other.CompareTag(playerTag))
            return;

        triggered = true;
        StartCoroutine(PlayRoutine());
    }

    public void PlayNow()
    {
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (fadeOutLevelMusic && LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(levelMusicFadeDuration);

        if (delayBeforePlay > 0f)
            yield return new WaitForSeconds(delayBeforePlay);

        if (BossMusicSequencer.Instance == null)
        {
            Debug.LogWarning($"[BossMusicTrigger] '{name}' : aucun BossMusicSequencer dans la scène.", this);
            yield break;
        }

        if (mode == PlayMode.Crossfade)
            BossMusicSequencer.Instance.PlayImmediate(segmentId);
        else if (mode == PlayMode.Queue)
            BossMusicSequencer.Instance.QueueSegment(segmentId);
        else
            BossMusicSequencer.Instance.Play(segmentId);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.5f);
    }
}