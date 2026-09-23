using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossMusicTrigger : MonoBehaviour
{
    [Header("Déclenchement")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private float delayBeforePlay = 0f;

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

        if (OxiOMusicDirector.Instance == null)
        {
            Debug.LogError($"[BossMusicTrigger] '{name}' : aucun OxiOMusicDirector dans la scène, la musique du boss ne démarrera pas.", this);
            yield break;
        }

        OxiOMusicDirector.Instance.EnterBossRoom();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.5f);
    }
}