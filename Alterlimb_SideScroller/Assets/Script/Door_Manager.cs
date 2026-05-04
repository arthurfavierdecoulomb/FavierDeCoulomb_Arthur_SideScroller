using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Porte avec 4 états d'animation : OpenDoor, CloseDoor, Idle_Closed, Idle_Opened.
/// Peut être ouverte de deux façons (au choix) :
///   - Mode Proximity : le joueur appuie sur clic droit en étant proche
///   - Mode Levers : tous les leviers requis doivent être activés
/// Dans les deux cas, la porte se referme automatiquement 3 secondes après que le joueur soit passé.
/// 
/// Hiérarchie de colliders attendue :
///   - Un Collider2D en mode Trigger (zone de détection du passage du joueur)
///   - Un Collider2D solide (NON-trigger) → assigné dans "Solid Collider", désactivé quand la porte est ouverte
/// </summary>
public class Door : MonoBehaviour
{
    public enum OpeningMode { Proximity, Levers }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Mode d'ouverture")]
    [SerializeField] OpeningMode mode = OpeningMode.Proximity;

    [Header("Mode Proximité")]
    [Tooltip("Distance maximale pour que le joueur puisse interagir")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1; // clic droit

    [Header("Mode Leviers")]
    [Tooltip("Liste des leviers qui doivent TOUS être activés pour ouvrir la porte")]
    [SerializeField] List<Lever> requiredLevers = new List<Lever>();

    [Header("Comportement commun")]
    [Tooltip("Délai avant fermeture automatique après que le joueur soit passé")]
    [SerializeField] float autoCloseDelay = 3f;
    [Tooltip("Détection du joueur via tag")]
    [SerializeField] string playerTag = "Player";

    [Header("Références")]
    [SerializeField] Animator animator;
    [SerializeField] Transform playerTransform; // optionnel, sinon trouvé via tag

    [Header("Collisions physiques")]
    [Tooltip("Collider2D solide qui bloque le joueur. Désactivé quand la porte est ouverte.")]
    [SerializeField] Collider2D solidCollider;
    [Tooltip("Délai entre le déclenchement de l'animation d'ouverture et la désactivation du collider")]
    [SerializeField] float openColliderDelay = 0.2f;
    [Tooltip("Délai entre le déclenchement de l'animation de fermeture et la réactivation du collider")]
    [SerializeField] float closeColliderDelay = 0.4f;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    bool isOpen;
    bool playerWasInside;       // pour détecter qu'il a "traversé"
    float closeTimer;
    bool autoCloseScheduled;

    // ── Hash des paramètres Animator ──
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

        // S'assure que le collider solide est bien actif au départ (porte fermée)
        if (solidCollider != null)
            solidCollider.enabled = true;
        else
            Debug.LogWarning($"[Door] '{name}' n'a pas de Solid Collider assigné. Le joueur pourra passer à travers.", this);

        // S'abonne aux événements des leviers requis
        if (mode == OpeningMode.Levers)
        {
            foreach (Lever lever in requiredLevers)
            {
                if (lever != null)
                    lever.OnLeverActivated += OnLeverStateChanged;
            }
        }
    }

    void OnDestroy()
    {
        // Désinscription propre
        foreach (Lever lever in requiredLevers)
        {
            if (lever != null)
                lever.OnLeverActivated -= OnLeverStateChanged;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Update
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (mode == OpeningMode.Proximity)
            HandleProximityMode();

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
        // Annule toute fermeture programmée tant qu'il est dans la porte
        autoCloseScheduled = false;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag(playerTag)) return;

        if (playerWasInside)
        {
            // Le joueur est passé : on programme la fermeture
            closeTimer = autoCloseDelay;
            autoCloseScheduled = true;
            playerWasInside = false;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Logique d'ouverture / fermeture
    // ════════════════════════════════════════════════════════════

    /// <summary>Appelé quand un levier change d'état</summary>
    void OnLeverStateChanged()
    {
        if (mode != OpeningMode.Levers) return;

        // Vérifie si TOUS les leviers requis sont activés
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

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;
        if (animator != null) animator.SetTrigger(OpenTrigger);

        // Désactive le collider solide après un petit délai
        // (pour que ça colle visuellement à l'animation d'ouverture)
        StartCoroutine(SetColliderAfterDelay(false, openColliderDelay));
    }

    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;
        if (animator != null) animator.SetTrigger(CloseTrigger);

        // Réactive le collider solide après un petit délai
        StartCoroutine(SetColliderAfterDelay(true, closeColliderDelay));
    }

    IEnumerator SetColliderAfterDelay(bool enabled, float delay)
    {
        if (solidCollider == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        solidCollider.enabled = enabled;
    }

    /// <summary>
    /// Permet à un Lever de demander si cette porte est sa cible
    /// (utilisé pour différencier vrais et faux leviers)
    /// </summary>
    public bool IsLeverRequired(Lever lever)
    {
        return mode == OpeningMode.Levers && requiredLevers.Contains(lever);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        if (mode == OpeningMode.Proximity)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
        else
        {
            // Trace une ligne vers chaque levier requis
            Gizmos.color = Color.yellow;
            foreach (Lever lever in requiredLevers)
            {
                if (lever != null)
                    Gizmos.DrawLine(transform.position, lever.transform.position);
            }
        }
    }
}