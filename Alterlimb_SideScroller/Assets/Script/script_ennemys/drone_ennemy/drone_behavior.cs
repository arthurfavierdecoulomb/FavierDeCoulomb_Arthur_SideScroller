using UnityEngine;

public class DroneEnemy : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  États
    // ════════════════════════════════════════════════════════════

    enum DroneState { Patrolling, Chasing, Shooting }

    // ════════════════════════════════════════════════════════════
    //  Champs sérialisés
    // ════════════════════════════════════════════════════════════

    [Header("Patrouille")]
    [SerializeField] Transform pointA;
    [SerializeField] Transform pointB;
    [SerializeField] float patrolSpeed = 3f;

    [Header("Poursuite")]
    [SerializeField] float detectionRange = 8f;
    [SerializeField] float chaseSpeed = 5f;
    [SerializeField] float stopDistance = 4f;

    [Header("Vol")]
    [SerializeField] float moveSmoothness = 5f;

    [Header("Laser")]
    [SerializeField] LineRenderer laserRenderer;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float laserDamage = 10f;
    [SerializeField] float laserDuration = 0.2f;

    [Header("Moteur")]
    [SerializeField] Transform motorTransform;
    [SerializeField] float normalSpinSpeed = 180f;
    [SerializeField] float shootingSpinSpeed = 720f;

    [Header("PV")]
    [SerializeField] float maxHealth = 100f;

    [Header("Refs")]
    [SerializeField] Transform player;

    // ════════════════════════════════════════════════════════════
    //  Données runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D rb;
    DroneState state = DroneState.Patrolling;
    float currentHealth;
    float fireCooldown;
    float laserTimer;
    bool isPatrollingToB = true;
    public bool isHooked = false;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        currentHealth = maxHealth;

        if (laserRenderer != null)
            laserRenderer.enabled = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Update principal
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (isHooked)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        UpdateState();
        SpinMotor();

        switch (state)
        {
            case DroneState.Patrolling: Patrol(); break;
            case DroneState.Chasing: Chase(); break;
            case DroneState.Shooting: Shoot(); break;
        }

        fireCooldown -= Time.deltaTime;
        UpdateLaserTimer();
    }

    // ════════════════════════════════════════════════════════════
    //  Machine à états
    // ════════════════════════════════════════════════════════════

    void UpdateState()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange) state = DroneState.Patrolling;
        else if (dist > stopDistance) state = DroneState.Chasing;
        else state = DroneState.Shooting;
    }

    // ════════════════════════════════════════════════════════════
    //  Comportements
    // ════════════════════════════════════════════════════════════

    void Patrol()
    {
        if (pointA == null || pointB == null) return;

        Transform target = isPatrollingToB ? pointB : pointA;
        MoveTo(target.position, patrolSpeed);

        if (Vector2.Distance(transform.position, target.position) < 0.2f)
            isPatrollingToB = !isPatrollingToB;
    }

    void Chase()
    {
        MoveTo(player.position, chaseSpeed);
    }

    void Shoot()
    {
        // Reste à distance, décélère
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, moveSmoothness * Time.deltaTime);

        // Oriente le firepoint vers le joueur
        if (firePoint != null && player != null)
        {
            Vector2 dir = (player.position - firePoint.position).normalized;
            firePoint.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        if (fireCooldown <= 0f)
        {
            FireLaser();
            fireCooldown = fireRate;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Laser
    // ════════════════════════════════════════════════════════════

    void FireLaser()
    {
        if (firePoint == null || player == null) return;

        // Dégâts au joueur
        PlayerHealth ph = player.GetComponent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(laserDamage);

        // Visuel laser
        if (laserRenderer != null)
        {
            laserRenderer.enabled = true;
            laserRenderer.SetPosition(0, firePoint.position);
            laserRenderer.SetPosition(1, player.position);
            laserTimer = laserDuration;
        }
    }

    void UpdateLaserTimer()
    {
        if (laserTimer <= 0f) return;

        laserTimer -= Time.deltaTime;
        if (laserTimer <= 0f && laserRenderer != null)
            laserRenderer.enabled = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Moteur
    // ════════════════════════════════════════════════════════════

    void SpinMotor()
    {
        if (motorTransform == null) return;

        float speed = (state == DroneState.Shooting) ? shootingSpinSpeed : normalSpinSpeed;
        motorTransform.Rotate(0f, 0f, speed * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════
    //  Déplacement
    // ════════════════════════════════════════════════════════════

    void MoveTo(Vector2 target, float speed)
    {
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * speed, moveSmoothness * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════
    //  Hook (Grappin)
    // ════════════════════════════════════════════════════════════

    public void GetHooked()
    {
        if (isHooked) return;
        isHooked = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
    }

    public void ReleaseHook()
    {
        isHooked = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Dégâts (appelé par SawAbility)
    // ════════════════════════════════════════════════════════════

    public void TakeDamage(float amount)
    {
        if (!isHooked) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        Destroy(gameObject);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}