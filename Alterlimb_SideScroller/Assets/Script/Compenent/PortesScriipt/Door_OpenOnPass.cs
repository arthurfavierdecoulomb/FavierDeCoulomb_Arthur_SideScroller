using UnityEngine;

public class CloseDoorOnPass : MonoBehaviour
{
    [Header("Cible")]
    [SerializeField] Door door;

    [Header("Détection")]
    [SerializeField] string playerTag = "Player";

    void Awake()
    {
        if (door == null)
            Debug.LogError($"{name}: aucune porte assigné");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (door != null) door.OpenDoor();
    }
}