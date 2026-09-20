using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Détection")]
    [SerializeField] Vector2 detectionSize = new Vector2(12f, 4f);
    [SerializeField] Vector2 detectionOffset = Vector2.zero;
    [SerializeField] LayerMask detectionMask;

    [Header("Tir")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float bulletSpeed = 12f;
    [SerializeField] float aimSpeed = 10f;

    [Header("Préparation du tir")]
    [SerializeField] float readyLeadTime = 0.35f;
    [SerializeField] bool telegraphFirstShot = true;

    [Header("Moteur")]
    [SerializeField] Transform motorTransform;
    [SerializeField] float normalSpinSpeed = 180f;
    [SerializeField] float shootingSpinSpeed = 720f;
    [SerializeField] float spinRatioSmoothing = 3f;

    [Header("Refs")]
    [SerializeField] Transform turretHead;
    [SerializeField] Transform player;

    [Header("Orientation du sprite")]
    [SerializeField] float spriteAngleOffset = 0f;

    bool playerInSight;
    float currentAngle;
    float previousAngle;
    float fireCooldown;
    float spinRatio;
    float headTurnRate;
    int shotCount;
    bool hasFiredSinceSight;

    public bool PlayerInSight => playerInSight;
    public float SpinRatio => spinRatio;
    public float HeadTurnRate => headTurnRate;
    public int ShotCount => shotCount;

    public bool IsReadyToFire => playerInSight
                              && !hasFiredSinceSight
                              && fireCooldown > 0f
                              && fireCooldown <= readyLeadTime;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        currentAngle = turretHead != null ? turretHead.localEulerAngles.z : 0f;
        previousAngle = currentAngle;
    }

    void Update()
    {
        bool wasInSight = playerInSight;
        playerInSight = CheckLineOfSight();

        if (playerInSight && !wasInSight)
        {
            hasFiredSinceSight = false;

            if (telegraphFirstShot && fireCooldown < readyLeadTime)
                fireCooldown = readyLeadTime;
        }

        if (!playerInSight)
            hasFiredSinceSight = false;

        SpinMotor();
        UpdateSpinRatio();

        if (playerInSight)
        {
            AimAtPlayer();

            if (fireCooldown <= 0f)
            {
                FireBullet();
                fireCooldown = fireRate;
            }
        }

        if (turretHead != null)
            turretHead.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        UpdateHeadTurnRate();

        fireCooldown -= Time.deltaTime;

        if (fireCooldown < 0f)
            fireCooldown = 0f;
    }

    void UpdateSpinRatio()
    {
        float target = playerInSight ? 1f : 0f;
        spinRatio = Mathf.MoveTowards(spinRatio, target, spinRatioSmoothing * Time.deltaTime);
    }

    void UpdateHeadTurnRate()
    {
        if (Time.deltaTime <= 0f) return;

        headTurnRate = Mathf.Abs(Mathf.DeltaAngle(previousAngle, currentAngle)) / Time.deltaTime;
        previousAngle = currentAngle;
    }

    bool CheckLineOfSight()
    {
        if (player == null) return false;

        Vector2 localPlayerPos = transform.InverseTransformPoint(player.position);
        Vector2 localOffsetPos = localPlayerPos - detectionOffset;

        Vector2 half = detectionSize * 0.5f;
        if (Mathf.Abs(localOffsetPos.x) > half.x || Mathf.Abs(localOffsetPos.y) > half.y)
            return false;

        if (firePoint == null) return true;

        Vector2 dir = (player.position - firePoint.position).normalized;
        float dist = Vector2.Distance(firePoint.position, player.position);
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, dir, dist, detectionMask);

        return hit.collider != null && hit.collider.CompareTag("Player");
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

        shotCount++;
        hasFiredSinceSight = true;

        Destroy(bullet, 5f);
    }

    void SpinMotor()
    {
        if (motorTransform == null) return;

        float speed = Mathf.Lerp(normalSpinSpeed, shootingSpinSpeed, spinRatio);
        motorTransform.Rotate(0f, 0f, speed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(detectionOffset, detectionSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}