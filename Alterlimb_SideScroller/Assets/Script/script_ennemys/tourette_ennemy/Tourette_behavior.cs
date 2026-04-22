using UnityEngine;

public class Turret : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  États
    // ════════════════════════════════════════════════════════════

    enum TurretState { Patrolling, Shooting }

    // ════════════════════════════════════════════════════════════
    //  Champs sérialisés
    // ════════════════════════════════════════════════════════════

    [Header("Rotation Patrouille (erratique)")]
    [SerializeField] float patrolMinAngle = -60f;
    [SerializeField] float patrolMaxAngle = 60f;
    [SerializeField] float patrolSnapSpeed = 600f;
    [SerializeField] float pauseMinTime = 0.05f;
    [SerializeField] float pauseMaxTime = 0.3f;

    [Header("Détection")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] LayerMask detectionMask;

    [Header("Tir")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float bulletSpeed = 12f;
    [SerializeField] float aimSpeed = 10f;

    [Header("Moteur")]
    [SerializeField] Transform motorTransform;
    [SerializeField] float normalSpinSpeed = 180f;
    [SerializeField] float shootingSpinSpeed = 720f;

    [Header("Refs")]
    [SerializeField] Transform turretHead;
    [SerializeField] Transform player;

    [Header("Orientation du sprite")]
    [SerializeField] float spriteAngleOffset = 0f;

    // ════════════════════════════════════════════════════════════
    //  Données runtime
    // ════════════════════════════════════════════════════════════

    TurretState state = TurretState.Patrolling;
    TurretState previousState = TurretState.Patrolling;
    float currentAngle;
    float targetAngle;
    float pauseTimer;
    float fireCooldown;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        PickNewTargetAngle();
    }

    // ════════════════════════════════════════════════════════════
    //  Update principal
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        UpdateState();
        SpinMotor();

        switch (state)
        {
            case TurretState.Patrolling: Patrol(); break;
            case TurretState.Shooting: Shoot(); break;
        }

        // Applique la rotation — un seul endroit, pas de conflit
        if (turretHead != null)
            turretHead.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        fireCooldown -= Time.deltaTime;
    }

    // ════════════════════════════════════════════════════════════
    //  Machine à états
    // ════════════════════════════════════════════════════════════

    void UpdateState()
    {
        previousState = state;
        state = CheckLineOfSight() ? TurretState.Shooting : TurretState.Patrolling;

        // Transition Shooting → Patrolling : reprend la patrouille depuis l'angle actuel
        if (state == TurretState.Patrolling && previousState == TurretState.Shooting)
            PickNewTargetAngle();
    }

    // ════════════════════════════════════════════════════════════
    //  Détection
    // ════════════════════════════════════════════════════════════

    bool CheckLineOfSight()
    {
        if (player == null || firePoint == null) return false;

        float dist = Vector2.Distance(firePoint.position, player.position);
        if (dist > detectionRange) return false;

        Vector2 dir = (player.position - firePoint.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, dir, detectionRange, detectionMask);

        return hit.collider != null && hit.collider.CompareTag("Player");
    }

    // ════════════════════════════════════════════════════════════
    //  Patrouille erratique
    // ════════════════════════════════════════════════════════════

    void Patrol()
    {
        if (turretHead == null) return;

        // En pause : attend avant de bouger vers un nouvel angle
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        // Snap rapide vers l'angle cible
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, patrolSnapSpeed * Time.deltaTime);

        // Arrivé à l'angle cible : pause courte puis nouvel angle
        if (Mathf.Abs(currentAngle - targetAngle) < 0.5f)
        {
            pauseTimer = Random.Range(pauseMinTime, pauseMaxTime);
            PickNewTargetAngle();
        }
    }

    void PickNewTargetAngle()
    {
        // Choisit un angle aléatoire suffisamment différent de l'actuel
        float newAngle;
        do
        {
            newAngle = Random.Range(patrolMinAngle, patrolMaxAngle);
        }
        while (Mathf.Abs(newAngle - currentAngle) < 20f);

        targetAngle = newAngle;
    }

    // ════════════════════════════════════════════════════════════
    //  Tir
    // ════════════════════════════════════════════════════════════

    void Shoot()
    {
        AimAtPlayer();

        if (fireCooldown <= 0f)
        {
            FireBullet();
            fireCooldown = fireRate;
        }
    }

    void AimAtPlayer()
    {
        if (turretHead == null || player == null) return;

        Vector2 dir = (player.position - turretHead.position).normalized;
        float worldAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float parentAngle = turretHead.parent != null ? turretHead.parent.eulerAngles.z : 0f;
        float localAngle = worldAngle - parentAngle - spriteAngleOffset;

        currentAngle = Mathf.LerpAngle(currentAngle, localAngle, aimSpeed * Time.deltaTime);
    }

    void FireBullet()
    {
        if (bulletPrefab == null || firePoint == null || player == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb == null) return;

        Vector2 dir = (player.position - firePoint.position).normalized;
        bulletRb.linearVelocity = dir * bulletSpeed;

        Destroy(bullet, 5f);
    }

    // ════════════════════════════════════════════════════════════
    //  Moteur
    // ════════════════════════════════════════════════════════════

    void SpinMotor()
    {
        if (motorTransform == null) return;

        float speed = (state == TurretState.Shooting) ? shootingSpinSpeed : normalSpinSpeed;
        motorTransform.Rotate(0f, 0f, speed * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}