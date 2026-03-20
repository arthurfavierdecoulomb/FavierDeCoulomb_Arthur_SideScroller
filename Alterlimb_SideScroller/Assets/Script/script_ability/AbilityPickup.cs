using UnityEngine;

public class AbilityPickup : MonoBehaviour
{
    public enum PickupType { Grapple, Saw, HighJump, Dash }

    [SerializeField] PickupType abilityType;

    void OnTriggerEnter2D(Collider2D other)
    {
        AbilityManager manager = other.GetComponent<AbilityManager>();
        if (manager == null) return;

        switch (abilityType)
        {
            case PickupType.Grapple:
                manager.UnlockArm(ArmAbility.Grapple);
                Debug.Log("Item ramassé : Grappin (Bras)");
                break;
            case PickupType.Saw:
                manager.UnlockArm(ArmAbility.Saw);
                Debug.Log("Item ramassé : Scie (Bras)");
                break;
            case PickupType.HighJump:
                manager.UnlockLeg(LegAbility.HighJump);
                Debug.Log("Item ramassé : Grand Jump (Jambes)");
                break;
            case PickupType.Dash:
                manager.UnlockLeg(LegAbility.Dash);
                Debug.Log("Item ramassé : Dash (Jambes)");
                break;
        }

        Destroy(gameObject);
    }

}