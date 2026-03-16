using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Rotation Patrouille")]
    [SerializeField] float patrolMinAngle = -60f;
    [SerializeField] float patrolMaxAngle = 60f;
    [SerializeField] float patrolSpeed = 45f;

    [Header("Détection")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] LayerMask detectionMask;

    [Header("Tir")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float bulletSpeed = 12f;

    [Header("Refs")]
    [SerializeField] Transform turretHead;
    [SerializeField] Transform player;          // assignable manuellement OU auto-trouvé

    float currentAngle;
    float patrolDirection = 1f;
    float fireCooldown = 0f;
    bool playerDetected = false;

    void Awake()
    {
        // Auto-find si non assigné dans l'inspecteur
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
            else
                Debug.LogError("[Turret] Aucun GameObject avec le tag 'Player' trouvé !");
        }

        if (turretHead == null)
            Debug.LogError("[Turret] turretHead non assigné !");

        if (firePoint == null)
            Debug.LogError("[Turret] firePoint non assigné !");

        if (bulletPrefab == null)
            Debug.LogError("[Turret] bulletPrefab non assigné !");

        // Avertit si le detectionMask est vide (piège classique)
        if (detectionMask == 0)
            Debug.LogWarning("[Turret] detectionMask est vide ! Coche Player + Ground dans l'inspecteur.");
    }

    void Update()
    {
        playerDetected = CheckLineOfSight();

        if (playerDetected)
            AimAtPlayer();
        else
            Patrol();

        fireCooldown -= Time.deltaTime;
        if (playerDetected && fireCooldown <= 0f)
        {
            FireBullet();
            fireCooldown = fireRate;
        }

        Debug.DrawRay(firePoint.position, turretHead.right * detectionRange,
                      playerDetected ? Color.red : Color.green);
    }

    bool CheckLineOfSight()
    {
        if (player == null) return false;

        float distToPlayer = Vector2.Distance(firePoint.position, player.position);
        Debug.Log($"[Turret] Distance joueur : {distToPlayer:F1} / Range : {detectionRange}");

        if (distToPlayer > detectionRange) return false;

        Vector2 dirToPlayer = (player.position - firePoint.position).normalized;

        RaycastHit2D hit = Physics2D.Raycast(
            firePoint.position,
            dirToPlayer,
            detectionRange,
            detectionMask
        );

        if (hit.collider == null)
        {
            Debug.LogWarning($"[Turret] Raycast ne touche RIEN — detectionMask probablement mal configuré");
            return false;
        }

        Debug.Log($"[Turret] Raycast touche : {hit.collider.name} | Tag : {hit.collider.tag}");

        return hit.collider.CompareTag("Player");
    }

    void Patrol()
    {
        currentAngle += patrolSpeed * patrolDirection * Time.deltaTime;

        if (currentAngle >= patrolMaxAngle) { currentAngle = patrolMaxAngle; patrolDirection = -1f; }
        else if (currentAngle <= patrolMinAngle) { currentAngle = patrolMinAngle; patrolDirection = 1f; }

        turretHead.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    void AimAtPlayer()
    {
        Vector2 dir = (player.position - turretHead.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float smoothAngle = Mathf.LerpAngle(turretHead.eulerAngles.z, angle, 10f * Time.deltaTime);
        turretHead.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
    }

    void FireBullet()
    {
        // ── Logs de diagnostic ──────────────────────────
        Debug.Log($"[Turret] FireBullet() appelé !");

        if (bulletPrefab == null) { Debug.LogError("[Turret] bulletPrefab EST NULL !"); return; }
        if (firePoint == null) { Debug.LogError("[Turret] firePoint EST NULL !"); return; }
        if (player == null) { Debug.LogError("[Turret] player EST NULL !"); return; }
        // ────────────────────────────────────────────────

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

        if (bulletRb == null)
        {
            Debug.LogError("[Turret] Le prefab balle n'a pas de Rigidbody2D !");
            return;
        }

        Vector2 dir = (player.position - firePoint.position).normalized;
        bulletRb.linearVelocity = dir * bulletSpeed;

        Debug.Log($"[Turret] Balle tirée vers {dir}, vitesse {bulletSpeed}");
        Destroy(bullet, 5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}