using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Points de spawn")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Respawn")]
    [SerializeField] float respawnDelay = 1.5f;

    int activeSpawnIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Respawn(CharaController player)
    {
        StartCoroutine(RespawnRoutine(player));
    }

    IEnumerator RespawnRoutine(CharaController player)
    {
        yield return new WaitForSeconds(respawnDelay);
        player.Revive(GetActiveSpawnPoint());
        player.ResetJumps();
    }

    Vector3 GetActiveSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("SpawnManager : aucun spawn point assigné !");
            return Vector3.zero;
        }
        return spawnPoints[activeSpawnIndex].position;
    }

    public void SetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        if (clamped <= activeSpawnIndex) return; // on ne régresse pas
        activeSpawnIndex = clamped;
        Debug.Log($"Checkpoint activé : spawn {activeSpawnIndex}");
    }
}