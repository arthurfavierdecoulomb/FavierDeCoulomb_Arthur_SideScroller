using UnityEngine;

public enum JumpMode { Normal, High }

public class CharaController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float MoveSpeed = 8f;
    [SerializeField] float Acceleration = 15f;
    [SerializeField] float Deceleration = 20f;

    [Header("Jump")]
    [SerializeField] float JumpForce = 18f;
    [SerializeField] float HighJumpForce = 28f;
    [SerializeField] float FallMultiplier = 3f;
    [SerializeField] float LowJumpMultiplier = 5f;
    [SerializeField] float CoyoteTime = 0.15f;
    [SerializeField] float JumpBufferTime = 0.1f;

    [Header("Dash")]
    [SerializeField] float DashForce = 25f;
    [SerializeField] float DashDuration = 0.15f;
    [SerializeField] float DashCooldown = 0.8f;
    [SerializeField] int MaxAirDashes = 1;

    [Header("Ground Check")]
    [SerializeField] float GroundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    // ── Glissade ───────────────────────────────────────────────
    [Header("Ice / Slippery")]
    [SerializeField] LayerMask iceLayer;
    [Tooltip("Force de poussée latérale sur glace (faible = difficile à changer de direction)")]
    [SerializeField] float IceAcceleration = 2f;
    [Tooltip("Freinage passif sur glace sans input : 0.999 = glisse infinie, 0.95 = s'arrête vite")]
    [Range(0.9f, 1f)]
    [SerializeField] float IceFriction = 0.985f;
    // ──────────────────────────────────────────────────────────

    Rigidbody2D rb;
    AbilityEnergySystem energySystem;

    float inputX;
    float coyoteTimeCounter;
    float jumpBufferCounter;
    bool isDashing;
    float dashTimeCounter;
    float dashCooldownCounter;
    int airDashesLeft;
    float dashDirection;
    bool isGrounded;
    bool isOnIce;
    bool wasOnIce;
    bool isDead;
    bool isInQuicksand; // ← NOUVEAU : état sable mouvant

    JumpMode jumpMode = JumpMode.Normal;
    bool dashEnabled = false;

    // ── Awake ──────────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        energySystem = GetComponent<AbilityEnergySystem>();
    }

    // ── Update (input + timers) ────────────────────────────────
    void Update()
    {
        if (isDead) return;

        inputX = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);

        wasOnIce = isOnIce;
        isOnIce = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, iceLayer);

        if (isGrounded || isOnIce)
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

        // ── SAUT ───────────────────────────────────────────────
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            // Sable mouvant : sauter = mort
            if (isInQuicksand)
            {
                Die();
                return;
            }

            float multiplier = 1f;
            if (jumpMode == JumpMode.High)
            {
                multiplier = energySystem != null ? energySystem.GetJumpMultiplier() : 1f;
                energySystem?.OnJumpBoostUsed();
            }

            float force = (jumpMode == JumpMode.High)
                ? HighJumpForce * multiplier
                : JumpForce;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // ── DASH ──────────────────────────────────────────────
        if (dashEnabled && Input.GetKeyDown(KeyCode.LeftShift)
            && dashCooldownCounter <= 0f && !isDashing)
        {
            // Sable mouvant : dasher = mort
            if (isInQuicksand)
            {
                Die();
                return;
            }

            bool canDash = isGrounded || isOnIce || airDashesLeft > 0;
            if (canDash)
            {
                StartDash();
                if (!isGrounded && !isOnIce) airDashesLeft--;
            }
        }

        if (dashCooldownCounter > 0f)
            dashCooldownCounter -= Time.deltaTime;
    }

    // ── FixedUpdate (physique) ─────────────────────────────────
    void FixedUpdate()
    {
        if (isDead) return;

        if (isDashing)
        {
            dashTimeCounter -= Time.fixedDeltaTime;
            if (dashTimeCounter <= 0f) EndDash();
            return;
        }

        // Grappin
        GrapplingHook grapple = GetComponent<GrapplingHook>();
        bool isSwinging = grapple != null && grapple.isUsingGrapple;

        if (isSwinging)
        {
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y
                                     * (FallMultiplier - 1) * Time.fixedDeltaTime;
            return;
        }

        // ── Mouvement horizontal ───────────────────────────────
        float targetSpeedX = inputX * MoveSpeed;
        bool treatAsIce = isOnIce || (wasOnIce && isGrounded);

        if (treatAsIce)
        {
            if (Mathf.Abs(inputX) > 0.01f)
            {
                float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX,
                                               IceAcceleration * Time.fixedDeltaTime * 50f);
                rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            }
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * IceFriction, rb.linearVelocity.y);
            }
        }
        else
        {
            float accel = (Mathf.Abs(inputX) > 0.01f) ? Acceleration : Deceleration;
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX, accel * Time.fixedDeltaTime * 50f);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }

        // ── Gravité augmentée ──────────────────────────────────
        // On désactive la gravité augmentée si on est dans le sable
        // (le QuicksandZone gère déjà la chute lente)
        if (!isInQuicksand)
        {
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y
                                     * (FallMultiplier - 1) * Time.fixedDeltaTime;
            else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y
                                     * (LowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    // ── Dash ───────────────────────────────────────────────────
    void StartDash()
    {
        float multiplier = energySystem != null ? energySystem.GetDashMultiplier() : 1f;
        energySystem?.OnDashUsed();

        isDashing = true;
        dashTimeCounter = DashDuration;
        dashCooldownCounter = DashCooldown;
        dashDirection = inputX != 0 ? Mathf.Sign(inputX) : Mathf.Sign(transform.localScale.x);
        rb.linearVelocity = new Vector2(dashDirection * DashForce * multiplier, 0f);
        rb.gravityScale = 0f;
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, 0f);
    }

    // ── API publique ───────────────────────────────────────────
    public void SetJumpMode(JumpMode mode) => jumpMode = mode;
    public void SetDashEnabled(bool enabled) => dashEnabled = enabled;

    public void ResetJumps()
    {
        airDashesLeft = MaxAirDashes;
        coyoteTimeCounter = CoyoteTime;
    }

    /// <summary>Appelé par QuicksandZone quand le joueur entre/sort.</summary>
    public void SetInQuicksand(bool value)
    {
        isInQuicksand = value;
    }

    // ── Mort & Respawn ─────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        isInQuicksand = false; // sécurité : reset l'état au cas où
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        SpawnManager.Instance.Respawn(this);
    }

    public void Revive(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        rb.gravityScale = 1f;
        rb.linearVelocity = Vector2.zero;
        isDead = false;
        isInQuicksand = false; // reset à la résurrection
        GetComponent<PlayerHealth>()?.ResetHealth();
    }
}