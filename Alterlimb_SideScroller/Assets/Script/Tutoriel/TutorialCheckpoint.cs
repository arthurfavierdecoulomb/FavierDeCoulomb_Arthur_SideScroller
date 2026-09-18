using UnityEngine;

public class TutorialCheckpoint : MonoBehaviour
{
    [Header("Détection")]
    [SerializeField] string playerTag = "Player";

    [Header("Comportement")]
    [SerializeField] bool clearsSafeZone = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        CharaController chara = other.GetComponentInParent<CharaController>();
        if (chara == null) return;

        if (clearsSafeZone)
            chara.SetSafeRespawn(null);
        else
            chara.SetSafeRespawn(transform.position);
    }
}