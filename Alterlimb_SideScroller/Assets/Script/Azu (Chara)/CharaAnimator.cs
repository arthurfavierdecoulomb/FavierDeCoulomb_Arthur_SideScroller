using UnityEngine;

/// <summary>
/// Gère les animations du joueur en fonction du bras actif.
/// 
/// Le graphe d'animation (transitions Idle/Course/Saut/Attack/WallGrip) est
/// partagé entre tous les bras via un Animator Controller de base. Chaque bras
/// a son Animator Override Controller qui remplace les clips visuels.
/// 
/// Le bras actif est piloté par AbilityManager (touche Q pour cycler).
/// PlayerAnimator se contente de suivre l'état d'AbilityManager.
/// 
/// L'attaque visuelle est déclenchée par SawAbility via TriggerAttack().
/// 
/// Wall grip : le script détecte via raycast horizontal si le joueur est
/// collé à un mur grippable (Layer dédié) tout en étant en l'air, et met
/// à jour le paramètre Animator "isWallGripping". Une seule animation couvre
/// les deux cas : glisser le long du mur ET se bloquer volontairement dessus.
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

    [Header("Wall Grip Check")]
    [Tooltip("Layer des murs grippables (GroundWallGrip)")]
    [SerializeField] LayerMask wallGripLayer;
    [Tooltip("Distance du raycast horizontal qui détecte le mur grippable")]
    [SerializeField] float wallCheckDistance = 0.6f;
    [Tooltip("Décalages verticaux des raycasts (plusieurs rayons pour couvrir la hauteur du joueur)")]
    [SerializeField] float[] wallCheckVerticalOffsets = { -0.4f, 0f, 0.4f };

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    // Refs
    Animator animator;
    Rigidbody2D rb;
    AbilityManager abilityManager;

    // State
    ArmAbility lastAppliedArm;
    bool isGrounded;
    bool isWallGripping;

    // Hashes
    static readonly int IsRunning = Animator.StringToHash("isRunning");
    static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    static readonly int IsFalling = Animator.StringToHash("isFalling");
    static readonly int AttackTrigger = Animator.StringToHash("Attack");
    static readonly int IsWallGripping = Animator.StringToHash("isWallGripping");

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

        // Détection wall grip
        isWallGripping = CheckWallGrip();

        // Paramètres d'animation
        animator.SetBool(IsRunning, isGrounded && Mathf.Abs(rb.linearVelocity.x) > 0.1f);
        animator.SetBool(IsGrounded, isGrounded);
        animator.SetBool(IsFalling, !isGrounded && rb.linearVelocity.y < -0.1f);
        animator.SetBool(IsWallGripping, isWallGripping);

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
    //  Détection du wall grip
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Détermine si le joueur est en wall grip : un mur grippable est détecté
    /// à gauche OU à droite (raycasts horizontaux), et le joueur n'est pas au sol.
    /// Couvre les deux mécaniques : glisser le long du mur et s'y bloquer.
    /// </summary>
    bool CheckWallGrip()
    {
        // Au sol → pas de wall grip possible
        if (isGrounded) return false;

        // On lance plusieurs rayons sur la hauteur du joueur, vers la gauche
        // ET vers la droite. Si l'un d'eux touche un mur grippable → on grippe.
        bool wallDetected = RaycastWallSide(Vector2.right) || RaycastWallSide(Vector2.left);

        return wallDetected;
    }

    /// <summary>
    /// Lance plusieurs raycasts horizontaux dans une direction donnée
    /// (à différentes hauteurs) et retourne true si un mur grippable est touché.
    /// </summary>
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