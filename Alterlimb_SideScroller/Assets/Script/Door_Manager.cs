using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Porte avec 4 modes d'ouverture : Proximity, Levers, OnDroneKilled, Fuses.
/// 
/// Option stayOpenForever : si cochée, la porte reste ouverte définitivement
/// après ouverture (pas de fermeture auto).
/// 
/// Message verrouillé : quand le joueur fait un clic droit sur une porte
/// encore verrouillée (Levers/OnDroneKilled/Fuses pas satisfaits), un message
/// du TutorialManager s'affiche (id configuré dans 'lockedMessageId').
/// </summary>
public class Door : MonoBehaviour
{
    public enum OpeningMode { Proximity, Levers, OnDroneKilled, Fuses }

    [Header("Mode d'ouverture")]
    [SerializeField] OpeningMode mode = OpeningMode.Proximity;

    [Header("Mode Proximité")]
    [Tooltip("Distance maximale pour que le joueur puisse interagir")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1; // clic droit

    [Header("Mode Leviers")]
    [Tooltip("Liste des leviers qui doivent TOUS être activés pour ouvrir la porte")]
    [SerializeField] List<Lever> requiredLevers = new List<Lever>();

    [Header("Mode OnDroneKilled")]
    [Tooltip("Drone dont la mort déverrouille cette porte")]
    [SerializeField] DroneEnemy targetDrone;

    [Header("Comportement commun")]
    [Tooltip("Si coché, la porte reste ouverte définitivement après ouverture (pas de fermeture auto)")]
    [SerializeField] bool stayOpenForever = false;
    [Tooltip("Délai avant fermeture automatique après que le joueur soit passé")]
    [SerializeField] float autoCloseDelay = 3f;
    [Tooltip("Détection du joueur via tag")]
    [SerializeField] string playerTag = "Player";

    [Header("Message si verrouillée")]
    [Tooltip("Id du message TutorialManager à afficher si le joueur interagit avec la porte verrouillée. Laisser vide pour aucun message.")]
    [SerializeField] string lockedMessageId = "";
    [Tooltip("Distance max pour déclencher le message verrouillé au clic droit")]
    [SerializeField] float lockedMessageRange = 2.5f;

    [Header("Références")]
    [SerializeField] Animator animator;
    [SerializeField] Transform playerTransform; // optionnel, sinon trouvé via tag

    [Header("Mode Fusibles")]
    [Tooltip("La porte s'ouvre quand tous les fusibles sont installés dans le FuseManager")]
    [SerializeField] bool useFuseManager = true;

    [Header("Collisions physiques")]
    [Tooltip("Collider2D solide qui bloque le joueur. Désactivé quand la porte est ouverte.")]
    [SerializeField] Collider2D solidCollider;
    [SerializeField] float openColliderDelay = 0.2f;
    [SerializeField] float closeColliderDelay = 0.4f;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    bool isOpen;
    bool playerWasInside;
    float closeTimer;
    bool autoCloseScheduled;

    static readonly int OpenTrigger = Animator.StringToHash("Open");
    static readonly int CloseTrigger = Animator.StringToHash("Close");

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerTransform = p.transform;
        }

        if (solidCollider != null)
            solidCollider.enabled = true;
        else
            Debug.LogWarning($"[Door] '{name}' n'a pas de Solid Collider assigné. Le joueur pourra passer à travers.", this);

