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
    bool isInQuicksand;

    // ── NOUVEAU : Course automatique + invincibilité (transitions de niveau) ──
    bool isAutoRunning;
    float autoRunDirection;
    bool isInvincible;

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

        // ── Course automatique (transitions de niveau) ──────────
        // En auto-run : on force la direction, on garde la détection de sol
        // (pour les animations), mais on ignore tous les autres inputs.
        if (isAutoRunning)
        {
            inputX = autoRunDirection;
            isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);
            wasOnIce = isOnIce;
            isOnIce = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, iceLayer);
            return;
        }

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

    // ── NOUVEAU : API pour LevelTransitionManager ──────────────

    /// <summary>
    /// Active/désactive la course automatique. Pendant l'auto-run :
    ///   - L'input horizontal est forcé à `direction` (+1 = droite, -1 = gauche)
    ///   - Tous les autres inputs (saut, dash) sont ignorés
    ///   - La physique de mouvement et la gravité continuent normalement
    /// </summary>
    public void SetAutoRun(bool active, float direction = 1f)
    {
        isAutoRunning = active;
        autoRunDirection = direction;

        // Si on arrête l'auto-run, on remet inputX à zéro pour éviter
        // que le joueur continue à se déplacer dans le sens forcé.
        if (!active) inputX = 0f;
    }

    /// <summary>
    /// Active/désactive l'invincibilité totale. Bloque les dead_zone et les hazards.
    /// Utilisé pendant les transitions de niveau.
    /// </summary>
    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    /// <summary>
    /// Téléporte le joueur instantanément à la position donnée.
    /// Reset la vélocité pour éviter qu'il continue sur sa lancée.
    /// </summary>
    public void TeleportTo(Vector2 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
    }

    // ── Mort & Respawn ─────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return; // ← protection pendant les transitions

        if (other.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (isInvincible) return; // ← protection pendant les transitions

        if (other.collider.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    public void Die()
    {
        if (isDead) return;
        if (isInvincible) return; // ← double sécurité au cas où Die() serait appelé directement
        isDead = true;
        isInQuicksand = false;
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
        isInQuicksand = false;
        GetComponent<PlayerHealth>()?.ResetHealth();
    }
}