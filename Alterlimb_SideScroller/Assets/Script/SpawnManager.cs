using System;
using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public static event Action OnPlayerRespawn;

    [Header("Points de spawn")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Respawn")]
    [SerializeField] float respawnDelay = 1.5f;

    int activeSpawnIndex = 0;

    public int ActiveSpawnIndex => activeSpawnIndex;
    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Respawn(CharaController player)
    {
        Vector3 spawnPos = GetActiveSpawnPoint();

        if (DeathAnimationManager.Instance != null)
        {
            DeathAnimationManager.Instance.PlayDeathSequence(
                onRespawn: () => DoRevive(player, spawnPos),
                checkpointPosition: spawnPos
            );
        }
        else
        {
            StartCoroutine(RespawnRoutine(player, spawnPos));
        }
    }

    public void SetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        if (clamped <= activeSpawnIndex) return;

        activeSpawnIndex = clamped;
        Debug.Log($"Checkpoint activé : spawn {activeSpawnIndex}");
    }

    IEnumerator RespawnRoutine(CharaController player, Vector3 spawnPos)
    {
        yield return new WaitForSeconds(respawnDelay);
        DoRevive(player, spawnPos);
    }

    void DoRevive(CharaController player, Vector3 spawnPos)
    {
        OnPlayerRespawn?.Invoke();
        player.Revive(spawnPos);
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
}