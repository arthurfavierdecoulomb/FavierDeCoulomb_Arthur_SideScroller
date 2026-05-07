using UnityEngine;

/// <summary>
/// Drone ennemi volant avec state machine + intégration grappin/scie.
/// 
/// États :
///   - PATROL : va et vient entre pointA et pointB
///   - CHASE : a détecté le joueur, le poursuit en gardant une distance d'attaque
///   - ATTACK : tire un laser continu sur le joueur
///   - RECHARGE : s'éloigne du joueur, attend, puis revient en CHASE
/// 
/// Système de grappin :
///   - Le joueur peut accrocher le drone avec son grappin → IA désactivée, drone immobilisé
///   - La scie (SawAbility) inflige des dégâts UNIQUEMENT si le drone est accroché
///   - Game design : il faut d'abord harponner le drone, puis l'attaquer
/// 
/// Détection : 360° dans un rayon donné.
/// Tir : si ligne de vue libre (raycast), le laser s'allume automatiquement.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DroneEnemy : MonoBehaviour
{
    public enum DroneState { Patrol, Chase, Attack, Recharge }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Patrouille")]
    [SerializeField] Transform patrolPointA;
    [SerializeField] Transform patrolPointB;
    [SerializeField] float patrolSpeed = 2.5f;
    [SerializeField] float pointReachedDistance = 0.3f;

    [Header("Détection joueur")]
    [SerializeField] float detectionRadius = 8f;
    [SerializeField] string playerTag = "Player";
    [Tooltip("Layers qui bloquent la ligne de vue (murs, sol)")]
    [SerializeField] LayerMask lineOfSightObstacles;

    [Header("Chasse (poursuite du joueur)")]
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
    [SerializeField] bool damageOnlyWhenHooked = true;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D rb;
    Transform player;
    DroneLaser laser;

    DroneState currentState;
    Transform currentPatrolTarget;
    float stateTimer;
    float currentHealth;

    // ── Système Grappin ─────────────────────────────────────────
    public bool isHooked { get; private set; }

    // ── Accesseurs publics (utilisés par DroneVisuals) ──────────
    public DroneState State => currentState;
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsAlive => currentHealth > 0f;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        laser = GetComponent<DroneLaser>();
        currentHealth = maxHealth;

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;

        currentState = DroneState.Patrol;
        currentPatrolTarget = patrolPointA;
    }

    // ════════════════════════════════════════════════════════════
    //  Update : détection + state machine
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        // ── Si accroché par le grappin : IA désactivée ──
        if (isHooked)
        {
            // S'assure que le laser est éteint pendant l'accroche
            if (laser != null) laser.SetFiring(false);
            return;
        }

        if (player == null) return;

        stateTimer += Time.deltaTime;
        EvaluateStateTransitions();
    }

    void FixedUpdate()
    {
        // ── Si accroché : on bloque le mouvement ──
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
    //  Transitions d'état
    // ════════════════════════════════════════════════════════════

    void EvaluateStateTransitions()
    {
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        bool playerDetected = distToPlayer <= detectionRadius;
        bool hasLineOfSight = HasLineOfSightTo(player.position);

        switch (currentState)
        {
            case DroneState.Patrol:
                if (playerDetected)
                    ChangeState(DroneState.Chase);
                break;

            case DroneState.Chase:
                if (distToPlayer > detectionRadius * 1.3f)
                {
                    ChangeState(DroneState.Patrol);
                    break;
                }
                if (hasLineOfSight && distToPlayer <= attackDistance + 1f)
                    ChangeState(DroneState.Attack);
                break;

            case DroneState.Attack:
                if (!hasLineOfSight || distToPlayer > attackDistance + 2f)
                {
                    ChangeState(DroneState.Chase);
                    break;
                }
                if (stateTimer >= attackDuration)
                    ChangeState(DroneState.Recharge);
                break;

            case DroneState.Recharge:
                if (stateTimer >= rechargeDuration)
                    ChangeState(DroneState.Chase);
                break;
        }
    }

    void ChangeState(DroneState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        if (laser != null)
            laser.SetFiring(newState == DroneState.Attack);
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
        {
            currentPatrolTarget = (currentPatrolTarget == patrolPointA) ? patrolPointB : patrolPointA;
        }
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
    //  Système Grappin (appelé par GrappinAccrochageBehavior)
    // ════════════════════════════════════════════════════════════

    /// <summary>Le joueur a accroché le drone avec son grappin.</summary>
    public void GetHooked()
    {
        if (isHooked) return;
        isHooked = true;
        rb.linearVelocity = Vector2.zero;

        // Le laser s'éteint immédiatement
        if (laser != null) laser.SetFiring(false);

        // Reset l'état pour reprendre proprement à la libération
        stateTimer = 0f;
    }

    /// <summary>Le joueur a relâché le grappin.</summary>
    public void ReleaseHook()
    {
        isHooked = false;
        // L'IA reprendra naturellement au prochain Update()
        // On retourne en Chase si le joueur est encore proche, sinon Patrol
        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            ChangeState(dist <= detectionRadius ? DroneState.Chase : DroneState.Patrol);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Vie / dégâts (appelé par SawAbility)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Inflige des dégâts au drone.
    /// Par défaut : ne fait des dégâts QUE si le drone est accroché par le grappin
    /// (game design : il faut d'abord harponner, puis attaquer).
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (damageOnlyWhenHooked && !isHooked) return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        if (laser != null) laser.SetFiring(false);
        // Tu peux ajouter une explosion / particules ici
        Destroy(gameObject);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Rayon de détection
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Distance d'attaque
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // Patrouille
        if (patrolPointA != null && patrolPointB != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
            Gizmos.DrawWireSphere(patrolPointA.position, 0.25f);
            Gizmos.DrawWireSphere(patrolPointB.position, 0.25f);
        }
    }
}