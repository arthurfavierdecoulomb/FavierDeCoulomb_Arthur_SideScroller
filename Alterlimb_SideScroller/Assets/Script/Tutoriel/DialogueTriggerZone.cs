using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    public enum TutorialCondition { Toujours, SeulementTuto, SeulementNonTuto }

    [Header("Dialogue")]
    [SerializeField] string sequenceId;
    [SerializeField] bool triggerOnce = true;

    [Header("Condition tutoriel")]
    [SerializeField] TutorialCondition tutorialCondition = TutorialCondition.Toujours;

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
        if (!PassesTutorialCondition()) return;
        if (OxiDialogueManager.Instance == null || OxiDialogueManager.Instance.IsPlaying) return;

        hasTriggered = true;
        OxiDialogueManager.Instance.PlaySequence(sequenceId);
    }

    bool PassesTutorialCondition()
    {
        if (tutorialCondition == TutorialCondition.Toujours) return true;

        bool tutorialEnabled = PlayerPrefs.GetInt("TutorialEnabled", 1) == 1;
        return tutorialCondition == TutorialCondition.SeulementTuto ? tutorialEnabled : !tutorialEnabled;
    }
}