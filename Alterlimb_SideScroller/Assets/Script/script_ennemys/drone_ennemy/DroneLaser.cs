using UnityEngine;

/// <summary>
/// Laser continu du drone.
/// 
/// Comportement :
///   - Quand SetFiring(true) → le LineRenderer s'active
///   - Le laser part de LaserOrigin et vise directement le joueur
///   - Si un mur bloque la vue, le laser s'arrête au mur (pas de dégâts)
///   - Si le joueur est touché, il prend des dégâts par seconde
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
    [SerializeField] float maxRange = 15f;
    [Tooltip("Layers qui bloquent le laser (murs, sol). NE PAS inclure le layer du joueur !")]
    [SerializeField] LayerMask obstacleLayers;

    [Header("Dégâts")]
    [Tooltip("Dégâts infligés par seconde au joueur")]
    [SerializeField] float damagePerSecond = 25f;
    [Tooltip("Intervalle entre deux applications de dégâts")]
    [SerializeField] float damageInterval = 0.1f;

    [Header("Visuel")]
    [SerializeField] float jitterAmount = 0.05f;
    [SerializeField] float jitterSpeed = 30f;

    [Header("Debug")]
    [Tooltip("Affiche des messages dans la console pour diagnostiquer")]
    [SerializeField] bool debugMode = false;

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

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();

            if (debugMode)
            {
                if (playerHealth == null)
                    Debug.LogError("[DroneLaser] Joueur trouvé mais SANS composant PlayerHealth !");
                else
                    Debug.Log("[DroneLaser] Joueur et PlayerHealth correctement détectés.");
            }
        }
        else if (debugMode)
        {
            Debug.LogError($"[DroneLaser] Aucun GameObject avec le tag '{playerTag}' trouvé !");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    public void SetFiring(bool firing)
    {
        isFiring = firing;
        if (lineRenderer != null) lineRenderer.enabled = firing;
        if (!firing) damageTimer = 0f;
    }

    // ════════════════════════════════════════════════════════════
    //  Update
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (!isFiring || laserOrigin == null || player == null) return;

        Vector2 origin = laserOrigin.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        float distance = Mathf.Min(toPlayer.magnitude, maxRange);
        Vector2 direction = toPlayer.normalized;

        // Raycast pour voir si un mur bloque le passage
        RaycastHit2D obstacleHit = Physics2D.Raycast(origin, direction, distance, obstacleLayers);

        Vector2 endPoint;
        bool playerHit;

        if (obstacleHit.collider != null)
        {
            // Mur bloque → laser s'arrête au mur
            endPoint = obstacleHit.point;
            playerHit = false;

            if (debugMode)
                Debug.Log($"[DroneLaser] Mur en chemin : {obstacleHit.collider.name}");
        }
        else
        {
            // Pas d'obstacle → laser atteint le joueur
            endPoint = origin + direction * distance;
            playerHit = true;
        }

        // Jitter visuel
        if (jitterAmount > 0f)
        {
            float jitter = Mathf.Sin(Time.time * jitterSpeed) * jitterAmount;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            endPoint += perpendicular * jitter;
        }

        // Met à jour le LineRenderer
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);

        // Inflige les dégâts
        if (playerHit && playerHealth != null)
        {
            damageTimer += Time.deltaTime;
            if (damageTimer >= damageInterval)
            {
                float damage = damagePerSecond * damageInterval;
                playerHealth.TakeDamage(damage);
                damageTimer = 0f;

                if (debugMode)
                    Debug.Log($"[DroneLaser] Dégâts infligés : {damage}");
            }
        }
        else
        {
            damageTimer = 0f;
        }
    }
}