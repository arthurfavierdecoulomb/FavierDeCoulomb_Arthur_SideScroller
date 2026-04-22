using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] int spawnIndex;
    [SerializeField] bool rechargeDoubleJump = false;

    bool activated = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        activated = true;
        SpawnManager.Instance.SetSpawnPoint(spawnIndex);

        if (rechargeDoubleJump)
        {
            CharaController chara = other.GetComponent<CharaController>();
            if (chara != null) chara.ResetJumps();
        }

        Debug.Log($"Checkpoint {spawnIndex} activé !");
    }
}