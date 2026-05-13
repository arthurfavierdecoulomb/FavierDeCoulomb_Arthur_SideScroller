using System;
using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    /// <summary>
    /// Déclenché juste avant que le joueur ne réapparaisse au checkpoint
    /// (après l'animation de mort si elle existe).
    /// Les abonnés peuvent l'écouter pour remettre leur état initial :
    /// plateformes mobiles, portes activées, switches, ennemis, etc.
    /// </summary>
    public static event Action OnPlayerRespawn;

    [Header("Points de spawn")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Respawn")]
    [Tooltip("Délai de respawn utilisé UNIQUEMENT si aucun DeathAnimationManager n'est présent (fallback).")]
    [SerializeField] float respawnDelay = 1.5f;

    int activeSpawnIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    public void Respawn(CharaController player)
    {
        Vector3 spawnPos = GetActiveSpawnPoint();

        // Si une animation de mort est disponible, on lui délègue le timing
        if (DeathAnimationManager.Instance != null)
        {
            DeathAnimationManager.Instance.PlayDeathSequence(
                onRespawn: () => DoRevive(player, spawnPos),
                checkpointPosition: spawnPos
            );
        }
        else
        {
            // Fallback : comportement original avec respawnDelay simple
            StartCoroutine(RespawnRoutine(player, spawnPos));
        }
    }

    public void SetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        if (clamped <= activeSpawnIndex) return; // on ne régresse pas
        activeSpawnIndex = clamped;
        Debug.Log($"Checkpoint activé : spawn {activeSpawnIndex}");
    }

    // ════════════════════════════════════════════════════════════
    //  Logique interne
    // ════════════════════════════════════════════════════════════

    /// <summary>Coroutine fallback (sans DeathAnimationManager)</summary>
    IEnumerator RespawnRoutine(CharaController player, Vector3 spawnPos)
    {
        yield return new WaitForSeconds(respawnDelay);
        DoRevive(player, spawnPos);
    }

    /// <summary>
    /// Le respawn effectif : notifie les abonnés, repositionne et relance le joueur.
    /// L'événement OnPlayerRespawn est déclenché JUSTE AVANT le Revive() du joueur,
    /// pour que toutes les plateformes mobiles, portes, etc. soient déjà reset
    /// quand le joueur réapparaît visuellement.
    /// </summary>
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