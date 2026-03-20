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

    Rigidbody2D rb;
    float inputX;
    float coyoteTimeCounter;
    float jumpBufferCounter;
    bool isDashing;
    float dashTimeCounter;
    float dashCooldownCounter;
    int airDashesLeft;
    float dashDirection;
    bool isGrounded;
    bool isDead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (isDead) return;

        inputX = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);

        if (isGrounded)
        {
            airDashesLeft = MaxAirDashes;
            coyoteTimeCounter = CoyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = JumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownCounter <= 0f && !isDashing)
        {
            bool canDash = isGrounded || airDashesLeft > 0;
            if (canDash)
            {
                StartDash();
                if (!isGrounded) airDashesLeft--;
            }
        }

        if (dashCooldownCounter > 0f)
            dashCooldownCounter -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (isDashing)
        {
            dashTimeCounter -= Time.fixedDeltaTime;
            if (dashTimeCounter <= 0f) EndDash();
            return;
        }

        GrapplingHook grapple = GetComponent<GrapplingHook>();
        bool isSwinging = grapple != null && grapple.isUsingGrapple;


        if (isSwinging)
        {
            return; 
        }



        float targetSpeedX = inputX * MoveSpeed;
        float accel = (Mathf.Abs(inputX) > 0.01f) ? Acceleration : Deceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX, accel * Time.fixedDeltaTime * 50f);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);

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
        dashDirection = inputX != 0 ? Mathf.Sign(inputX) : Mathf.Sign(transform.localScale.x);
        rb.linearVelocity = new Vector2(dashDirection * DashForce, 0f);
        rb.gravityScale = 0f;
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, 0f);
    }

    
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
        GetComponent<PlayerHealth>()?.ResetHealth();
    }
}