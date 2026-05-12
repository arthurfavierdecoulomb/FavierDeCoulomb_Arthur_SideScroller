using UnityEngine;
using System;
using System.Collections.Generic;

public enum ArmAbility { Hand, Grapple, Saw }
public enum LegAbility { NormalJump, HighJump, Dash }

public class AbilityManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Événements (écoutés par l'UI, l'audio, les FX, etc.)
    // ════════════════════════════════════════════════════════════

    /// <summary>Déclenché quand le bras actif change (après un cycle Q).</summary>
    public event Action<ArmAbility> OnArmChanged;

    /// <summary>Déclenché quand un nouvel artefact de bras est débloqué (pickup).</summary>
    public event Action<ArmAbility> OnArmUnlocked;

    // ════════════════════════════════════════════════════════════
    //  Données
    // ════════════════════════════════════════════════════════════

    List<ArmAbility> unlockedArms = new List<ArmAbility> { ArmAbility.Hand };
    List<LegAbility> unlockedLegs = new List<LegAbility> { LegAbility.NormalJump };

    int armIndex = 0;
    int legIndex = 0;

    public ArmAbility CurrentArm => unlockedArms[armIndex];
    public LegAbility CurrentLeg => unlockedLegs[legIndex];

    /// <summary>Liste lecture seule des bras débloqués (pour l'UI).</summary>
    public IReadOnlyList<ArmAbility> UnlockedArms => unlockedArms;

    // Refs
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
    }

    void Update()
    {
        // Cycle bras avec Q (uniquement si plus d'un bras débloqué)
        if (Input.GetKeyDown(KeyCode.Q) && unlockedArms.Count > 1)
        {
            armIndex = (armIndex + 1) % unlockedArms.Count;
            ApplyArmAbility();
            OnArmChanged?.Invoke(CurrentArm);
        }

        // Cycle jambes avec E
        if (Input.GetKeyDown(KeyCode.E) && unlockedLegs.Count > 1)
        {
            legIndex = (legIndex + 1) % unlockedLegs.Count;
            ApplyLegAbility();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Application des capacités
    // ════════════════════════════════════════════════════════════

    void ApplyArmAbility()
    {
        if (grappleScript)
        {
            grappleScript.canUseGrapple = false;
            grappleScript.ReleaseGrapple();
        }
        if (sawScript) sawScript.enabled = false;

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
        switch (CurrentLeg)
        {
            case LegAbility.NormalJump:
                charaController.SetJumpMode(JumpMode.Normal);
                charaController.SetDashEnabled(false);
                break;
            case LegAbility.HighJump:
                charaController.SetJumpMode(JumpMode.High);
                charaController.SetDashEnabled(false);
                break;
            case LegAbility.Dash:
                charaController.SetJumpMode(JumpMode.Normal);
                charaController.SetDashEnabled(true);
                break;
        }

        Debug.Log($"Jambes actives : {CurrentLeg}");
    }

    // ════════════════════════════════════════════════════════════
    //  Déblocage des capacités (appelé par AbilityPickup)
    // ════════════════════════════════════════════════════════════

    public void UnlockArm(ArmAbility ability)
    {
        if (!unlockedArms.Contains(ability))
        {
            unlockedArms.Add(ability);
            Debug.Log($"Capacité bras débloquée : {ability}");
            OnArmUnlocked?.Invoke(ability);
        }
    }

    public void UnlockLeg(LegAbility ability)
    {
        if (!unlockedLegs.Contains(ability))
        {
            unlockedLegs.Add(ability);
            Debug.Log($"Capacité jambes débloquée : {ability}");
        }
    }
}