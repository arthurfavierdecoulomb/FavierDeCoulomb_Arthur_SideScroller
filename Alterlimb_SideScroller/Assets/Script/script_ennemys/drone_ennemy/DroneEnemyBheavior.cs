using UnityEngine;
using System;

/// <summary>
/// Drone ennemi volant — version "Animator" (utilise un spritesheet animé).
/// 
/// État interne (state machine logique) :
///   - Patrol : patrouille A↔B
///   - Chase : poursuit le joueur sans tirer
///   - Attack : tire le laser (le drone reste relativement immobile)
///   - Recharge : s'éloigne pour "recharger" puis revient
/// 
/// État envoyé à l'Animator (via paramètre Int "State") :
///   - 0 = Idle  → quand Patrol ou stationnaire
///   - 1 = Chase → quand Chase, Attack ou Recharge (hélice rapide)
///   - 2 = Death → à la mort
/// 
/// Système Grappin :
///   - Le joueur peut accrocher le drone → IA désactivée, drone immobilisé
///   - La scie inflige des dégâts (configurable : avec ou sans grappin requis)
/// 
/// Explosion à la mort :
///   - Déclenchée via Animation Event sur une frame précise de l'animation de mort
///   - L'event appelle la méthode publique TriggerDeathExplosion()
/// 
/// Notification de mort (pour ouverture de porte, etc.) :
///   - Event statique OnDroneDied déclenché à la fin de l'animation de mort
///   - Appelé via Animation Event NotifyDeathComplete() sur la dernière frame
///   - Les Door en mode OnDroneKilled s'y abonnent
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DroneEnemy : MonoBehaviour
{
    public enum DroneState { Patrol, Chase, Attack, Recharge }

    // ════════════════════════════════════════════════════════════
    //  Event statique : notifie tous les écouteurs qu'un drone est mort
    //  (utilisé par Door en mode OnDroneKilled)
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

    [Header("Détection joueur")]
    [SerializeField] float detectionRadius = 8f;
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
    bool deathNotified;  // évite de notifier deux fois si l'Animation Event est mal placé

    // ── Système Grappin ─────────────────────────────────────────
    public bool isHooked { get; private set; }

    // ── Accesseurs publics ──────────────────────────────────────
    public DroneState State => currentState;
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsAlive => currentHealth > 0f && !isDying;

    // ── Hash du paramètre Animator (perf : évite de chercher par nom à chaque frame) ──
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
        // En cours de mort : on attend juste la fin de l'animation
        if (isDying) return;

        // Si accroché : IA désactivée
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
                if (playerDetected) ChangeState(DroneState.Chase);
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

        // Active le laser uniquement en mode Attack
        if (laser != null)
            laser.SetFiring(newState == DroneState.Attack);

        // Met à jour l'animation : Patrol = Idle, tout le reste = Chase
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
    //  Flip horizontal (suit la direction de mouvement OU vise le joueur)
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
            float dist = Vector2.Distance(transform.position, player.position);
            ChangeState(dist <= detectionRadius ? DroneState.Chase : DroneState.Patrol);
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

        // Stoppe le laser
        if (laser != null) laser.SetFiring(false);

        // Stoppe le mouvement
        rb.linearVelocity = Vector2.zero;

        // Lance l'animation de mort
        SetAnimatorState(ANIM_DEATH);

        // Filet de sécurité : si l'Animation Event NotifyDeathComplete()
        // n'est pas placé (oubli sur la dernière frame), on notifie quand même
        // juste avant que le GameObject soit détruit.
        Invoke(nameof(EnsureDeathNotified), deathAnimationDuration - 0.05f);

        // Détruit après la fin de l'animation
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
    /// précise de l'animation de mort. Instancie le Particle System d'explosion
    /// à la position du drone et le détruit après explosionLifetime secondes.
    /// 
    /// IMPORTANT : la signature doit être sans paramètre (ou avec un seul
    /// paramètre simple) pour être appelable depuis un Animation Event.
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

        // Détruit l'explosion après son cycle de vie pour ne pas polluer la scène
        Destroy(explosion, explosionLifetime);
    }

    // ════════════════════════════════════════════════════════════
    //  Notification de mort (appelée par Animation Event)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Méthode publique appelée par un Animation Event placé sur la DERNIÈRE
    /// frame de l'animation de mort. Notifie tous les écouteurs (notamment
    /// les Door en mode OnDroneKilled) que ce drone a fini de mourir.
    /// 
    /// IMPORTANT : la signature doit être sans paramètre pour être
    /// appelable depuis un Animation Event.
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
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

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