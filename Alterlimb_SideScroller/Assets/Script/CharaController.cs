using UnityEngine;

public class CharaController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float MoveSpeed = 8f;
    [SerializeField] float Acceleration = 15f;
    [SerializeField] float Deceleration = 20f;

    [Header("Jump")]
    [SerializeField] float JumpForce = 18f;
    [SerializeField] float FallMultiplier = 3f;
    [SerializeField] float CoyoteTime = 0.15f;
    [SerializeField] float JumpBufferTime = 0.1f;
    [SerializeField] float JumpLockoutTime = 0.12f;

    [Header("Dash")]
    [SerializeField] float DashForce = 25f;
    [SerializeField] float DashDuration = 0.15f;
    [SerializeField] float DashCooldown = 0.8f;
    [SerializeField] int MaxAirDashes = 1;

    [Header("Ground Check")]
    [SerializeField] float GroundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    [Header("Ice / Slippery")]
    [SerializeField] LayerMask iceLayer;
    [SerializeField] float IceAcceleration = 2f;
    [Range(0.9f, 1f)]
    [SerializeField] float IceFriction = 0.985f;

    Rigidbody2D rb;
    AbilityEnergySystem energySystem;
    GrapplingHook grapple;

    float defaultGravityScale;

    float inputX;
    float coyoteTimeCounter;
    float jumpBufferCounter;
    float jumpLockoutCounter;

    bool dashRequested;
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

    bool isAutoRunning;
    float autoRunDirection;
    bool isInvincible;

    bool dashEnabled = false;

    public static event System.Action OnPlayerDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OnPlayerDied = null;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        energySystem = GetComponent<AbilityEnergySystem>();
        grapple = GetComponent<GrapplingHook>();
        defaultGravityScale = rb.gravityScale;
    }

    void Update()
    {
        if (isDead) return;

        if (isAutoRunning)
        {
            inputX = autoRunDirection;
            jumpBufferCounter = 0f;
            dashRequested = false;
            return;
        }

        inputX = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = JumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        if (dashEnabled && Input.GetKeyDown(KeyCode.LeftShift)
            && dashCooldownCounter <= 0f && !isDashing)
            dashRequested = true;

        if (dashCooldownCounter > 0f)
            dashCooldownCounter -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (isDead) return;

        UpdateGroundState();

        if (isDashing)
        {
            dashTimeCounter -= Time.fixedDeltaTime;
            if (dashTimeCounter <= 0f) EndDash();
            return;
        }

        HandleJump();
        if (isDead) return;

        HandleDash();
        if (isDead || isDashing) return;

        if (grapple == null) grapple = GetComponent<GrapplingHook>();
        bool isSwinging = grapple != null && grapple.isUsingGrapple;
        if (isSwinging)
        {
            ApplyFallGravity();
            return;
        }

        ApplyHorizontalMovement();
        ApplyFallGravity();
    }

    void UpdateGroundState()
    {
        if (jumpLockoutCounter > 0f)
            jumpLockoutCounter -= Time.fixedDeltaTime;

        bool ignoreGround = jumpLockoutCounter > 0f;

        wasOnIce = isOnIce;

        if (ignoreGround)
        {
            isGrounded = false;
            isOnIce = false;
            coyoteTimeCounter = 0f;
            return;
        }

        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);
        isOnIce = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, iceLayer);

        if (isGrounded || isOnIce)
        {
            airDashesLeft = MaxAirDashes;
            coyoteTimeCounter = CoyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;
        }
    }

    void HandleJump()
    {
        if (jumpBufferCounter <= 0f || coyoteTimeCounter <= 0f) return;

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        if (isInQuicksand)
        {
            Die();
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
        jumpLockoutCounter = JumpLockoutTime;
    }

    void HandleDash()
    {
        if (!dashRequested) return;
        dashRequested = false;

        if (dashCooldownCounter > 0f) return;

        if (isInQuicksand)
        {
            Die();
            return;
        }

        bool canDash = isGrounded || isOnIce || airDashesLeft > 0;
        if (!canDash) return;

        if (!isGrounded && !isOnIce) airDashesLeft--;
        StartDash();
    }

    void ApplyHorizontalMovement()
    {
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
    }

    void ApplyFallGravity()
    {
        if (isInQuicksand) return;
        if (rb.linearVelocity.y >= 0f) return;

        rb.linearVelocity += Vector2.up * Physics2D.gravity.y * rb.gravityScale
                             * (FallMultiplier - 1f) * Time.fixedDeltaTime;
    }

    void StartDash()
    {
        float multiplier = energySystem != null ? energySystem.GetDashMultiplier() : 1f;
        energySystem?.OnDashUsed();

        isDashing = true;
        dashTimeCounter = DashDuration;
        dashCooldownCounter = DashCooldown;
        dashDirection = inputX != 0f ? Mathf.Sign(inputX) : Mathf.Sign(transform.localScale.x);
        rb.linearVelocity = new Vector2(dashDirection * DashForce * multiplier, 0f);
        rb.gravityScale = 0f;
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, 0f);
    }

    public void SetDashEnabled(bool enabled) => dashEnabled = enabled;

    public void ResetJumps()
    {
        airDashesLeft = MaxAirDashes;
        coyoteTimeCounter = CoyoteTime;
        jumpLockoutCounter = 0f;
    }

    public void SetInQuicksand(bool value)
    {
        isInQuicksand = value;
    }

    public void SetAutoRun(bool active, float direction = 1f)
    {
        isAutoRunning = active;
        autoRunDirection = direction;
        if (!active) inputX = 0f;
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    public void TeleportTo(Vector2 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return;
        if (other.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (isInvincible) return;
        if (other.collider.CompareTag("dead_zone") || other.gameObject.layer == LayerMask.NameToLayer("dead_zone"))
            Die();
    }

    public void Die()
    {
        if (isDead) return;
        if (isInvincible) return;

        isDead = true;
        isInQuicksand = false;
        isDashing = false;
        dashRequested = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        OnPlayerDied?.Invoke();

        SpawnManager.Instance.Respawn(this);
    }

    public void Revive(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        isDead = false;
        isInQuicksand = false;
        isDashing = false;
        dashRequested = false;
        jumpBufferCounter = 0f;
        jumpLockoutCounter = 0f;
        GetComponent<PlayerHealth>()?.ResetHealth();
    }

    void OnDisable()
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}