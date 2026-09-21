using System.Linq;
using UnityEngine;

public class DialogueTriggerZone : MonoBehaviour
{
    public enum TutorialCondition { Toujours, SeulementTuto, SeulementNonTuto }

    [Header("Dialogue")]
    [SerializeField] string sequenceId;
    [SerializeField] bool triggerOnce = true;

    [Header("Condition tutoriel")]
    [SerializeField] TutorialCondition tutorialCondition = TutorialCondition.Toujours;

    [Header("Capacite requise")]
    [SerializeField] bool requiresArm = false;
    [SerializeField] ArmAbility requiredArm = ArmAbility.Grapple;
    [SerializeField] bool requiresLeg = false;
    [SerializeField] LegAbility requiredLeg = LegAbility.Dash;
    [SerializeField] string lockedSequenceId = "";
    [SerializeField] bool repeatLockedDialogue = true;

    [Header("Détection")]
    [SerializeField] string playerTag = "Player";

    bool hasTriggered;
    bool hasTriggeredLocked;
    AbilityManager abilityManager;

    void Awake()
    {
        if (string.IsNullOrEmpty(sequenceId))
            Debug.LogError($"{name}: DialogueTriggerZone has no sequenceId assigned", this);

        if ((requiresArm || requiresLeg) && string.IsNullOrEmpty(lockedSequenceId))
            Debug.LogError($"{name}: une capacité est requise mais Locked Sequence Id est vide : le joueur sans la capacité n'aura aucune explication.", this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!PassesTutorialCondition()) return;
        if (OxiDialogueManager.Instance == null || OxiDialogueManager.Instance.IsPlaying) return;

        CacheAbilityManager(other);

        if (!PassesAbilityCondition())
        {
            PlayLockedDialogue();
            return;
        }

        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;
        OxiDialogueManager.Instance.PlaySequence(sequenceId);
    }

    void CacheAbilityManager(Collider2D other)
    {
        if (abilityManager != null) return;

        abilityManager = other.GetComponent<AbilityManager>();

        if (abilityManager == null)
            abilityManager = other.GetComponentInParent<AbilityManager>();

        if (abilityManager == null && (requiresArm || requiresLeg))
            Debug.LogError($"{name}: aucun AbilityManager trouvé sur le joueur, la condition de capacité sera ignorée.", this);
    }

    bool PassesAbilityCondition()
    {
        if (!requiresArm && !requiresLeg) return true;
        if (abilityManager == null) return true;

        if (requiresArm && !abilityManager.IsArmUnlocked(requiredArm)) return false;
        if (requiresLeg && !abilityManager.UnlockedLegs.Contains(requiredLeg)) return false;

        return true;
    }

    void PlayLockedDialogue()
    {
        if (string.IsNullOrEmpty(lockedSequenceId)) return;
        if (!repeatLockedDialogue && hasTriggeredLocked) return;

        hasTriggeredLocked = true;
        OxiDialogueManager.Instance.PlaySequence(lockedSequenceId);
    }

    bool PassesTutorialCondition()
    {
        if (tutorialCondition == TutorialCondition.Toujours) return true;

        bool tutorialEnabled = PlayerPrefs.GetInt("TutorialEnabled", 1) == 1;
        return tutorialCondition == TutorialCondition.SeulementTuto ? tutorialEnabled : !tutorialEnabled;
    }
}