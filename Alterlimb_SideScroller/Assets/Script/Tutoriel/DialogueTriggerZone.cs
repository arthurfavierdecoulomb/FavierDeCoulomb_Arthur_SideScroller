using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] string sequenceId;
    [SerializeField] bool triggerOnce = true;

    [Header("Détection")]
    [SerializeField] string playerTag = "Player";

    bool hasTriggered;

    void Awake()
    {
        if (string.IsNullOrEmpty(sequenceId))
            Debug.LogError($"{name}: DialogueTriggerZone has no sequenceId assigned");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (OxiDialogueManager.Instance == null || OxiDialogueManager.Instance.IsPlaying) return;

        hasTriggered = true;
        OxiDialogueManager.Instance.PlaySequence(sequenceId);
    }
}