using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DroneLaser : MonoBehaviour
{
    [Header("Références — Deux sorties de laser")]
    [SerializeField] Transform laserOriginRight;
    [SerializeField] Transform laserOriginLeft;

    [Header("Cible")]
    [SerializeField] string playerTag = "Player";

    [Header("Comportement")]
    [SerializeField] float maxRange = 15f;
    [SerializeField] LayerMask obstacleLayers;

    [Header("Dégâts")]
    [SerializeField] float damagePerSecond = 25f;
    [SerializeField] float damageInterval = 0.1f;

    [Header("Visuel")]
    [SerializeField] float jitterAmount = 0.05f;
    [SerializeField] float jitterSpeed = 30f;
    [SerializeField] float minWidthRatio = 0.15f;
    [SerializeField] float intensitySmoothing = 25f;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    LineRenderer lineRenderer;
    Transform player;
    PlayerHealth playerHealth;
    bool isFiring;
    bool damageEnabled = true;
    float damageTimer;
    float targetIntensity = 1f;
    float currentIntensity = 1f;
    float baseWidth;
    Color baseStartColor;
    Color baseEndColor;

    Transform activeOrigin;

    public bool IsFiring => isFiring;
    public float Intensity => currentIntensity;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
        lineRenderer.positionCount = 2;

        baseWidth = lineRenderer.widthMultiplier;
        baseStartColor = lineRenderer.startColor;
        baseEndColor = lineRenderer.endColor;

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

        activeOrigin = laserOriginRight != null ? laserOriginRight : laserOriginLeft;
    }

    public void SetFiring(bool firing)
    {
        isFiring = firing;
        if (lineRenderer != null) lineRenderer.enabled = firing;
        if (!firing) damageTimer = 0f;
    }

    public void SetIntensity(float intensity)
    {
        targetIntensity = Mathf.Clamp01(intensity);
    }

    public void SetIntensityInstant(float intensity)
    {
        targetIntensity = Mathf.Clamp01(intensity);
        currentIntensity = targetIntensity;
        ApplyIntensity();
    }

    public void SetDamageEnabled(bool enabled)
    {
        damageEnabled = enabled;
        if (!enabled) damageTimer = 0f;
    }

    public void SetActiveOrigin(bool facingRight)
    {
        Transform desired = facingRight ? laserOriginRight : laserOriginLeft;
        if (desired != null) activeOrigin = desired;
    }

    void Update()
    {
        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, intensitySmoothing * Time.deltaTime);

        if (!isFiring || activeOrigin == null || player == null) return;

        ApplyIntensity();

        Vector2 origin = activeOrigin.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        float playerDistance = toPlayer.magnitude;
        float distance = Mathf.Min(playerDistance, maxRange);
        Vector2 direction = toPlayer.normalized;

        RaycastHit2D obstacleHit = Physics2D.Raycast(origin, direction, distance, obstacleLayers);

        Vector2 endPoint;
        bool playerHit;

        if (obstacleHit.collider != null)
        {
            endPoint = obstacleHit.point;
            playerHit = false;

            if (debugMode)
                Debug.Log($"[DroneLaser] Mur en chemin : {obstacleHit.collider.name}");
        }
        else
        {
            endPoint = origin + direction * distance;
            playerHit = playerDistance <= maxRange;
        }

        if (jitterAmount > 0f)
        {
            float jitter = Mathf.Sin(Time.time * jitterSpeed) * jitterAmount;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            endPoint += perpendicular * jitter;
        }

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);

        if (playerHit && damageEnabled && playerHealth != null)
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

    void ApplyIntensity()
    {
        if (lineRenderer == null) return;

        lineRenderer.widthMultiplier = baseWidth * Mathf.Lerp(minWidthRatio, 1f, currentIntensity);

        Color start = baseStartColor;
        Color end = baseEndColor;
        start.a = baseStartColor.a * currentIntensity;
        end.a = baseEndColor.a * currentIntensity;

        lineRenderer.startColor = start;
        lineRenderer.endColor = end;
    }
}