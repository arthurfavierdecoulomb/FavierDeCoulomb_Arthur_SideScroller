using UnityEngine;

/// <summary>
/// Monte-charge vertical contrôlable par le joueur.
/// 
/// Fonctionnement :
///   - Le joueur monte sur la plateforme (détecté via collision sur le dessus)
///   - Tant qu'il est dessus, il peut appuyer sur upKey pour monter ou downKey pour descendre
///   - La plateforme s'arrête automatiquement aux limites configurées
///   - Le joueur suit le mouvement via application directe du delta (pas de parenting)
/// 
/// Animator :
///   - Paramètre Int "moveDir" :  1 = monte,  -1 = descend,  0 = arrêt
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class LiftPlatform : MonoBehaviour
{
    [Header("Contrôles")]
    [SerializeField] KeyCode upKey = KeyCode.W;
    [SerializeField] KeyCode downKey = KeyCode.S;

    [Header("Mouvement")]
    [Tooltip("Vitesse de déplacement vertical (unités/seconde)")]
    [SerializeField] float moveSpeed = 3f;
    [Tooltip("Distance maximale vers le haut depuis la position de départ")]
    [SerializeField] float upLimit = 5f;
    [Tooltip("Distance maximale vers le bas depuis la position de départ")]
    [SerializeField] float downLimit = 0f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";
    [Tooltip("Angle maximal (en degrés) du contact pour considérer que le joueur est SUR la plateforme.")]
    [SerializeField] float maxStandingAngle = 45f;
    [Tooltip("Délai (secondes) sans contact avant de considérer que le joueur est parti.")]
    [SerializeField] float contactLostThreshold = 0.1f;

    [Header("Animation")]
    [SerializeField] Animator animator;

    // Tolérance pour la détection "à la limite" (évite le jitter autour des bornes)
    const float LIMIT_EPSILON = 0.01f;

    // Hash du paramètre Animator (perf : évite la recherche par string)
    static readonly int MoveDirHash = Animator.StringToHash("moveDir");

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    Vector2 startPosition;
    Vector2 lastPosition;
    bool playerOnPlatform;
    float timeSinceLastContact;
    int currentMoveDir; // cache pour éviter de spammer l'Animator

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;

        if (animator == null) animator = GetComponent<Animator>();

        startPosition = rb.position;
        lastPosition = startPosition;

        SetMoveDir(0);
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du joueur (collision sur le dessus)
    // ════════════════════════════════════════════════════════════

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            float angle = Vector2.Angle(contact.normal, Vector2.down);
            if (angle <= maxStandingAngle)
            {
                if (!playerOnPlatform)
                {
                    playerRb = collision.collider.attachedRigidbody;
                    playerOnPlatform = true;
                }
                timeSinceLastContact = 0f;
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;
        DetachPlayer();
    }

    void Update()
    {
        if (!playerOnPlatform) return;

        timeSinceLastContact += Time.deltaTime;
        if (timeSinceLastContact > contactLostThreshold)
            DetachPlayer();
    }

    void DetachPlayer()
    {
        playerOnPlatform = false;
        playerRb = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Contrôle du mouvement
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        int newMoveDir = 0;

        // 1. Lire l'input et bouger la plateforme si le joueur est dessus
        if (playerOnPlatform)
        {
            if (Input.GetKey(upKey)) newMoveDir += 1;
            if (Input.GetKey(downKey)) newMoveDir -= 1;

            if (newMoveDir != 0)
            {
                Vector2 currentPos = rb.position;
                float minY = startPosition.y - downLimit;
                float maxY = startPosition.y + upLimit;

                // Vérifie si la direction demandée est BLOQUÉE par une limite
                bool blockedUp = newMoveDir > 0 && currentPos.y >= maxY - LIMIT_EPSILON;
                bool blockedDown = newMoveDir < 0 && currentPos.y <= minY + LIMIT_EPSILON;

                if (blockedUp || blockedDown)
                {
                    // Bloqué à une limite : animation Idle, on ne bouge pas
                    newMoveDir = 0;
                }
                else
                {
                    // Mouvement autorisé : on applique le delta clampé proprement
                    float deltaY = newMoveDir * moveSpeed * Time.fixedDeltaTime;
                    float newY = Mathf.Clamp(currentPos.y + deltaY, minY, maxY);
                    rb.MovePosition(new Vector2(currentPos.x, newY));
                }
            }
        }

        // 2. Met à jour l'animation
        SetMoveDir(newMoveDir);

        // 3. Calculer de combien la plateforme a bougé cette frame
        Vector2 platformDelta = rb.position - lastPosition;
        lastPosition = rb.position;

        // 4. Appliquer le même delta au joueur s'il est dessus
        if (playerOnPlatform && playerRb != null && platformDelta.sqrMagnitude > 0.0001f)
        {
            playerRb.position += platformDelta;
        }
    }

    /// <summary>
    /// Met à jour le paramètre moveDir de l'Animator, uniquement s'il a changé
    /// (évite de spammer l'Animator chaque frame).
    /// </summary>
    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos : ligne verte montrant la course complète
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        Vector2 referencePos = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 topPoint = referencePos + Vector2.up * upLimit;
        Vector2 bottomPoint = referencePos + Vector2.down * downLimit;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(topPoint, bottomPoint);
        Gizmos.DrawWireSphere(topPoint, 0.15f);
        Gizmos.DrawWireSphere(bottomPoint, 0.15f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(referencePos, 0.1f);
    }
}