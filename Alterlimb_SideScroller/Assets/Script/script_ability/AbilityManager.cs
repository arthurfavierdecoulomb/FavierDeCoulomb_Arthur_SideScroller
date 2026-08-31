using UnityEngine;
using System;
using System.Collections.Generic;

public enum ArmAbility { Hand, Grapple, Saw }
public enum LegAbility { NormalJump = 0, Dash = 2 }

public class AbilityManager : MonoBehaviour
{
    public event Action<ArmAbility> OnArmChanged;
    public event Action<ArmAbility> OnArmUnlocked;
    public event Action<LegAbility> OnLegChanged;
    public event Action<LegAbility> OnLegUnlocked;

    List<ArmAbility> unlockedArms = new List<ArmAbility> { ArmAbility.Hand };
    List<LegAbility> unlockedLegs = new List<LegAbility> { LegAbility.NormalJump };

    int armIndex = 0;
    int legIndex = 0;
    bool combatLocked = false;

    public ArmAbility CurrentArm => unlockedArms[armIndex];
    public LegAbility CurrentLeg => unlockedLegs[legIndex];
    public IReadOnlyList<ArmAbility> UnlockedArms => unlockedArms;
    public IReadOnlyList<LegAbility> UnlockedLegs => unlockedLegs;
    public bool CombatLocked => combatLocked;
    public bool IsSawEquipped => !combatLocked && CurrentArm == ArmAbility.Saw;
    public bool IsGrappleEquipped => !combatLocked && CurrentArm == ArmAbility.Grapple;

    GrapplingHook grappleScript;
    SawAbility sawScript;
    CharaController charaController;

    void Awake()
    {
        grappleScript = GetComponent<GrapplingHook>();
        sawScript = GetComponent<SawAbility>();
        charaController = GetComponent<CharaController>();

        if (grappleScript) grappleScript.canUseGrapple = false;
        if (sawScript) sawScript.enabled = false;

        ApplyLegAbility();
    }

    void Update()
    {
        if (!combatLocked && Input.GetKeyDown(KeyCode.Q) && unlockedArms.Count > 1)
        {
            armIndex = (armIndex + 1) % unlockedArms.Count;
            ApplyArmAbility();
            OnArmChanged?.Invoke(CurrentArm);
        }

        if (Input.GetKeyDown(KeyCode.E) && unlockedLegs.Count > 1)
        {
            legIndex = (legIndex + 1) % unlockedLegs.Count;
            ApplyLegAbility();
            OnLegChanged?.Invoke(CurrentLeg);
        }
    }

    void ApplyArmAbility()
    {
        if (grappleScript)
        {
            grappleScript.canUseGrapple = false;
            grappleScript.ReleaseGrapple();
        }

        if (sawScript) sawScript.enabled = false;

        if (combatLocked)
        {
            Debug.Log("Bras verrouillés (combat)");
            return;
        }

        switch (CurrentArm)
        {
            case ArmAbility.Hand:
                break;
            case ArmAbility.Grapple:
                if (grappleScript) grappleScript.canUseGrapple = true;
                break;
            case ArmAbility.Saw:
                if (sawScript) sawScript.enabled = true;
                break;
        }

        Debug.Log($"Bras actif : {CurrentArm}");
    }

    void ApplyLegAbility()
    {
        if (charaController == null) return;

        charaController.SetDashEnabled(CurrentLeg == LegAbility.Dash);
        Debug.Log($"Jambes actives : {CurrentLeg}");
    }

    public void SetCombatLock(bool locked)
    {
        if (combatLocked == locked) return;

        combatLocked = locked;

        if (locked)
        {
            int handIndex = unlockedArms.IndexOf(ArmAbility.Hand);
            if (handIndex >= 0) armIndex = handIndex;
        }

        ApplyArmAbility();
        OnArmChanged?.Invoke(CurrentArm);
    }

    public void EquipArm(ArmAbility ability)
    {
        int index = unlockedArms.IndexOf(ability);
        if (index < 0) return;

        armIndex = index;
        ApplyArmAbility();
        OnArmChanged?.Invoke(CurrentArm);
    }

    public bool IsArmUnlocked(ArmAbility ability)
    {
        return unlockedArms.Contains(ability);
    }

    public void UnlockArm(ArmAbility ability)
    {
        if (unlockedArms.Contains(ability)) return;

        unlockedArms.Add(ability);
        Debug.Log($"Capacité bras débloquée : {ability}");
        OnArmUnlocked?.Invoke(ability);
    }

    public void UnlockLeg(LegAbility ability)
    {
        if (unlockedLegs.Contains(ability)) return;

        unlockedLegs.Add(ability);
        Debug.Log($"Capacité jambes débloquée : {ability}");
        OnLegUnlocked?.Invoke(ability);
    }
}