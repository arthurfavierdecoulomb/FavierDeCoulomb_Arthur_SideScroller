using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] int spawnIndex;
    [SerializeField] bool rechargeDoubleJump = false;

    [Header("Notification")]
    [SerializeField] bool showNotice = true;
    [SerializeField] string noticeMessage = "";

    bool activated = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;
        if (SpawnManager.Instance == null) return;
        if (spawnIndex <= SpawnManager.Instance.ActiveSpawnIndex) return;

        activated = true;
        SpawnManager.Instance.SetSpawnPoint(spawnIndex);

        if (rechargeDoubleJump)
        {
            CharaController chara = other.GetComponent<CharaController>();
            if (chara != null) chara.ResetJumps();
        }

        if (showNotice && SpawnPointNotice.Instance != null)
            SpawnPointNotice.Instance.Show(noticeMessage);

        Debug.Log($"Checkpoint {spawnIndex} activé !");
    }
}