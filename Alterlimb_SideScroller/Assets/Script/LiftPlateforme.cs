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

        // 1. Bouger la plateforme via Rigidbody2D.linearVelocity
        //    → en Kinematic, ça fait avancer la plateforme à vitesse constante
        //      sans subir la physique, mais en restant "physiquement cohérente"
        //      pour les contacts (le joueur dessus suit naturellement)
        rb.linearVelocity = platformVelocity;

        // 2. Si la plateforme DESCEND, on force le joueur à suivre pour éviter
        //    qu'il se "décolle" à cause de la gravité (qui tombe moins vite
        //    que la plateforme à pleine vitesse n'est PAS le cas ici, donc on
        //    aide la physique en collant le joueur à la plateforme)
        if (playerOnPlatform && playerRb != null && platformVelocity.y < 0f)
        {
            // On remplace la composante Y du joueur par celle de la plateforme
            // UNIQUEMENT si le joueur ne saute pas (vy <= 0)
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