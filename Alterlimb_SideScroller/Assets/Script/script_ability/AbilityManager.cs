using UnityEngine;
using System.Collections.Generic;

public enum ArmAbility { Hand, Grapple, Saw }
public enum LegAbility { NormalJump, HighJump, Dash }

public class AbilityManager : MonoBehaviour
{
    // Capacités débloquées
    List<ArmAbility> unlockedArms = new List<ArmAbility> { ArmAbility.Hand };
    List<LegAbility> unlockedLegs = new List<LegAbility> { LegAbility.NormalJump };

    int armIndex = 0;
    int legIndex = 0;

    public ArmAbility CurrentArm => unlockedArms[armIndex];
    public LegAbility CurrentLeg => unlockedLegs[legIndex];

    // Refs
    GrapplingHook grappleScript;
    SawAbility sawScript;
    CharaController charaController;

    void Awake()
    {
        grappleScript = GetComponent<GrapplingHook>();
        sawScript = GetComponent<SawAbility>();
        charaController = GetComponent<CharaController>();

        // Désactive tout au départ
        if (grappleScript) grappleScript.canUseGrapple = false;
        if (sawScript) sawScript.enabled = false;
    }

    void Update()
    {
        // Cycle bras avec A
        if (Input.GetKeyDown(KeyCode.Q))
        {
            armIndex = (armIndex + 1) % unlockedArms.Count;
            ApplyArmAbility();
        }

        // Cycle jambes avec E
        if (Input.GetKeyDown(KeyCode.E))
        {
            legIndex = (legIndex + 1) % unlockedLegs.Count;
            ApplyLegAbility();
        }
    }

    void ApplyArmAbility()
    {
        // Désactive tout d'abord
        if (grappleScript)
        {
            grappleScript.canUseGrapple = false;
            grappleScript.ReleaseGrapple();
        }
        if (sawScript) sawScript.enabled = false;

        // Active la capacité courante
        switch (CurrentArm)
        {
            case ArmAbility.Hand:
                // Rien à activer, c'est la main de base
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

    // ── Débloquage des capacités (appelé par AbilityPickup) ──

    public void UnlockArm(ArmAbility ability)
    {
        if (!unlockedArms.Contains(ability))
        {
            unlockedArms.Add(ability);
            Debug.Log($"Capacité bras débloquée : {ability}");
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