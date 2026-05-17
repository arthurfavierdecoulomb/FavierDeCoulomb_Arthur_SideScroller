using UnityEngine;
using System;

/// <summary>
/// Gère le comptage des fusibles du niveau 2.
/// 
/// Deux compteurs distincts :
///   - fusesCarried   : fusibles ramassés mais pas encore insérés au panneau
///   - fusesInstalled : fusibles déjà insérés dans le panneau
/// 
/// Événements :
///   - OnFuseCollected / OnFuseInstalled : événements d'instance (UI, audio...)
///   - OnAllFusesInstalledStatic : événement STATIQUE déclenché quand les 5
///     fusibles sont installés. Statique pour que la porte (Door) puisse s'y
///     abonner dès son Awake() sans dépendre de l'ordre d'initialisation.
/// 
/// Les fusibles sont conservés au respawn (choix de game design).
/// </summary>
public class FuseManager : MonoBehaviour
{
    public static FuseManager Instance { get; private set; }

    /// <summary>
    /// Événement statique déclenché quand TOUS les fusibles sont installés.
    /// Statique → la Door peut s'y abonner dans Awake() sans souci d'ordre.
    /// </summary>
    public static event Action OnAllFusesInstalledStatic;

    [Header("Configuration")]
    [SerializeField] int totalFuses = 5;

    // Événements d'instance (pour l'UI, l'audio, le panneau...)
    public event Action OnFuseCollected;
    public event Action OnFuseInstalled;

    int fusesCarried;
    int fusesInstalled;

    public int FusesCarried => fusesCarried;
    public int FusesInstalled => fusesInstalled;
    public int TotalFuses => totalFuses;
    public bool AllFusesInstalled => fusesInstalled >= totalFuses;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    public void CollectFuse()
    {
        fusesCarried++;
        Debug.Log($"[FuseManager] Fusible ramassé. En main : {fusesCarried}");
        OnFuseCollected?.Invoke();
    }

    public bool TryInstallOneFuse()
    {
        if (fusesCarried <= 0) return false;
        if (fusesInstalled >= totalFuses) return false;

        fusesCarried--;
        fusesInstalled++;
        Debug.Log($"[FuseManager] Fusible installé. Panneau : {fusesInstalled}/{totalFuses}");

        OnFuseInstalled?.Invoke();

        if (fusesInstalled >= totalFuses)
        {
            Debug.Log("[FuseManager] Tous les fusibles sont installés !");
            OnAllFusesInstalledStatic?.Invoke();
        }

        return true;
    }
}