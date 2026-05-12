using UnityEngine;

/// <summary>
/// Gère les animations du joueur en fonction du bras actif.
/// 
/// Le graphe d'animation (transitions Idle/Course/Saut/Attack) est partagé
/// entre tous les bras via un Animator Controller de base. Chaque bras a
/// son Animator Override Controller qui remplace les clips visuels.
/// 
/// Le bras actif est piloté par AbilityManager (touche Q pour cycler).
/// PlayerAnimator se contente de suivre l'état d'AbilityManager.
/// 
/// L'attaque visuelle est déclenchée par SawAbility via TriggerAttack().
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Override Controllers")]
    [SerializeField] AnimatorOverrideController handOverride;
    [SerializeField] AnimatorOverrideController sawOverride;
    [SerializeField] AnimatorOverrideController grappleOverride;

    [Header("Ground Check")]
    [SerializeField] float groundCheckDistance = 1.1f;
    [SerializeField] LayerMask groundLayer;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    // Refs
    Animator animator;
    Rigidbody2D rb;
    AbilityManager abilityManager;

    // State
    ArmAbility lastAppliedArm;
    bool isGrounded;

    // Hashes
    static readonly int IsRunning = Animator.StringToHash("isRunning");
    static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    static readonly int IsFalling = Animator.StringToHash("isFalling");
    static readonly int AttackTrigger = Animator.StringToHash("Attack");

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

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

        // Force l'application de l'Override initial (main par défaut)
        lastAppliedArm = ArmAbility.Hand;
        ApplyOverrideForArm(ArmAbility.Hand);
    }

    // ════════════════════════════════════════════════════════════
    //  Update : suit l'état de l'AbilityManager
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        // Détection sol
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, groundLayer);

        // Paramètres d'animation
        animator.SetBool(IsRunning, isGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.1f);
        animator.SetBool(IsGrounded, isGrounded);
        animator.SetBool(IsFalling, !isGrounded && rb.linearVelocity.y < -0.1f);

        // Flip du sprite
        if (rb.linearVelocity.x > 0.1f)
            transform.localScale = new Vector3(1, 1, 1);
        else if (rb.linearVelocity.x < -0.1f)
            transform.localScale = new Vector3(-1, 1, 1);

        // Synchronisation avec AbilityManager
        if (abilityManager != null && abilityManager.CurrentArm != lastAppliedArm)
        {
            ApplyOverrideForArm(abilityManager.CurrentArm);
            lastAppliedArm = abilityManager.CurrentArm;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Application de l'Override Controller
    // ════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════
    //  API publique : appelée par SawAbility
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Déclenche l'animation d'attaque. Appelée par SawAbility quand elle attaque.
    /// Ne fait rien si le bras actif n'a pas d'animation d'attaque dans son Override.
    /// </summary>
    public void TriggerAttack()
    {
        animator.SetTrigger(AttackTrigger);
    }
}