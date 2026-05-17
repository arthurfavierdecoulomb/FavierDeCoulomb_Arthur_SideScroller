using UnityEngine;
using System;

/// <summary>
/// Drone ennemi volant — version "Animator" (utilise un spritesheet animé).
/// 
/// Détection : mode GARDIEN. Le drone ne réagit au joueur que si celui-ci est
/// dans une zone de détection rectangulaire FIXE posée dans le niveau.
/// Si le joueur sort de la zone, le drone le perd et retourne en patrouille.
/// 
/// État interne (state machine logique) :
///   - Patrol : patrouille A↔B
///   - Chase : poursuit le joueur sans tirer
///   - Attack : tire le laser
///   - Recharge : s'éloigne pour "recharger" puis revient
/// 
/// État envoyé à l'Animator (paramètre Int "State") :
///   - 0 = Idle, 1 = Chase, 2 = Death
/// 
/// Laser : deux sorties (gauche/droite). Le DroneEnemy informe le DroneLaser
/// de quel côté tirer à chaque flip, pour éviter le "laser au cul".
/// 
/// Notification de mort : event statique OnDroneDied (pour les Door).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DroneEnemy : MonoBehaviour
{
    public enum DroneState { Patrol, Chase, Attack, Recharge }

    // ════════════════════════════════════════════════════════════
    //  Event statique : notifie qu'un drone est mort (Door OnDroneKilled)
    // ════════════════════════════════════════════════════════════
    public static event Action<DroneEnemy> OnDroneDied;

    // Constantes pour le paramètre "State" de l'Animator
    const int ANIM_IDLE = 0;
    const int ANIM_CHASE = 1;
    const int ANIM_DEATH = 2;

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Patrouille")]
    [SerializeField] Transform patrolPointA;
    [SerializeField] Transform patrolPointB;
    [SerializeField] float patrolSpeed = 2.5f;
    [SerializeField] float pointReachedDistance = 0.3f;

    [Header("Détection joueur — Zone gardien")]
    [Tooltip("Centre de la zone de détection rectangulaire FIXE (un Transform vide posé dans le niveau)")]
    [SerializeField] Transform detectionZoneCenter;
    [Tooltip("Taille de la zone de détection rectangulaire (largeur × hauteur)")]
    [SerializeField] Vector2 detectionZoneSize = new Vector2(10f, 6f);
    [SerializeField] string playerTag = "Player";
    [Tooltip("Layers qui bloquent la ligne de vue (murs, sol). NE PAS inclure le layer du joueur.")]
    [SerializeField] LayerMask lineOfSightObstacles;

    [Header("Chasse")]
    [SerializeField] float chaseSpeed = 4f;
    [SerializeField] float attackDistance = 5f;
    [Tooltip("Hauteur supplémentaire du drone par rapport au joueur")]
    [SerializeField] float verticalOffset = 2.5f;

    [Header("Attaque (laser continu)")]
    [SerializeField] float attackDuration = 2.5f;

    [Header("Recharge")]
    [SerializeField] float rechargeDuration = 2f;
    [SerializeField] float rechargeDistance = 9f;
    [SerializeField] float rechargeSpeed = 5f;

    [Header("Vie")]
    [SerializeField] float maxHealth = 100f;
    [Tooltip("Si activé : la scie ne fait des dégâts QUE si le drone est accroché par le grappin")]
    [SerializeField] bool damageOnlyWhenHooked = false;
    [Tooltip("Durée de l'animation de mort avant que le GameObject soit détruit")]
    [SerializeField] float deathAnimationDuration = 1.5f;

    [Header("Explosion à la mort")]
    [Tooltip("Prefab de Particle System à instancier lors de l'explosion. Déclenchée par Animation Event.")]
    [SerializeField] GameObject explosionPrefab;
    [Tooltip("Offset de position pour l'explosion par rapport au centre du drone")]
    [SerializeField] Vector2 explosionOffset = Vector2.zero;
    [Tooltip("Durée de vie de l'explosion avant destruction (en secondes)")]
    [SerializeField] float explosionLifetime = 3f;

    [Header("Flip horizontal")]
    [Tooltip("Vitesse minimale pour déclencher un flip (anti-jitter)")]
    [SerializeField] float flipThreshold = 0.5f;
    [Tooltip("Si le sprite original regarde à GAUCHE par défaut, coche cette case")]
    [SerializeField] bool spriteDefaultFacesLeft = true;

    [Header("Références")]
    [SerializeField] Animator animator;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Transform player;
    DroneLaser laser;

    DroneState currentState;
    Transform currentPatrolTarget;
    float stateTimer;
    float currentHealth;
    bool isDying;
    bool deathNotified;

    // ── Système Grappin ─────────────────────────────────────────
    public bool isHooked { get; private set; }

    // ── Accesseurs publics ──────────────────────────────────────
    public DroneState State => currentState;
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsAlive => currentHealth > 0f && !isDying;

    static readonly int AnimStateHash = Animator.StringToHash("State");

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        laser = GetComponent<DroneLaser>();
        currentHealth = maxHealth;

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;

        currentState = DroneState.Patrol;
        currentPatrolTarget = patrolPointA;

        SetAnimatorState(ANIM_IDLE);
    }

    // ════════════════════════════════════════════════════════════
    //  Update : détection + state machine
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (isDying) return;

        if (isHooked)
        {
            if (laser != null) laser.SetFiring(false);
            return;
        }

        if (player == null) return;

        stateTimer += Time.deltaTime;
        EvaluateStateTransitions();
        UpdateFlip();
    }

    void FixedUpdate()
    {
        if (isDying) return;

        if (isHooked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (player == null) return;

        switch (currentState)
        {
            case DroneState.Patrol: PatrolBehavior(); break;
            case DroneState.Chase: ChaseBehavior(); break;
            case DroneState.Attack: AttackBehavior(); break;
            case DroneState.Recharge: RechargeBehavior(); break;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Transitions d'état (mode gardien)
    // ════════════════════════════════════════════════════════════

    void EvaluateStateTransitions()
    {
        bool playerDetected = IsPlayerInDetectionZone();
        bool hasLineOfSight = HasLineOfSightTo(player.position);
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case DroneState.Patrol:
                if (playerDetected) ChangeState(DroneState.Chase);
                break;

            case DroneState.Chase:
                // Mode gardien : si le joueur sort de la zone, on le perd
                if (!playerDetected)
                {
                    ChangeState(DroneState.Patrol);
                    break;
                }
                if (hasLineOfSight && distToPlayer <= attackDistance + 1f)
                    ChangeState(DroneState.Attack);
                break;

            case DroneState.Attack:
                if (!playerDetected)
                {
                    ChangeState(DroneState.Patrol);
                    break;
                }
                if (!hasLineOfSight || distToPlayer > attackDistance + 2f)
                {
                    ChangeState(DroneState.Chase);
                    break;
                }
                if (stateTimer >= attackDuration)
                    ChangeState(DroneState.Recharge);
                break;

            case DroneState.Recharge:
                if (!playerDetected)
                {
                    ChangeState(DroneState.Patrol);
                    break;
                }
                if (stateTimer >= rechargeDuration)
                    ChangeState(DroneState.Chase);
                break;
        }
    }

    /// <summary>
    /// Teste si le joueur est à l'intérieur de la zone de détection rectangulaire fixe.
    /// Le drone est un "gardien" : il ne réagit que dans ce secteur.
    /// </summary>
    bool IsPlayerInDetectionZone()
    {
        if (detectionZoneCenter == null || player == null) return false;

        Vector2 zoneCenter = detectionZoneCenter.position;
        Vector2 halfSize = detectionZoneSize * 0.5f;
        Vector2 playerPos = player.position;

        bool insideX = playerPos.x >= zoneCenter.x - halfSize.x
                    && playerPos.x <= zoneCenter.x + halfSize.x;
        bool insideY = playerPos.y >= zoneCenter.y - halfSize.y
                    && playerPos.y <= zoneCenter.y + halfSize.y;

        return insideX && insideY;
    }

    void ChangeState(DroneState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        if (laser != null)
            laser.SetFiring(newState == DroneState.Attack);

        if (newState == DroneState.Patrol)
            SetAnimatorState(ANIM_IDLE);
        else
            SetAnimatorState(ANIM_CHASE);
    }

    void SetAnimatorState(int animState)
    {
        if (animator != null)
            animator.SetInteger(AnimStateHash, animState);
    }

    // ════════════════════════════════════════════════════════════
    //  Comportements
    // ════════════════════════════════════════════════════════════

    void PatrolBehavior()
    {
        if (patrolPointA == null || patrolPointB == null) return;
        if (currentPatrolTarget == null) currentPatrolTarget = patrolPointA;

        Vector2 targetPos = currentPatrolTarget.position;
        Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * patrolSpeed;

        if (Vector2.Distance(transform.position, targetPos) < pointReachedDistance)
            currentPatrolTarget = (currentPatrolTarget == patrolPointA) ? patrolPointB : patrolPointA;
    }

    void ChaseBehavior()
    {
        Vector2 chaseTarget = GetChaseTarget();
        Vector2 direction = (chaseTarget - (Vector2)transform.position).normalized;
        float dist = Vector2.Distance(transform.position, chaseTarget);
        float speedFactor = Mathf.Clamp01(dist / 2f);
        rb.linearVelocity = direction * chaseSpeed * speedFactor;
    }

    void AttackBehavior()
    {
        Vector2 chaseTarget = GetChaseTarget();
        Vector2 direction = (chaseTarget - (Vector2)transform.position).normalized;
        float dist = Vector2.Distance(transform.position, chaseTarget);
        float speedFactor = Mathf.Clamp01(dist / 1.5f);
        rb.linearVelocity = direction * (chaseSpeed * 0.4f) * speedFactor;
    }

    void RechargeBehavior()
    {
        Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)player.position).normalized;
        Vector2 retreatTarget = (Vector2)player.position + awayFromPlayer * rechargeDistance;
        retreatTarget.y += verticalOffset;

        Vector2 direction = (retreatTarget - (Vector2)transform.position).normalized;
        float dist = Vector2.Distance(transform.position, retreatTarget);
        float speedFactor = Mathf.Clamp01(dist / 1.5f);
        rb.linearVelocity = direction * rechargeSpeed * speedFactor;
    }

    Vector2 GetChaseTarget()
    {
        float side = Mathf.Sign(transform.position.x - player.position.x);
        if (side == 0) side = 1f;
        return new Vector2(
            player.position.x + side * attackDistance,
            player.position.y + verticalOffset
        );
    }

    // ════════════════════════════════════════════════════════════
    //  Flip horizontal + synchro du laser
    // ════════════════════════════════════════════════════════════

    void UpdateFlip()
    {
        if (spriteRenderer == null) return;

        bool shouldFaceRight;

        if ((currentState == DroneState.Chase || currentState == DroneState.Attack) && player != null)
        {
            shouldFaceRight = player.position.x > transform.position.x;
        }
        else
        {
            float vx = rb.linearVelocity.x;
            if (Mathf.Abs(vx) < flipThreshold) return;
            shouldFaceRight = vx > 0;
        }

        spriteRenderer.flipX = spriteDefaultFacesLeft ? shouldFaceRight : !shouldFaceRight;

        // Informe le laser de quel côté tirer (évite le "laser au cul" après un flip)
        if (laser != null)
            laser.SetActiveOrigin(shouldFaceRight);
    }

    // ════════════════════════════════════════════════════════════
    //  Ligne de vue
    // ════════════════════════════════════════════════════════════

    public bool HasLineOfSightTo(Vector2 targetPos)
    {
        Vector2 origin = transform.position;
        Vector2 direction = targetPos - origin;
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, distance, lineOfSightObstacles);
        return hit.collider == null;
    }

    // ════════════════════════════════════════════════════════════
    //  Système Grappin
    // ════════════════════════════════════════════════════════════

    public void GetHooked()
    {
        if (isHooked || isDying) return;
        isHooked = true;
        rb.linearVelocity = Vector2.zero;

        if (laser != null) laser.SetFiring(false);
        SetAnimatorState(ANIM_IDLE);
        stateTimer = 0f;
    }

    public void ReleaseHook()
    {
        isHooked = false;
        if (player != null)
        {
            // En mode gardien : on reprend la chasse seulement si le joueur
            // est dans la zone, sinon retour patrouille.
            ChangeState(IsPlayerInDetectionZone() ? DroneState.Chase : DroneState.Patrol);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Vie / dégâts
    // ════════════════════════════════════════════════════════════

    public void TakeDamage(float amount)
    {
        if (isDying) return;
        if (damageOnlyWhenHooked && !isHooked) return;

        currentHealth -= amount;
        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        if (isDying) return;
        isDying = true;

        if (laser != null) laser.SetFiring(false);
        rb.linearVelocity = Vector2.zero;

        SetAnimatorState(ANIM_DEATH);

        Invoke(nameof(EnsureDeathNotified), deathAnimationDuration - 0.05f);
        Destroy(gameObject, deathAnimationDuration);
    }

    void EnsureDeathNotified()
    {
        if (!deathNotified) NotifyDeathComplete();
    }

    // ════════════════════════════════════════════════════════════
    //  Explosion (appelée par Animation Event sur l'anim de mort)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Méthode publique appelée par un Animation Event placé sur une frame
    /// précise de l'animation de mort. Instancie le Particle System d'explosion.
    /// La signature doit être sans paramètre pour être appelable depuis un Animation Event.
    /// </summary>
    public void TriggerDeathExplosion()
    {
        if (explosionPrefab == null)
        {
            Debug.LogWarning($"[DroneEnemy {gameObject.name}] explosionPrefab non assigné — explosion ignorée");
            return;
        }

        Vector3 spawnPos = transform.position + (Vector3)explosionOffset;
        GameObject explosion = Instantiate(explosionPrefab, spawnPos, Quaternion.identity);
        Destroy(explosion, explosionLifetime);
    }

    // ════════════════════════════════════════════════════════════
    //  Notification de mort (appelée par Animation Event)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Méthode publique appelée par un Animation Event sur la DERNIÈRE frame
    /// de l'animation de mort. Notifie les écouteurs (Door en mode OnDroneKilled).
    /// La signature doit être sans paramètre pour un Animation Event.
    /// </summary>
    public void NotifyDeathComplete()
    {
        if (deathNotified) return;
        deathNotified = true;

        OnDroneDied?.Invoke(this);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Zone de détection rectangulaire fixe (gardien)
        if (detectionZoneCenter != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
            Gizmos.DrawCube(detectionZoneCenter.position, detectionZoneSize);
            Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
            Gizmos.DrawWireCube(detectionZoneCenter.position, detectionZoneSize);
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        if (patrolPointA != null && patrolPointB != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
            Gizmos.DrawWireSphere(patrolPointA.position, 0.25f);
            Gizmos.DrawWireSphere(patrolPointB.position, 0.25f);
        }
    }
}