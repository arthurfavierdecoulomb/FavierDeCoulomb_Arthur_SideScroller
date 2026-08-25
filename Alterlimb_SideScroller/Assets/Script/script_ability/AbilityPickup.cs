using UnityEngine;

public class AbilityPickup : MonoBehaviour
{
    public enum PickupType { Grapple, Saw }

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
        }

        Destroy(gameObject);
    }

}