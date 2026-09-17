using UnityEngine;

public class BreakablePlatformTrigger : MonoBehaviour
{
    [HideInInspector] public HazardManager.Hazard hazard;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            hazard.playerOnPlatform = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            hazard.playerOnPlatform = false;
    }
}