using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DroneEnemy : MonoBehaviour
{
    public enum DroneState { Patrol, Chase, Attack, Recharge }

    public static event Action<DroneEnemy> OnDroneDied;

    const int ANIM_IDLE = 0;
    const int ANIM_CHASE = 1;
    const int ANIM_DEATH = 2;

    [Header("Patrouille")]
    [SerializeField] Transform patrolPointA;
    [SerializeField] Transform patrolPointB;
    [SerializeField] float patrolSpeed = 2.5f;
    [SerializeField] float pointReachedDistance = 0.3f;

    [Header("Détection joueur — Zone gardien")]
    [SerializeField] Transform detectionZoneCenter;
    [SerializeField] Vector2 detectionZoneSize = new Vector2(10f, 6f);
    [SerializeField] string playerTag = "Player";
    [SerializeField] LayerMask lineOfSightObstacles;

    [Header("Chasse")]
    [SerializeField] float chaseSpeed = 4f;
    [SerializeField] float attackDistance = 5f;
    [SerializeField] float verticalOffset = 2.5f;

    [Header("Visée")]
    [SerializeField] float aimDuration = 0.7f;
    [SerializeField] float lockLeadTime = 0.2f;

    [Header("Attaque (laser continu)")]
    [SerializeField] float attackDuration = 2.5f;

    [Header("Fin d'énergie")]
    [SerializeField] float energyBlinkDuration = 0.5f;
    [SerializeField] float energyBlinkInterval = 0.07f;
    [Range(0f, 1f)]
    [SerializeField] float energyBlinkLowIntensity = 0.18f;
    [SerializeField] bool damageDuringBlink = false;

    [Header("Recharge")]
    [SerializeField] float rechargeDuration = 2f;
    [SerializeField] float rechargeDistance = 9f;
    [SerializeField] float rechargeSpeed = 5f;

    [Header("Vie")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] bool damageOnlyWhenHooked = false;
    [SerializeField] float deathAnimationDuration = 1.5f;

    [Header("Explosion à la mort")]
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] Vector2 explosionOffset = Vector2.zero;
    [SerializeField] float explosionLifetime = 3f;

    [Header("Flip horizontal")]
    [SerializeField] float flipThreshold = 0.5f;
    [SerializeField] bool spriteDefaultFacesLeft = true;

    [Header("Références")]
    [SerializeField] Animator animator;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Transform player;
    DroneLaser laser;
    DamageFeedback damageFeedback;

    DroneState currentState;
    Transform currentPatrolTarget;
    float stateTimer;
    float currentHealth;
    bool isDying;
    bool deathNotified;
    bool lastFiringSent;
    bool beamBlinkVisible;
    float lastIntensitySent = -1f;
    bool lastDamageSent = true;

    public bool isHooked { get; private set; }

    public DroneState State => currentState;
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsAlive => currentHealth > 0f && !isDying;

    float AimEndTime => aimDuration;
    float FireEndTime => aimDuration + attackDuration;
    float BlinkEndTime => aimDuration + attackDuration + energyBlinkDuration;

    public bool IsAiming => currentState == DroneState.Attack && !isHooked && !isDying
                         && stateTimer < AimEndTime;

    public bool IsLocked => currentState == DroneState.Attack && !isHooked && !isDying
                         && stateTimer >= AimEndTime - lockLeadTime && stateTimer < AimEndTime;

    public bool IsFiring => currentState == DroneState.Attack && !isHooked && !isDying
                         && stateTimer >= AimEndTime && stateTimer < FireEndTime;

    public bool IsBeamBlinking => currentState == DroneState.Attack && !isHooked && !isDying
                               && stateTimer >= FireEndTime && stateTimer < BlinkEndTime;

    public bool IsBeamVisible => IsFiring || (IsBeamBlinking && beamBlinkVisible);

    public float MotorLoad
    {
        get
        {
            if (rb == null || isDying) return 0f;
            if (isHooked) return 0.15f;

            float reference = Mathf.Max(0.01f, Mathf.Max(chaseSpeed, rechargeSpeed));
            return Mathf.Clamp01(rb.linearVelocity.magnitude / reference);
        }
    }

    static readonly int AnimStateHash = Animator.StringToHash("State");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        laser = GetComponent<DroneLaser>();
        damageFeedback = GetComponent<DamageFeedback>();
        currentHealth = maxHealth;

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;

        currentState = DroneState.Patrol;
        currentPatrolTarget = patrolPointA;

        SetAnimatorState(ANIM_IDLE);
    }

    void Update()
    {
        if (isDying) return;

        if (isHooked)
        {
            SendFiring(false);
            return;
        }

        if (player == null) return;

        stateTimer += Time.deltaTime;
        EvaluateStateTransitions();
        UpdateBeamOutput();
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
                if (stateTimer >= BlinkEndTime)
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

    void UpdateBeamOutput()
    {
        if (currentState != DroneState.Attack || stateTimer < AimEndTime)
        {
            SendFiring(false);
            return;
        }

        if (stateTimer < FireEndTime)
        {
            beamBlinkVisible = true;
            SendFiring(true);
            SendIntensity(1f);
            SendDamageEnabled(true);
            return;
        }

        float interval = Mathf.Max(0.01f, energyBlinkInterval);
        int step = Mathf.FloorToInt((stateTimer - FireEndTime) / interval);
        beamBlinkVisible = (step % 2) == 0;

        SendFiring(true);
        SendIntensity(beamBlinkVisible ? 1f : energyBlinkLowIntensity);
        SendDamageEnabled(damageDuringBlink);
    }

    void SendIntensity(float intensity)
    {
        if (Mathf.Approximately(intensity, lastIntensitySent)) return;

        lastIntensitySent = intensity;

        if (laser != null) laser.SetIntensity(intensity);
    }

    void SendDamageEnabled(bool enabled)
    {
        if (enabled == lastDamageSent) return;

        lastDamageSent = enabled;

        if (laser != null) laser.SetDamageEnabled(enabled);
    }

    void SendFiring(bool firing)
    {
        if (firing == lastFiringSent) return;

        lastFiringSent = firing;

        if (laser == null) return;

        laser.SetFiring(firing);

        if (firing)
        {
            lastIntensitySent = 1f;
            laser.SetIntensityInstant(1f);
        }
    }

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

        if (newState != DroneState.Attack)
            SendFiring(false);

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

        if (laser != null)
            laser.SetActiveOrigin(shouldFaceRight);
    }

    public bool HasLineOfSightTo(Vector2 targetPos)
    {
        Vector2 origin = transform.position;
        Vector2 direction = targetPos - origin;
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, distance, lineOfSightObstacles);
        return hit.collider == null;
    }

    public void GetHooked()
    {
        if (isHooked || isDying) return;
        isHooked = true;
        rb.linearVelocity = Vector2.zero;

        SendFiring(false);
        SetAnimatorState(ANIM_IDLE);
        stateTimer = 0f;
    }

    public void ReleaseHook()
    {
        isHooked = false;
        if (player != null)
        {
            ChangeState(IsPlayerInDetectionZone() ? DroneState.Chase : DroneState.Patrol);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDying) return;
        if (damageOnlyWhenHooked && !isHooked) return;

        currentHealth -= amount;

        if (damageFeedback != null)
            damageFeedback.ShowDamage(amount);

        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        if (isDying) return;
        isDying = true;

        SendFiring(false);
        rb.linearVelocity = Vector2.zero;

        SetAnimatorState(ANIM_DEATH);

        Invoke(nameof(EnsureDeathNotified), deathAnimationDuration - 0.05f);
        Destroy(gameObject, deathAnimationDuration);
    }

    void EnsureDeathNotified()
    {
        if (!deathNotified) NotifyDeathComplete();
    }

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

    public void NotifyDeathComplete()
    {
        if (deathNotified) return;
        deathNotified = true;

        OnDroneDied?.Invoke(this);
    }

    void OnDrawGizmos()
    {
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