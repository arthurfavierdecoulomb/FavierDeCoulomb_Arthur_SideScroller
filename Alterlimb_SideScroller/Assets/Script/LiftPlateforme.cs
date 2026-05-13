using UnityEngine;

/// <summary>
/// Monte-charge vertical contrôlable par le joueur.
/// 
/// Fonctionnement :
///   - Le joueur monte sur la plateforme (détecté via collision sur le dessus)
///   - Au lieu de parenter le joueur, on lui transmet la vélocité de la plateforme
///     directement sur son Rigidbody2D → la physique gère tout proprement,
///     pas de conflit gravité/parenting, montée et descente symétriques.
///   - Le joueur peut appuyer sur upKey/downKey pour monter ou descendre
///   - La plateforme s'arrête automatiquement aux limites configurées
/// 
/// Reset au respawn :
///   - S'abonne à SpawnManager.OnPlayerRespawn pour revenir à sa position
///     initiale quand le joueur meurt et respawn.
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

    const float LIMIT_EPSILON = 0.01f;
    static readonly int MoveDirHash = Animator.StringToHash("moveDir");

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    Vector2 startPosition;
    Vector2 platformVelocity;
    bool playerOnPlatform;
    float timeSinceLastContact;
    int currentMoveDir;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null) animator = GetComponent<Animator>();

        startPosition = rb.position;
        SetMoveDir(0);
    }

    // ════════════════════════════════════════════════════════════
    //  Abonnement / désabonnement à l'événement de respawn
    // ════════════════════════════════════════════════════════════

    void OnEnable()
    {
        SpawnManager.OnPlayerRespawn += ResetToStartPosition;
    }

    void OnDisable()
    {
        SpawnManager.OnPlayerRespawn -= ResetToStartPosition;
    }

    /// <summary>
    /// Remet la plateforme à sa position initiale et coupe tout mouvement en cours.
    /// Appelé automatiquement quand le joueur respawn.
    /// </summary>
    void ResetToStartPosition()
    {
        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        platformVelocity = Vector2.zero;

        // Détache le joueur s'il était dessus au moment de la mort
        DetachPlayer();

        // Reset de l'animation
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
        platformVelocity = Vector2.zero;

        if (playerOnPlatform)
        {
            if (Input.GetKey(upKey)) newMoveDir += 1;
            if (Input.GetKey(downKey)) newMoveDir -= 1;

            if (newMoveDir != 0)
            {
                Vector2 currentPos = rb.position;
                float minY = startPosition.y - downLimit;
                float maxY = startPosition.y + upLimit;

                bool blockedUp = newMoveDir > 0 && currentPos.y >= maxY - LIMIT_EPSILON;
                bool blockedDown = newMoveDir < 0 && currentPos.y <= minY + LIMIT_EPSILON;

                if (blockedUp || blockedDown)
                {
                    newMoveDir = 0;
                }
                else
                {
                    platformVelocity = new Vector2(0f, newMoveDir * moveSpeed);
                }
            }
        }

        rb.linearVelocity = platformVelocity;

        if (playerOnPlatform && playerRb != null && platformVelocity.y < 0f)
        {
            Vector2 v = playerRb.linearVelocity;
            if (v.y <= 0f)
            {
                v.y = platformVelocity.y;
                playerRb.linearVelocity = v;
            }
        }

        SetMoveDir(newMoveDir);
    }

    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
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