        switch (mode)
        {
            case OpeningMode.Levers:
                foreach (Lever lever in requiredLevers)
                {
                    if (lever != null)
                        lever.OnLeverActivated += OnLeverStateChanged;
                }
                break;

            case OpeningMode.OnDroneKilled:
                if (targetDrone == null)
                    Debug.LogWarning($"[Door] '{name}' est en mode OnDroneKilled mais aucun targetDrone n'est assigné.", this);
                DroneEnemy.OnDroneDied += OnDroneDiedHandler;
                break;

            case OpeningMode.Fuses:
                FuseManager.OnAllFusesInstalledStatic += OnAllFusesHandler;
                break;
        }
    }

    void OnDestroy()
    {
        foreach (Lever lever in requiredLevers)
        {
            if (lever != null)
                lever.OnLeverActivated -= OnLeverStateChanged;
        }

        DroneEnemy.OnDroneDied -= OnDroneDiedHandler;
        FuseManager.OnAllFusesInstalledStatic -= OnAllFusesHandler;
    }

    // ════════════════════════════════════════════════════════════
    //  Update
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (mode == OpeningMode.Proximity)
            HandleProximityMode();
        else
            HandleLockedInteraction();

        HandleAutoClose();
    }

    void HandleProximityMode()
    {
        if (playerTransform == null || isOpen) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            OpenDoor();
        }
    }

    /// <summary>
    /// Pour les modes Levers/OnDroneKilled/Fuses : si le joueur fait un clic droit
    /// près de la porte alors qu'elle est encore verrouillée, on affiche un message.
    /// </summary>
    void HandleLockedInteraction()
    {
        if (isOpen) return;
        if (string.IsNullOrEmpty(lockedMessageId)) return;
        if (playerTransform == null) return;

        if (Input.GetKeyDown(interactionKey))
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            if (distance <= lockedMessageRange)
            {
                if (TutorialManager.Instance != null)
                    TutorialManager.Instance.ShowMessageById(lockedMessageId);
            }
        }
    }

    void HandleAutoClose()
    {
        if (!isOpen || !autoCloseScheduled) return;

        closeTimer -= Time.deltaTime;
        if (closeTimer <= 0f)
        {
            CloseDoor();
            autoCloseScheduled = false;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du passage du joueur
    // ════════════════════════════════════════════════════════════

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag(playerTag)) return;

        playerWasInside = true;
        autoCloseScheduled = false;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag(playerTag)) return;

        if (playerWasInside)
        {
            playerWasInside = false;

            // Si la porte doit rester ouverte définitivement, on ne programme rien
            if (stayOpenForever) return;

            closeTimer = autoCloseDelay;
            autoCloseScheduled = true;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Logique d'ouverture / fermeture
    // ════════════════════════════════════════════════════════════

    void OnLeverStateChanged()
    {
        if (mode != OpeningMode.Levers) return;

        bool allActivated = true;
        foreach (Lever lever in requiredLevers)
        {
            if (lever == null || !lever.IsActivated)
            {
                allActivated = false;
                break;
            }
        }

        if (allActivated && !isOpen)
            OpenDoor();
    }

    void OnDroneDiedHandler(DroneEnemy deadDrone)
    {
        if (mode != OpeningMode.OnDroneKilled) return;
        if (deadDrone != targetDrone) return;
        if (isOpen) return;

        OpenDoor();
    }

    void OnAllFusesHandler()
    {
        if (mode != OpeningMode.Fuses) return;
        if (isOpen) return;

        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;
        if (animator != null) animator.SetTrigger(OpenTrigger);

        StartCoroutine(SetColliderAfterDelay(false, openColliderDelay));
    }

    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;
        if (animator != null) animator.SetTrigger(CloseTrigger);

        StartCoroutine(SetColliderAfterDelay(true, closeColliderDelay));
    }

    IEnumerator SetColliderAfterDelay(bool enabled, float delay)
    {
        if (solidCollider == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        solidCollider.enabled = enabled;
    }

    public bool IsLeverRequired(Lever lever)
    {
        return mode == OpeningMode.Levers && requiredLevers.Contains(lever);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        switch (mode)
        {
            case OpeningMode.Proximity:
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, interactionRange);
                break;

            case OpeningMode.Levers:
                Gizmos.color = Color.yellow;
                foreach (Lever lever in requiredLevers)
                {
                    if (lever != null)
                        Gizmos.DrawLine(transform.position, lever.transform.position);
                }
                break;

            case OpeningMode.OnDroneKilled:
                if (targetDrone != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(transform.position, targetDrone.transform.position);
                    Gizmos.DrawWireSphere(targetDrone.transform.position, 0.5f);
                }
                break;
        }

        // Zone du message verrouillé (si configuré)
        if (!string.IsNullOrEmpty(lockedMessageId))
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, lockedMessageRange);
        }
    }
}