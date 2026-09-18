using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Override Controllers")]
    [SerializeField] AnimatorOverrideController handOverride;
    [SerializeField] AnimatorOverrideController sawOverride;
    [SerializeField] AnimatorOverrideController grappleOverride;

    [Header("Ground Check")]
    [SerializeField] float groundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    [Header("Wall Grip Check")]
    [SerializeField] LayerMask wallGripLayer;
    [SerializeField] float wallCheckDistance = 0.6f;
    [SerializeField] float[] wallCheckVerticalOffsets = { -0.4f, 0f, 0.4f };

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    Animator animator;
    Rigidbody2D rb;
    AbilityManager abilityManager;

    ArmAbility lastAppliedArm;
    bool isGrounded;
    bool isWallGripping;

    static readonly int IsRunning = Animator.StringToHash("isRunning");
    static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    static readonly int IsFalling = Animator.StringToHash("isFalling");
    static readonly int AttackTrigger = Animator.StringToHash("Attack");
    static readonly int IsWallGripping = Animator.StringToHash("isWallGripping");

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        abilityManager = GetComponent<AbilityManager>();

        if (abilityManager == null)
        {
            Debug.LogError("[PlayerAnimator] AbilityManager introuvable sur le joueur !");
            return;
        }

        lastAppliedArm = ArmAbility.Hand;
        ApplyOverrideForArm(ArmAbility.Hand);
    }

    void Update()
    {
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, groundLayer);

        isWallGripping = CheckWallGrip();

        animator.SetBool(IsRunning, isGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.1f);
        animator.SetBool(IsGrounded, isGrounded);
        animator.SetBool(IsFalling, !isGrounded && rb.linearVelocity.y < -0.1f);
        animator.SetBool(IsWallGripping, isWallGripping);

        if (rb.linearVelocity.x > 0.1f)
            transform.localScale = new Vector3(1, 1, 1);
        else if (rb.linearVelocity.x < -0.1f)
            transform.localScale = new Vector3(-1, 1, 1);

        if (abilityManager != null && abilityManager.CurrentArm != lastAppliedArm)
        {
            ApplyOverrideForArm(abilityManager.CurrentArm);
            lastAppliedArm = abilityManager.CurrentArm;
        }
    }

    bool CheckWallGrip()
    {
        if (isGrounded) return false;

        bool wallDetected = RaycastWallSide(Vector2.right) || RaycastWallSide(Vector2.left);

        return wallDetected;
    }

    bool RaycastWallSide(Vector2 direction)
    {
        foreach (float yOffset in wallCheckVerticalOffsets)
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * yOffset;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance, wallGripLayer);

            if (debugMode)
            {
                Color rayColor = (hit.collider != null) ? Color.green : Color.red;
                Debug.DrawRay(origin, direction * wallCheckDistance, rayColor);
            }

            if (hit.collider != null) return true;
        }

        return false;
    }

    void ApplyOverrideForArm(ArmAbility arm)
    {
        AnimatorOverrideController target = null;
        switch (arm)
        {
            case ArmAbility.Hand: target = handOverride; break;
            case ArmAbility.Saw: target = sawOverride; break;
            case ArmAbility.Grapple: target = grappleOverride; break;
        }

        if (target == null)
        {
            if (debugMode) Debug.LogWarning($"[PlayerAnimator] Override Controller manquant pour {arm}");
            return;
        }

        animator.runtimeAnimatorController = target;

        if (debugMode) Debug.Log($"[PlayerAnimator] Override appliqué : {arm}");
    }

    public void TriggerAttack()
    {
        animator.SetTrigger(AttackTrigger);
    }

    public void TriggerWakeUp(string triggerName)
    {
        animator.SetTrigger(triggerName);
    }
}