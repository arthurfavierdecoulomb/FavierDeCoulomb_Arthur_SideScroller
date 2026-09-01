using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseLock : MonoBehaviour
{
    public static PauseLock Instance { get; private set; }

    [Header("Verrous automatiques")]
    [SerializeField] private bool lockOnDeath = true;
    [SerializeField] private float graceAfterRespawn = 0.4f;
    [SerializeField] private float deathLockTimeout = 12f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = false;

    private static readonly HashSet<string> reasons = new HashSet<string>();

    public static bool IsLocked => reasons.Count > 0;

    private Coroutine unlockRoutine;
    private Coroutine timeoutRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        reasons.Clear();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (!lockOnDeath)
            return;

        CharaController.OnPlayerDied += HandlePlayerDied;
        SpawnManager.OnPlayerRespawn += HandlePlayerRespawn;
    }

    private void OnDisable()
    {
        CharaController.OnPlayerDied -= HandlePlayerDied;
        SpawnManager.OnPlayerRespawn -= HandlePlayerRespawn;

        Unlock("death");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Lock(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return;

        reasons.Add(reason);
    }

    public static void Unlock(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return;

        reasons.Remove(reason);
    }

    public static void UnlockAll()
    {
        reasons.Clear();
    }

    private void HandlePlayerDied()
    {
        Lock("death");

        if (logDiagnostics)
            Debug.Log("[PauseLock] Pause verrouillée : le joueur est mort.", this);

        if (unlockRoutine != null)
        {
            StopCoroutine(unlockRoutine);
            unlockRoutine = null;
        }

        if (timeoutRoutine != null)
            StopCoroutine(timeoutRoutine);

        timeoutRoutine = StartCoroutine(TimeoutRoutine());
    }

    private void HandlePlayerRespawn()
    {
        if (unlockRoutine != null)
            StopCoroutine(unlockRoutine);

        unlockRoutine = StartCoroutine(UnlockAfterGrace());
    }

    private IEnumerator UnlockAfterGrace()
    {
        if (graceAfterRespawn > 0f)
            yield return new WaitForSecondsRealtime(graceAfterRespawn);

        Unlock("death");

        if (timeoutRoutine != null)
        {
            StopCoroutine(timeoutRoutine);
            timeoutRoutine = null;
        }

        if (logDiagnostics)
            Debug.Log("[PauseLock] Pause déverrouillée.", this);

        unlockRoutine = null;
    }

    private IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSecondsRealtime(deathLockTimeout);

        if (IsLocked)
        {
            Debug.LogWarning($"[PauseLock] Verrou de mort levé de force après {deathLockTimeout}s : aucun respawn reçu. Vérifie que SpawnManager déclenche bien OnPlayerRespawn.", this);
            Unlock("death");
        }

        timeoutRoutine = null;
    }
}