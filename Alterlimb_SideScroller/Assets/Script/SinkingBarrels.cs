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
    [Tooltip("Active le léger balancement vertical quand le barril est à sa position de repos")]
    [SerializeField] bool useWobble = true;
    [Tooltip("Amplitude du wobble (en unités Unity)")]
    [SerializeField] float wobbleAmplitude = 0.05f;
    [Tooltip("Vitesse du wobble")]
    [SerializeField] float wobbleSpeed = 2f;

    [Header("Détection joueur")]
    [Tooltip("Tag du joueur")]
    [SerializeField] string playerTag = "Player";
    [Tooltip("Seuil minimum pour considérer que le joueur est BIEN au-dessus (et non collé sur le côté)")]
    [Range(0.1f, 1f)]
    [SerializeField] float topContactThreshold = 0.5f;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D rb;
    Vector3 startPosition;
    bool playerOnBarrel;
    float wobbleTimer;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Sécurité : un barril a forcément un Rigidbody2D Kinematic
        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"[SinkingBarrel] '{name}' devrait avoir un Rigidbody2D en mode Kinematic.", this);
        }

        startPosition = transform.position;
    }

    // ════════════════════════════════════════════════════════════
    //  Mouvement physique (FixedUpdate pour cohérence)
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentPos;

        if (playerOnBarrel)
        {
            // Le joueur est dessus → on s'enfonce
            float lowestY = startPosition.y - maxSinkDepth;
            float newY = Mathf.MoveTowards(currentPos.y, lowestY, sinkSpeed * Time.fixedDeltaTime);
            targetPos = new Vector3(startPosition.x, newY, currentPos.z);
        }
        else
        {
            // Le joueur n'est pas dessus → on remonte vers la position initiale
            float newY = Mathf.MoveTowards(currentPos.y, startPosition.y, floatBackSpeed * Time.fixedDeltaTime);

            // Wobble si on est arrivé (ou très proche) à la position de repos
            if (useWobble && Mathf.Abs(newY - startPosition.y) < 0.01f)
            {
                wobbleTimer += Time.fixedDeltaTime * wobbleSpeed;
                newY = startPosition.y + Mathf.Sin(wobbleTimer) * wobbleAmplitude;
            }
            else
            {
                wobbleTimer = 0f;
            }

            targetPos = new Vector3(startPosition.x, newY, currentPos.z);
        }

        rb.MovePosition(targetPos);
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du joueur
    // ════════════════════════════════════════════════════════════

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        // Vérifie que le joueur est bien AU-DESSUS du barril,
        // pas en train de le pousser sur le côté ou par en bas
        if (IsContactFromAbove(collision))
        {
            playerOnBarrel = true;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Re-vérifie en continu : si le joueur saute et retombe sur le côté,
        // ou si le contact change de nature, on met à jour playerOnBarrel
        if (!collision.gameObject.CompareTag(playerTag)) return;

        playerOnBarrel = IsContactFromAbove(collision);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;
        playerOnBarrel = false;
    }

    /// <summary>
    /// Vérifie si l'un des points de contact a une normale qui pointe vers le BAS,
    /// ce qui signifie que le joueur appuie depuis le DESSUS du barril.
    /// </summary>
    bool IsContactFromAbove(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            // contact.normal pointe DEPUIS le barril VERS le joueur.
            // Si normal.y > seuil → le joueur est au-dessus.
            if (contact.normal.y > topContactThreshold)
                return true;
        }
        return false;
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPosition : transform.position;
        Vector3 lowest = origin + Vector3.down * maxSinkDepth;

        // Trait de la course de descente
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f); // bleu eau
        Gizmos.DrawLine(origin, lowest);

        // Position de repos
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, 0.2f);

        // Profondeur max (= où la dead_zone devrait être)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lowest, 0.2f);

        // Ligne horizontale en pointillé pour la profondeur max
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawLine(lowest + Vector3.left * 0.5f, lowest + Vector3.right * 0.5f);
    }
}