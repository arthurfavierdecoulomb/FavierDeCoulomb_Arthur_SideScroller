using UnityEngine;

public class DroneEnemy : MonoBehaviour
{
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

    [Header("Tir")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float bulletSpeed = 10f;

    [Header("Refs")]
    [SerializeField] Transform player;

    Rigidbody2D rb;
    public bool isHooked = false;
    bool isPatrollingToB = true;
    float fireCooldown = 0f;

    enum DroneState { Patrolling, Chasing, Shooting }
    DroneState state = DroneState.Patrolling;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (isHooked) return; // IA désactivée dès qu'accroché

        UpdateState();

        switch (state)
        {
            case DroneState.Patrolling: Patrol(); break;
            case DroneState.Chasing: Chase(); break;
            case DroneState.Shooting: Shoot(); break;
        }

        fireCooldown -= Time.deltaTime;
    }

    void UpdateState()
    {
        if (player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange) state = DroneState.Patrolling;
        else if (dist > stopDistance) state = DroneState.Chasing;
        else state = DroneState.Shooting;
    }

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
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, moveSmoothness * Time.deltaTime);

        if (firePoint != null && player != null)
        {
            Vector2 dir = (player.position - firePoint.position).normalized;
            firePoint.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        if (fireCooldown <= 0f)
        {
            FireBullet();
            fireCooldown = fireRate;
        }
    }

    void FireBullet()
    {
        if (bulletPrefab == null || firePoint == null || player == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            Vector2 dir = (player.position - firePoint.position).normalized;
            bulletRb.linearVelocity = dir * bulletSpeed;
        }
        Destroy(bullet, 5f);
    }

    void MoveTo(Vector2 target, float speed)
    {
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * speed, moveSmoothness * Time.deltaTime);
    }

    // ─── HOOK ────────────────────────────────────────────────────

    public void GetHooked()
    {
        if (isHooked) return;
        isHooked = true;

        rb.gravityScale = 1f;           // gravité active → il tombe
        rb.linearVelocity = Vector2.zero; // reset la vélocité de vol
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!isHooked) return;

        if (col.gameObject.CompareTag("Ground"))
            Destroy(gameObject); // le sol le détruit
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}