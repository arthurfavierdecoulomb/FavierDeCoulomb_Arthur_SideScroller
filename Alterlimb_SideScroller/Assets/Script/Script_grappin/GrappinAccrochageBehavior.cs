using UnityEngine;

/// <summary>
/// Grappin du joueur : déploiement progressif, accroche sur décor ou drone,
/// swing latéral et contrôle de la longueur de corde.
/// 
/// États logiques (exposés via la propriété State pour les systèmes visuels) :
///   - Idle      : rien en cours
///   - Deploying : le harpon voyage vers le point d'accroche
///   - Hooked    : accroché, le SpringJoint2D est actif
/// 
/// isUsingGrapple : vrai uniquement quand le grappin est réellement en action
/// (tir qui a accroché). Reste faux si le tir part dans le vide.
/// </summary>
public class GrapplingHook : MonoBehaviour
{
    public enum GrappleState { Idle, Deploying, Hooked }

    [Header("Grappin")]
    [SerializeField] float maxDistance = 15f;
    [SerializeField] float deploySpeed = 25f;
    [SerializeField] float swingForce = 3f;
    [SerializeField] LayerMask hookableLayers;

    [Header("Swing Settings")]
    [SerializeField] float ropeMaxLength = 12f;
    [SerializeField] float ropeShortenSpeed = 3f;
    [SerializeField] float minRopeLength = 2f;
    [SerializeField] float maxSwingSpeed = 8f;

    [Header("Refs")]
    [SerializeField] Transform firePoint;
    [SerializeField] LineRenderer lineRenderer;

    Rigidbody2D rb;
    Camera cam;

    public bool canUseGrapple = true;
    public bool isUsingGrapple = false;

    GrappleState state = GrappleState.Idle;

    Vector2 hookPoint;
    Vector2 hookTipPosition;

    SpringJoint2D springJoint;
    DroneEnemy hookedDrone = null;
    Rigidbody2D droneRb = null;

    // ════════════════════════════════════════════════════════════
    //  Accès public (pour les systèmes visuels : bras, harpon, anim)
    // ════════════════════════════════════════════════════════════

    /// <summary>État actuel du grappin (Idle / Deploying / Hooked).</summary>
    public GrappleState State => state;

    /// <summary>Position du bout de la corde (le harpon) en temps réel.</summary>
    public Vector2 HookTipPosition => hookTipPosition;

    /// <summary>Vrai si le grappin est accroché (état Hooked).</summary>
    public bool IsHooked => state == GrappleState.Hooked;

    /// <summary>
    /// Direction du dernier scroll molette : 1 = raccourcir (dur),
    /// -1 = rallonger (moux), 0 = pas de scroll. Mis à jour chaque frame.
    /// </summary>
    public int ScrollDirection { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
    }

    void Update()
    {
        HandleInput();
        UpdateDeployment();
        UpdateRopeLength();
        DrawRope();
    }

    void HandleInput()
    {
        if (!canUseGrapple)
        {
            // Sécurité : si on change d'artefact pendant qu'on grappine,
            // on relâche proprement.
            if (state != GrappleState.Idle) ReleaseGrapple();
            return;
        }

        if (Input.GetMouseButtonDown(0) && state == GrappleState.Idle)
        {
            TryShoot();
        }

        if (Input.GetMouseButtonUp(0) && state != GrappleState.Idle)
        {
            ReleaseGrapple();
        }
    }

    void TryShoot()
    {
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - (Vector2)firePoint.position).normalized;

        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, direction, maxDistance, hookableLayers);

        if (hit.collider != null)
        {
            hookPoint = hit.point;
            hookTipPosition = firePoint.position;
            state = GrappleState.Deploying;
            lineRenderer.enabled = true;

            // Le grappin est réellement en action (le tir a accroché)
            isUsingGrapple = true;

            hookedDrone = hit.collider.GetComponent<DroneEnemy>();
            if (hookedDrone != null)
                droneRb = hit.collider.GetComponent<Rigidbody2D>();
        }
        // Si le raycast ne touche rien : isUsingGrapple reste false,
        // le bras grappin n'apparaîtra pas pour un tir dans le vide.
    }

    void UpdateDeployment()
    {
        if (state != GrappleState.Deploying) return;

        hookTipPosition = Vector2.MoveTowards(hookTipPosition, hookPoint, deploySpeed * Time.deltaTime);

        if (Vector2.Distance(hookTipPosition, hookPoint) < 0.1f)
        {
            hookTipPosition = hookPoint;
            AttachGrapple();
        }
    }

    void AttachGrapple()
    {
        state = GrappleState.Hooked;

        if (hookedDrone != null)
        {
            hookedDrone.GetHooked();

            // SpringJoint sur le JOUEUR → tire le joueur vers le drone
            springJoint = gameObject.AddComponent<SpringJoint2D>();
            springJoint.autoConfigureConnectedAnchor = false;
            springJoint.connectedBody = droneRb;
            springJoint.connectedAnchor = Vector2.zero;
            springJoint.distance = Vector2.Distance(transform.position, hookedDrone.transform.position);
            springJoint.dampingRatio = 0.5f;
            springJoint.frequency = 2f;
            springJoint.enableCollision = true;
            return;
        }

        // Accroche sur décor
        springJoint = gameObject.AddComponent<SpringJoint2D>();
        springJoint.autoConfigureConnectedAnchor = false;
        springJoint.connectedAnchor = hookPoint;

        float dist = Vector2.Distance(firePoint.position, hookPoint);
        springJoint.distance = Mathf.Min(dist, ropeMaxLength);
        springJoint.dampingRatio = 0.5f;
        springJoint.frequency = 2f;
        springJoint.enableCollision = true;
    }

    void UpdateRopeLength()
    {
        // Réinitialise la direction de scroll chaque frame
        ScrollDirection = 0;

        if (state != GrappleState.Hooked || springJoint == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            // Mémorise le sens du scroll pour l'animation du bras
            ScrollDirection = (scroll > 0f) ? 1 : -1;

            springJoint.distance = Mathf.Clamp(
                springJoint.distance - scroll * ropeShortenSpeed,
                minRopeLength,
                ropeMaxLength
            );
        }

        // Swing latéral uniquement sur décor
        if (hookedDrone == null)
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            if (inputX != 0 && Mathf.Abs(rb.linearVelocity.x) < maxSwingSpeed)
            {
                float targetX = inputX * maxSwingSpeed;
                float newX = Mathf.Lerp(rb.linearVelocity.x, targetX, swingForce * Time.deltaTime);
                rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            }
        }
    }

    public void ReleaseGrapple()
    {
        CancelInvoke(nameof(ReleaseGrapple));
        state = GrappleState.Idle;
        isUsingGrapple = false;
        ScrollDirection = 0;
        lineRenderer.enabled = false;

        if (springJoint != null)
        {
            Destroy(springJoint);
            springJoint = null;
        }

        // Le drone reprend son IA
        if (hookedDrone != null)
        {
            hookedDrone.ReleaseHook();
            hookedDrone = null;
        }

        droneRb = null;
    }

    void DrawRope()
    {
        if (state == GrappleState.Idle)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, firePoint.position);

        if (hookedDrone != null && state == GrappleState.Hooked)
            lineRenderer.SetPosition(1, hookedDrone.transform.position);
        else
            lineRenderer.SetPosition(1, hookTipPosition);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxDistance);

        if (firePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(firePoint.position, 0.1f);
        }
    }
}