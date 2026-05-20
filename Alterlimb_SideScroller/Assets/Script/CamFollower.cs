using UnityEngine;

/// <summary>
/// Caméra qui suit le joueur avec smoothing différencié X/Y, look-ahead
/// (anticipation du mouvement), dead zone (zone centrale où la cam ne bouge
/// pas) et bornes optionnelles.
/// 
/// ZOOM : la caméra interpole en douceur vers un orthographic size cible.
/// Le zoom par défaut est mémorisé au démarrage. Des CameraZoomZone peuvent
/// demander un zoom différent via SetTargetZoom() et revenir au défaut via
/// ResetZoom().
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Smoothing")]
    [SerializeField] float smoothSpeedX = 6f;
    [SerializeField] float smoothSpeedY = 4f;

    [Header("Offset")]
    [SerializeField] Vector3 offset = new Vector3(0f, 1.5f, -10f);

    [Header("Look Ahead (anticipation)")]
    [SerializeField] float lookAheadDistance = 2f;
    [SerializeField] float lookAheadSpeed = 4f;

    [Header("Dead Zone (zone sans mouvement cam)")]
    [SerializeField] float deadZoneX = 0.5f;
    [SerializeField] float deadZoneY = 0.8f;

    [Header("Camera Bounds (optionnel)")]
    [SerializeField] bool useBounds = false;
    [SerializeField] float minX, maxX, minY, maxY;

    [Header("Zoom")]
    [Tooltip("Vitesse d'interpolation du zoom vers la valeur cible")]
    [SerializeField] float zoomSpeed = 3f;

    // ── Runtime ──
    float currentLookAhead;
    float targetLookAhead;
    float lastTargetX;

    Camera cam;
    float defaultZoom;   // orthographic size par défaut, mémorisé au démarrage
    float targetZoom;    // zoom actuellement demandé (par défaut ou par une zone)

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultZoom = cam.orthographicSize;
        targetZoom = defaultZoom;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ── Position ──
        Vector3 targetPos = target.position + offset;

        // Look ahead : anticipe la direction du perso
        float moveDirectionX = target.position.x - lastTargetX;
        if (Mathf.Abs(moveDirectionX) > 0.01f)
            targetLookAhead = Mathf.Sign(moveDirectionX) * lookAheadDistance;

        currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead,
                                       lookAheadSpeed * Time.deltaTime);
        lastTargetX = target.position.x;

        targetPos.x += currentLookAhead;

        // Dead zone : la cam ne bouge que si le perso sort de la zone
        float diffX = targetPos.x - transform.position.x;
        float diffY = targetPos.y - transform.position.y;
        if (Mathf.Abs(diffX) < deadZoneX) targetPos.x = transform.position.x;
        if (Mathf.Abs(diffY) < deadZoneY) targetPos.y = transform.position.y;

        // Smooth
        Vector3 smoothed = new Vector3
        (
            Mathf.Lerp(transform.position.x, targetPos.x, smoothSpeedX * Time.deltaTime),
            Mathf.Lerp(transform.position.y, targetPos.y, smoothSpeedY * Time.deltaTime),
            offset.z
        );

        // Clamp dans les bounds si activé
        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minX, maxX);
            smoothed.y = Mathf.Clamp(smoothed.y, minY, maxY);
        }

        transform.position = smoothed;

        // ── Zoom : interpolation douce vers la valeur cible ──
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom,
                                           zoomSpeed * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════
    //  API publique — zoom
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Demande à la caméra de zoomer vers une nouvelle valeur (orthographic size).
    /// Appelé par les CameraZoomZone quand le joueur entre dedans.
    /// Plus la valeur est petite, plus c'est zoomé. Plus elle est grande,
    /// plus c'est dézoomé.
    /// </summary>
    public void SetTargetZoom(float newZoom)
    {
        targetZoom = newZoom;
    }

    /// <summary>
    /// Remet la cible de zoom à la valeur par défaut (celle au lancement).
    /// Appelé par les CameraZoomZone quand le joueur en sort.
    /// </summary>
    public void ResetZoom()
    {
        targetZoom = defaultZoom;
    }

    /// <summary>Le zoom par défaut, mémorisé au lancement (utile pour les zones).</summary>
    public float DefaultZoom => defaultZoom;
}