using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    Animator animator;
    Rigidbody2D rb;

    static readonly int IsRunning = Animator.StringToHash("isRunning");
    static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    static readonly int IsFalling = Animator.StringToHash("isFalling");

    [SerializeField] float GroundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    bool isGrounded;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, groundLayer);

        animator.SetBool(IsRunning, isGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.1f);
        animator.SetBool(IsGrounded, isGrounded);
        animator.SetBool(IsFalling, !isGrounded && rb.linearVelocity.y < -0.1f);

        // Flip du sprite
        if (rb.linearVelocity.x > 0.1f)
            transform.localScale = new Vector3(1, 1, 1);
        else if (rb.linearVelocity.x < -0.1f)
            transform.localScale = new Vector3(-1, 1, 1);
    }
}