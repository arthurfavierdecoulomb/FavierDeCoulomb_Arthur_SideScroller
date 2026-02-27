using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    [Header("Grappin")]
    [SerializeField] float maxDistance = 15f;
    [SerializeField] float deploySpeed = 25f;       // vitesse de déploiement visuel
    [SerializeField] float pullForce = 12f;         // force d'attraction vers le point
    [SerializeField] float swingForce = 8f;         // force latérale pendant le swing
    [SerializeField] LayerMask hookableLayers;      // layers sur lesquels on peut s'accrocher

    [Header("Swing Settings")]
    [SerializeField] float ropeMaxLength = 12f;
    [SerializeField] float ropeShortenSpeed = 3f;   // raccourcir la corde avec scroll
    [SerializeField] float minRopeLength = 2f;

    [Header("Refs")]
    [SerializeField] Transform firePoint;           // point de départ du grappin (main du perso)
    [SerializeField] LineRenderer lineRenderer;

    Rigidbody2D rb;
    Camera cam;

    // State
    enum GrappleState { Idle, Deploying, Hooked }
    GrappleState state = GrappleState.Idle;

    Vector2 hookTarget;         // destination visée
    Vector2 hookPoint;          // point d'accroche réel
    Vector2 hookTipPosition;    // position animée de la tête du grappin

    SpringJoint2D springJoint;

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
        // Tir grappin
        if (Input.GetMouseButtonDown(0) && state == GrappleState.Idle)
        {
            TryShoot();
        }

        // Relâcher
        if (Input.GetMouseButtonUp(0) && state != GrappleState.Idle)
        {
            ReleaseGrapple();
        }
    }

    void TryShoot()
    {
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - (Vector2)firePoint.position).normalized;

        // Raycast pour vérifier si on touche quelque chose d'accrochable
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, direction, maxDistance, hookableLayers);

        if (hit.collider != null)
        {
            hookPoint = hit.point;
            hookTarget = hit.point;
            hookTipPosition = firePoint.position;
            state = GrappleState.Deploying;

            lineRenderer.enabled = true;
        }
    }

    void UpdateDeployment()
    {
        if (state != GrappleState.Deploying) return;

        // Anime la tête du grappin vers le point d'accroche
        hookTipPosition = Vector2.MoveTowards(hookTipPosition, hookPoint, deploySpeed * Time.deltaTime);

        // Est-ce qu'on est arrivé ?
        if (Vector2.Distance(hookTipPosition, hookPoint) < 0.1f)
        {
            hookTipPosition = hookPoint;
            AttachGrapple();
        }
    }

    void AttachGrapple()
    {
        state = GrappleState.Hooked;

        // Crée un SpringJoint pour simuler la corde
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
        if (state != GrappleState.Hooked) return;

        // Scroll pour raccourcir/allonger la corde
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            springJoint.distance = Mathf.Clamp(
                springJoint.distance - scroll * ropeShortenSpeed,
                minRopeLength,
                ropeMaxLength
            );
        }

        // Swing latéral avec A/D pendant l'accroche
        float inputX = Input.GetAxisRaw("Horizontal");
        if (inputX != 0)
        {
            rb.AddForce(new Vector2(inputX * swingForce, 0f), ForceMode2D.Force);
        }
    }

    void ReleaseGrapple()
    {
        state = GrappleState.Idle;
        lineRenderer.enabled = false;

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

    // Dessine la portée max dans l'éditeur
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
