using UnityEngine;

/// <summary>
/// Laser continu du drone.
/// 
/// Comportement :
///   - Quand SetFiring(true) est appelé → le LineRenderer s'active
///   - À chaque frame, raycast depuis l'origine vers le joueur
///   - Si le rayon touche un mur : le laser s'arrête au mur
///   - Si le rayon touche le joueur : il prend des dégâts par seconde
/// 
/// Le laser est désactivé visuellement quand SetFiring(false) est appelé.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class DroneLaser : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Référence")]
    [Tooltip("Point d'origine du laser (Transform vide enfant du drone)")]
    [SerializeField] Transform laserOrigin;

    [Header("Cible")]
    [SerializeField] string playerTag = "Player";

    [Header("Comportement")]
    [Tooltip("Distance maximale du laser (au-delà, il ne touche plus rien)")]
    [SerializeField] float maxRange = 15f;
    [Tooltip("Layers qui bloquent le laser (murs, sol)")]
    [SerializeField] LayerMask obstacleLayers;

    [Header("Dégâts")]
    [Tooltip("Dégâts infligés par seconde au joueur")]
    [SerializeField] float damagePerSecond = 25f;
    [Tooltip("Intervalle minimal entre deux applications de dégâts (anti-spam)")]
    [SerializeField] float damageInterval = 0.1f;

    [Header("Visuel")]
    [Tooltip("Effet de jitter (tremblement) pour rendre le laser vivant (0 = stable, 0.1 = tremble)")]
    [SerializeField] float jitterAmount = 0.05f;
    [Tooltip("Vitesse du jitter")]
    [SerializeField] float jitterSpeed = 30f;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    LineRenderer lineRenderer;
    Transform player;
    PlayerHealth playerHealth;
    bool isFiring;
    float damageTimer;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
        lineRenderer.positionCount = 2;

        // Cherche le joueur
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Active ou désactive le tir du laser.
    /// Appelé par DroneEnemy lors des changements d'état.
    /// </summary>
    public void SetFiring(bool firing)
    {
        isFiring = firing;
        if (lineRenderer != null)
            lineRenderer.enabled = firing;
    }

    // ════════════════════════════════════════════════════════════
    //  Update : vise + raycast + dégâts
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (!isFiring || laserOrigin == null || player == null) return;

        // Direction depuis l'origine vers le joueur
        Vector2 origin = laserOrigin.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        float distance = Mathf.Min(toPlayer.magnitude, maxRange);
        Vector2 direction = toPlayer.normalized;

        // Raycast pour voir si on touche un mur en chemin
        Vector2 endPoint;
        bool playerHit = false;

        // On raycast en cherchant les murs ET le joueur (tout sauf nos propres colliders)
        RaycastHit2D obstacleHit = Physics2D.Raycast(origin, direction, distance, obstacleLayers);

        if (obstacleHit.collider != null)
        {
            // Mur en chemin → le laser s'arrête au mur, pas de dégâts
            endPoint = obstacleHit.point;
            playerHit = false;
        }
        else
        {
            // Pas d'obstacle → le rayon va jusqu'au joueur
            endPoint = origin + direction * distance;
            playerHit = true;
        }

        // Applique le jitter visuel sur le point d'arrivée
        if (jitterAmount > 0f)
        {
            float jitter = Mathf.Sin(Time.time * jitterSpeed) * jitterAmount;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            endPoint += perpendicular * jitter;
        }

        // Met à jour le LineRenderer
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);

        // Inflige les dégâts si le joueur est touché
        if (playerHit && playerHealth != null)
        {
            damageTimer += Time.deltaTime;
            if (damageTimer >= damageInterval)
            {
                int damage = Mathf.RoundToInt(damagePerSecond * damageInterval);
                playerHealth.TakeDamage(damage);
                damageTimer = 0f;
            }
        }
        else
        {
            damageTimer = 0f; // reset si on tire dans le mur
        }
    }
}