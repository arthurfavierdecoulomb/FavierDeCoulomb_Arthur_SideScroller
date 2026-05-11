using UnityEngine;

public class Turret : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Champs sérialisés
    // ════════════════════════════════════════════════════════════

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

    bool playerInSight;
    float currentAngle;
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
    }

    // ════════════════════════════════════════════════════════════
    //  Update principal
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        playerInSight = CheckLineOfSight();

        SpinMotor();

        if (playerInSight)
        {
            AimAtPlayer();

            if (fireCooldown <= 0f)
            {
                FireBullet();
                fireCooldown = fireRate;
            }
        }

        // Applique la rotation
        if (turretHead != null)
            turretHead.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        fireCooldown -= Time.deltaTime;
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
    //  Visée
    // ════════════════════════════════════════════════════════════

    void AimAtPlayer()
    {
        if (turretHead == null || player == null) return;

        Vector2 dir = (player.position - turretHead.position).normalized;
        float worldAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float parentAngle = turretHead.parent != null ? turretHead.parent.eulerAngles.z : 0f;
        float localAngle = worldAngle - parentAngle - spriteAngleOffset;

        currentAngle = Mathf.LerpAngle(currentAngle, localAngle, aimSpeed * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════
    //  Tir
    // ════════════════════════════════════════════════════════════

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

        float speed = playerInSight ? shootingSpinSpeed : normalSpinSpeed;
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