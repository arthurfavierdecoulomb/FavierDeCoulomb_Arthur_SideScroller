using UnityEngine;



[RequireComponent(typeof(Rigidbody2D))]
public class SinkingBarrel : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Enfoncement (joueur dessus)")]
    [Tooltip("Vitesse à laquelle le barril s'enfonce sous le poids du joueur")]
    [SerializeField] float sinkSpeed = 1.2f;
    [Tooltip("Profondeur maximale d'enfoncement (en unités Unity, par rapport à la position initiale)")]
    [SerializeField] float maxSinkDepth = 5f;

    [Header("Flottaison (joueur absent)")]
    [Tooltip("Vitesse à laquelle le barril remonte à sa position initiale")]
    [SerializeField] float floatBackSpeed = 2f;

    [Header("Wobble visuel (au repos)")]
    [Tooltip("Active le léger balancement vertical en permanence quand le joueur n'est pas dessus")]
    [SerializeField] bool useWobble = true;
    [Tooltip("Amplitude du wobble (en unités Unity)")]
    [SerializeField] float wobbleAmplitude = 0.1f;
    [Tooltip("Vitesse du wobble")]
    [SerializeField] float wobbleSpeed = 2f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";
    [Tooltip("Activez pour voir des Debug.Log et confirmer la détection")]
    [SerializeField] bool debugMode = false;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D rb;
    Vector3 startPosition;
    float wobbleTimer;

    // ── Détection robuste : on COMPTE le nombre de contacts joueur ──
    // (plus fiable que la normale qui peut s'inverser quand le barril bouge vite)
    int playerContactCount;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"[SinkingBarrel] '{name}' devrait avoir un Rigidbody2D en mode Kinematic.", this);
        }

        startPosition = transform.position;
    }

    // ════════════════════════════════════════════════════════════
    //  Mouvement physique
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        // Le wobble timer tourne TOUJOURS (continuité du mouvement)
        wobbleTimer += Time.fixedDeltaTime * wobbleSpeed;

        // Détermine si le joueur est sur le barril
        bool playerOnBarrel = playerContactCount > 0;

        Vector3 targetPos;

        if (playerOnBarrel)
        {
            // ── Le joueur est dessus → on s'enfonce ─────────────
            float lowestY = startPosition.y - maxSinkDepth;
            float newY = Mathf.MoveTowards(transform.position.y, lowestY, sinkSpeed * Time.fixedDeltaTime);
            targetPos = new Vector3(startPosition.x, newY, transform.position.z);
        }
        else
        {
            // ── Le joueur n'est PAS dessus → flottaison ─────────
            // 1. On calcule la position de repos avec wobble (même si on n'y est pas encore)
            float restY = startPosition.y;
            if (useWobble)
                restY += Mathf.Sin(wobbleTimer) * wobbleAmplitude;

            // 2. On se déplace vers cette cible animée
            float newY = Mathf.MoveTowards(transform.position.y, restY, floatBackSpeed * Time.fixedDeltaTime);
            targetPos = new Vector3(startPosition.x, newY, transform.position.z);
        }

        rb.MovePosition(targetPos);
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du joueur (robuste)
    // ════════════════════════════════════════════════════════════

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        // Vérifie que le joueur est BIEN au-dessus du barril (en Y)
        // C'est PLUS robuste que la normale qui peut foirer quand le barril bouge
        if (IsPlayerAbove(collision.transform))
        {
            playerContactCount++;
            if (debugMode) Debug.Log($"[SinkingBarrel] Joueur détecté DESSUS (count={playerContactCount})");
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        playerContactCount = Mathf.Max(0, playerContactCount - 1);
        if (debugMode) Debug.Log($"[SinkingBarrel] Joueur a quitté (count={playerContactCount})");
    }

    /// <summary>
    /// Vérifie si le joueur est positionné AU-DESSUS du barril (en coordonnée Y).
    /// Bien plus robuste que la normale de contact, qui peut devenir incohérente
    /// quand l'objet bouge vite (ce qui est notre cas, le barril descend).
    /// </summary>
    bool IsPlayerAbove(Transform playerTransform)
    {
        // Le joueur est considéré "au-dessus" si son centre est plus haut que le centre du barril
        return playerTransform.position.y > transform.position.y;
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPosition : transform.position;
        Vector3 lowest = origin + Vector3.down * maxSinkDepth;

        // Trait de la course de descente
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f);
        Gizmos.DrawLine(origin, lowest);

        // Position de repos
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, 0.2f);

        // Profondeur max
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lowest, 0.2f);

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawLine(lowest + Vector3.left * 0.5f, lowest + Vector3.right * 0.5f);
    }
}