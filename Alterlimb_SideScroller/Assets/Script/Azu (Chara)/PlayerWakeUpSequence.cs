using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WakeUpEntry
{
    public LevelData level;
    public string animatorTrigger = "AzuWakeUp";
    public string tutorialDialogueId;
    public string noTutorialDialogueId;
}

public class PlayerWakeUpSequence : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] CharaController player;
    [SerializeField] AbilityManager abilityManager;
    [SerializeField] PlayerAnimator playerAnimator;

    [Header("Réveils configurés")]
    [SerializeField] List<WakeUpEntry> wakeUps = new List<WakeUpEntry>();

    [Header("Musique")]
    [SerializeField] float musicCutDuration = 0.1f;
    [SerializeField] float musicFadeInDuration = 1.5f;

    ArmAbility savedArm;
    bool sequenceActive;
    string pendingDialogueId;
    LevelData currentLevel;

    void Awake()
    {
        if (player == null) player = GetComponent<CharaController>();
        if (abilityManager == null) abilityManager = GetComponent<AbilityManager>();
        if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimator>();

        if (player == null)
            Debug.LogError($"{name}: PlayerWakeUpSequence has no CharaController assigned");

        if (abilityManager == null)
            Debug.LogError($"{name}: PlayerWakeUpSequence has no AbilityManager assigned");

        if (playerAnimator == null)
            Debug.LogError($"{name}: PlayerWakeUpSequence has no PlayerAnimator assigned");
    }

    void OnEnable()
    {
        LevelTransitionManager.OnLevelEntered += HandleLevelEntered;
    }

    void OnDisable()
    {
        LevelTransitionManager.OnLevelEntered -= HandleLevelEntered;

        if (OxiDialogueManager.Instance != null)
            OxiDialogueManager.Instance.OnSequenceFinished -= HandleDialogueFinished;
    }

    void HandleLevelEntered(LevelData target)
    {
        if (sequenceActive) return;

        WakeUpEntry entry = FindEntry(target);
        if (entry == null) return;

        currentLevel = target;
        StartWakeUp(entry);
    }

    WakeUpEntry FindEntry(LevelData target)
    {
        foreach (WakeUpEntry entry in wakeUps)
            if (entry != null && entry.level == target)
                return entry;

        return null;
    }

    void StartWakeUp(WakeUpEntry entry)
    {
        if (player == null || abilityManager == null || playerAnimator == null) return;

        bool tutorialEnabled = PlayerPrefs.GetInt("TutorialEnabled", 1) == 1;
        pendingDialogueId = tutorialEnabled ? entry.tutorialDialogueId : entry.noTutorialDialogueId;

        sequenceActive = true;
        savedArm = abilityManager.CurrentArm;

        player.SetInvincible(true);
        player.SetControlLocked(true);
        abilityManager.SetCombatLock(true);

        player.SetMoveEnabled(!tutorialEnabled);
        player.SetJumpEnabled(!tutorialEnabled);
        player.SetInteractEnabled(!tutorialEnabled);

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(musicCutDuration);

        playerAnimator.TriggerWakeUp(entry.animatorTrigger);
    }

    public void WakeUpFinished()
    {
        if (!sequenceActive) return;

        if (string.IsNullOrEmpty(pendingDialogueId) || OxiDialogueManager.Instance == null)
        {
            EndSequence();
            return;
        }

        OxiDialogueManager.Instance.OnSequenceFinished += HandleDialogueFinished;
        OxiDialogueManager.Instance.PlaySequence(pendingDialogueId);
    }

    void HandleDialogueFinished(string id)
    {
        if (!sequenceActive || id != pendingDialogueId) return;

        OxiDialogueManager.Instance.OnSequenceFinished -= HandleDialogueFinished;
        EndSequence();
    }

    void EndSequence()
    {
        sequenceActive = false;
        pendingDialogueId = null;

        abilityManager.SetCombatLock(false);
        abilityManager.EquipArm(savedArm);

        player.SetControlLocked(false);
        player.SetInvincible(false);
    }

    public void StartLevelMusic()
    {
        if (currentLevel == null || currentLevel.ambientMusic == null) return;
        if (LevelMusicPlayer.Instance == null) return;

        LevelMusicPlayer.Instance.PlayMusic(currentLevel.ambientMusic, musicFadeInDuration);
        currentLevel = null;
    }
}