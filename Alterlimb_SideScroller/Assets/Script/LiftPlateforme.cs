using UnityEngine;
using System.Collections;

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
/// Bounce aux limites :
///   - Quand la plateforme atteint upLimit ou downLimit, elle joue un bounce
///     d'overshoot (dépassement + rebonds amortis dans la direction du mouvement).
///   - Le bounce NE se joue PAS quand le joueur relâche la touche en plein
///     mouvement (pour préserver le feeling de précision).
///   - Le joueur sur la plateforme suit le mouvement du bounce.
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

    [Header("Bounce aux limites")]
    [Tooltip("Amplitude du premier overshoot (en unités Unity). 0.3 = 30cm.")]
    [SerializeField] float bounceAmplitude = 0.3f;
    [Tooltip("Durée totale du bounce (en secondes)")]
    [SerializeField] float bounceDuration = 0.5f;
    [Tooltip("Nombre de rebonds amortis (1 = overshoot + retour, 2 = overshoot + rebond + retour, etc.)")]
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Tooltip("Facteur d'amortissement entre rebonds (0.5 = chaque rebond fait la moitié du précédent)")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.4f;

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

    // ── État du bounce ──
    bool isBouncing;
    Coroutine bounceCoroutine;

    // Mémorise la direction du mouvement précédent pour détecter l'arrivée à une limite
    int previousMoveDir;

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
    /// Remet la plateforme à sa position initiale et coupe tout mouvement / bounce en cours.
    /// </summary>
    void ResetToStartPosition()
    {
        // Stoppe un éventuel bounce en cours
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }
        isBouncing = false;

        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        platformVelocity = Vector2.zero;

        previousMoveDir = 0;

        DetachPlayer();
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
        // Pendant un bounce, la coroutine gère tout : on ne lit ni inputs ni mouvement
        if (isBouncing) return;

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

                // ── Détection de l'arrivée à une limite ───────────
                // Si le joueur appuie pour aller dans une direction MAIS qu'on
                // était en mouvement dans cette direction au tick précédent ET
                // qu'on vient juste d'atteindre la limite → on déclenche le bounce.
                if ((blockedUp && previousMoveDir > 0) || (blockedDown && previousMoveDir < 0))
                {
                    // Snap propre à la limite avant le bounce
                    float snappedY = blockedUp ? maxY : minY;
                    rb.position = new Vector2(startPosition.x, snappedY);

                    float bounceDirection = blockedUp ? 1f : -1f;
                    StartBounce(bounceDirection);

                    previousMoveDir = 0;
                    SetMoveDir(0);
                    return;
                }

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

        // Pendant la descente, on force la velocity du joueur à suivre
        // (sinon la gravité le décolle de la plateforme)
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
        previousMoveDir = newMoveDir;
    }

    // ════════════════════════════════════════════════════════════
    //  Bounce : coroutine d'overshoot avec rebonds amortis
    // ════════════════════════════════════════════════════════════

    void StartBounce(float direction)
    {
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(BounceRoutine(direction));
    }

    /// <summary>
    /// Joue un bounce d'overshoot dans la direction donnée.
    /// La plateforme dépasse la limite, puis revient avec des rebonds amortis
    /// avant de se stabiliser exactement sur la limite.
    /// Le joueur sur la plateforme suit le mouvement via sa velocity.
    /// </summary>
    /// <param name="direction">+1 si on a atteint la limite haute, -1 pour la limite basse</param>
    IEnumerator BounceRoutine(float direction)
    {
        isBouncing = true;

        // Position de référence : la limite exacte où l'on vient de s'arrêter
        float baseY = rb.position.y;
        float previousY = baseY;
        float elapsed = 0f;

        // Pendant le bounce, la velocity Rigidbody reste à 0
        // (on déplace via rb.position pour un contrôle frame-perfect)
        rb.linearVelocity = Vector2.zero;

        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;

            // Formule : sinus amorti
            //   sin(2π × bounceCount × t) → oscillation
            //   × (1-t)^(1-damping) → amortissement
            //   × bounceAmplitude × direction → amplitude et sens
            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float offset = oscillation * dampingCurve * bounceAmplitude * direction;

            float newY = baseY + offset;
            float deltaY = newY - previousY;

            rb.position = new Vector2(startPosition.x, newY);

            // Le joueur suit le bounce : on convertit le delta en velocity équivalente
            if (playerOnPlatform && playerRb != null)
            {
                float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
                Vector2 v = playerRb.linearVelocity;
                v.y = deltaY / dt;
                playerRb.linearVelocity = v;
            }

            previousY = newY;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Snap final exact à la limite pour éviter la dérive numérique
        rb.position = new Vector2(startPosition.x, baseY);

        isBouncing = false;
        bounceCoroutine = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Animator helper
    // ════════════════════════════════════════════════════════════

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