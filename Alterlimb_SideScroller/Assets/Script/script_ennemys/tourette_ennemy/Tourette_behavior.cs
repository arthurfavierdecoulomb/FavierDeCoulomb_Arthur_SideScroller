using System.Net;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class Turret : MonoBehaviour
{
    [Header("Rotation Patrouille")]
    [SerializeField] float patrolMinAngle = -60f;   // angle bas
    [SerializeField] float patrolMaxAngle = 60f;    // angle haut
    [SerializeField] float patrolSpeed = 45f;       // degrés par seconde

    [Header("Détection")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] LayerMask detectionMask;       // coche Player + Ground

    [Header("Tir")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 1.5f;
    [SerializeField] float bulletSpeed = 12f;

    [Header("Refs")]
    [SerializeField] Transform turretHead;          // le rond qui tourne
    [SerializeField] Transform player;

    float currentAngle;
    float patrolDirection = 1f;
    float fireCooldown = 0f;
    bool playerDetected = false;

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

        // Dessine le raycast dans la Scene View
        Debug.DrawRay(firePoint.position, turretHead.right * detectionRange, playerDetected ? Color.red : Color.green);
    }

    bool CheckLineOfSight()
    {
        if (player == null) return false;

        Vector2 dirToPlayer = (player.position - firePoint.position).normalized;
        float distToPlayer = Vector2.Distance(firePoint.position, player.position);

        if (distToPlayer > detectionRange) return false;

        // Raycast vers le joueur — si ça touche le joueur en premier c'est bon
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, dirToPlayer, detectionRange, detectionMask);

        if (hit.collider != null && hit.collider.CompareTag("Player"))
            return true;

        return false;
    }

    void Patrol()
    {
        currentAngle += patrolSpeed * patrolDirection * Time.deltaTime;

        if (currentAngle >= patrolMaxAngle)
        {
            currentAngle = patrolMaxAngle;
            patrolDirection = -1f;
        }
        else if (currentAngle <= patrolMinAngle)
        {
            currentAngle = patrolMinAngle;
            patrolDirection = 1f;
        }

        turretHead.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    void AimAtPlayer()
    {
        Vector2 dir = (player.position - turretHead.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Rotation fluide vers le joueur
        float smoothAngle = Mathf.LerpAngle(turretHead.eulerAngles.z, angle, 10f * Time.deltaTime);
        turretHead.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
    }

    void FireBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

        if (bulletRb != null)
        {
            Vector2 dir = (player.position - firePoint.position).normalized;
            bulletRb.linearVelocity = dir * bulletSpeed;
        }

        Destroy(bullet, 5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}