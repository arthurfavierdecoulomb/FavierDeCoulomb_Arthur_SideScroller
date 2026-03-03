using UnityEngine;

public class CharaController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float MoveSpeed = 8f;
    [SerializeField] float Acceleration = 15f;
    [SerializeField] float Deceleration = 20f;

    [Header("Jump")]
    [SerializeField] float JumpForce = 18f;
    [SerializeField] float FallMultiplier = 3f;       // chute plus rapide
    [SerializeField] float LowJumpMultiplier = 5f;    // saut court si on relâche tôt
    [SerializeField] float CoyoteTime = 0.15f;        // peut sauter un peu après le bord
    [SerializeField] float JumpBufferTime = 0.1f;     // mémorise l'input saut

    [Header("Dash")]
    [SerializeField] float DashForce = 25f;
    [SerializeField] float DashDuration = 0.15f;
    [SerializeField] float DashCooldown = 0.8f;
    [SerializeField] int MaxAirDashes = 1;            // dashes autorisés en l'air

    [Header("Ground Check")]
    [SerializeField] float GroundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    Rigidbody2D rb;
    float inputX;

    // Coyote time & jump buffer
    float coyoteTimeCounter;
    float jumpBufferCounter;

    // Dash
    bool isDashing;
    float dashTimeCounter;
    float dashCooldownCounter;
    int airDashesLeft;
    float dashDirection;

    bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");

        // Ground check
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);

        // Reset air dashes au sol
        if (isGrounded)
        {
            airDashesLeft = MaxAirDashes;
            coyoteTimeCounter = CoyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Jump buffer
        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = JumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // Saut
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // Dash input
        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownCounter <= 0f && !isDashing)
        {
            bool canDash = isGrounded || airDashesLeft > 0;
            if (canDash)
            {
                StartDash();
                if (!isGrounded) airDashesLeft--;
            }
        }

        // Cooldown
        if (dashCooldownCounter > 0f)
            dashCooldownCounter -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            dashTimeCounter -= Time.fixedDeltaTime;
            if (dashTimeCounter <= 0f) EndDash();
            return;
        }

        // Mouvement horizontal — on multiplie par 50 pour compenser Time.fixedDeltaTime
        float targetSpeedX = inputX * MoveSpeed;
        float accel = (Mathf.Abs(inputX) > 0.01f) ? Acceleration : Deceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX, accel * Time.fixedDeltaTime * 50f);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);

        // Gravité dynamique
        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (FallMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (LowJumpMultiplier - 1) * Time.fixedDeltaTime;
    }

    void StartDash()
    {
        isDashing = true;
        dashTimeCounter = DashDuration;
        dashCooldownCounter = DashCooldown;

        // Direction du dash : input ou direction du perso
        dashDirection = inputX != 0 ? Mathf.Sign(inputX) : Mathf.Sign(transform.localScale.x);

        rb.linearVelocity = new Vector2(dashDirection * DashForce, 0f); // annule la vélocité verticale
        rb.gravityScale = 0f; // neutralise la gravité pendant le dash
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, 0f); // freine en sortie de dash
    }
}