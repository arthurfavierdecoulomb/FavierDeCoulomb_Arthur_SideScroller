using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    [Header("Grappin")]
    [SerializeField] float maxDistance = 15f;
    [SerializeField] float deploySpeed = 25f;
    [SerializeField] float swingForce = 8f;
    [SerializeField] LayerMask hookableLayers;

    [Header("Swing Settings")]
    [SerializeField] float ropeMaxLength = 12f;
    [SerializeField] float ropeShortenSpeed = 3f;
    [SerializeField] float minRopeLength = 2f;

    [Header("Refs")]
    [SerializeField] Transform firePoint;
    [SerializeField] LineRenderer lineRenderer;

    Rigidbody2D rb;
    Camera cam;

    public bool canUseGrapple = true;
    public bool isUsingGrapple = false;

    enum GrappleState { Idle, Deploying, Hooked }
    GrappleState state = GrappleState.Idle;

    Vector2 hookPoint;
    Vector2 hookTipPosition;

    SpringJoint2D springJoint;
    DroneEnemy pendingDrone = null; // drone détecté, en attente d'accroche

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
        if (!canUseGrapple) return;

        if (Input.GetMouseButtonDown(0) && state == GrappleState.Idle)
        {
            isUsingGrapple = true;
            TryShoot();
        }

        if (Input.GetMouseButtonUp(0) && state != GrappleState.Idle)
        {
            isUsingGrapple = false;
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

            // On stocke le drone mais on NE l'active PAS encore
            // Il sera activé seulement quand le grappin arrive à destination
            pendingDrone = hit.collider.GetComponent<DroneEnemy>();
        }
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

        // Si c'est un drone, on le fait tomber et on ne crée pas de SpringJoint
        if (pendingDrone != null)
        {
            pendingDrone.GetHooked();
            pendingDrone = null;
            // La corde reste visible 0.3s puis se détache automatiquement
            Invoke(nameof(ReleaseGrapple), 0.3f);
            return;
        }

        // Sinon accroche normale sur le décor
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
        if (state != GrappleState.Hooked || springJoint == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            springJoint.distance = Mathf.Clamp(
                springJoint.distance - scroll * ropeShortenSpeed,
                minRopeLength,
                ropeMaxLength
            );
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        if (inputX != 0)
        {
            rb.AddForce(new Vector2(inputX * swingForce, 0f), ForceMode2D.Force);
        }
    }

    public void ReleaseGrapple()
    {
        CancelInvoke(nameof(ReleaseGrapple));
        state = GrappleState.Idle;
        lineRenderer.enabled = false;
        pendingDrone = null;

        if (springJoint != null)
        {
            Destroy(springJoint);
            springJoint = null;
        }
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
        lineRenderer.SetPosition(1, hookTipPosition);
    }

    void OnDrawGizmosSelected()
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