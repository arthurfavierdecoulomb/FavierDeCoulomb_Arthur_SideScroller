using UnityEngine;

/// <summary>
/// Bras du grappin : un GameObject séparé qui rotationne vers la souris,
/// gère sa visibilité, et pilote son Animator (les 5 animations du bras).
/// 
/// VISIBILITÉ : visible dès que le grappin est l'artefact équipé
/// (AbilityManager.CurrentArm == Grapple).
/// 
/// VISÉE : quand visible, le bras vise la souris pendant qu'on utilise le
/// grappin. Au repos, il revient à une pose neutre. Le flip est compensé.
/// 
/// ANIMATIONS : le bras lit l'état du GrapplingHook et pilote son Animator :
///   - Charge (Trigger)  : au tir (Idle → Deploying)
///   - Hooked (Bool)     : vrai pendant l'état Hooked
///   - Scrolling (Int)   : 1 = dur, -1 = moux, 0 = rien (scroll molette)
///   - Release (Trigger) : au relâché du grappin (retour à Idle)
/// 
/// Setup :
///   - GameObject "BrasGrappin" enfant du joueur, pivot à l'épaule.
///   - Un SpriteRenderer + un Animator (avec les 7 états du bras).
///   - Ce script attaché dessus.
/// </summary>
public class GrappleArm : MonoBehaviour
{
    [Header("Rotation neutre (quand on ne vise pas)")]
    [Tooltip("Angle Z du bras au repos (grappin équipé mais pas de visée)")]
    [SerializeField] float restAngle = 0f;
    [Tooltip("Vitesse de retour à la position neutre")]
    [SerializeField] float returnSpeed = 12f;

    [Header("Références")]
    [Tooltip("L'AbilityManager du joueur (pour savoir quel artefact est équipé)")]
    [SerializeField] AbilityManager abilityManager;
    [Tooltip("Le GrapplingHook du joueur (source de l'état du grappin)")]
    [SerializeField] GrapplingHook grapplingHook;
    [Tooltip("Le Transform du corps du joueur, pour détecter le sens du flip (scale X)")]
    [SerializeField] Transform playerBody;
    [Tooltip("L'Animator du bras grappin (les 5 animations)")]
    [SerializeField] Animator armAnimator;

    Camera cam;
    SpriteRenderer spriteRenderer;
    bool isVisible;

    // Mémorise l'état du grappin à la frame précédente, pour détecter les transitions
    GrapplingHook.GrappleState previousState = GrapplingHook.GrappleState.Idle;

    // ── Hash des paramètres Animator ──
    static readonly int ChargeTrigger = Animator.StringToHash("Charge");
    static readonly int HookedBool = Animator.StringToHash("Hooked");
    static readonly int ScrollingInt = Animator.StringToHash("Scrolling");
    static readonly int ReleaseTrigger = Animator.StringToHash("Release");

    void Awake()
    {
        cam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (abilityManager == null)
            abilityManager = GetComponentInParent<AbilityManager>();
        if (grapplingHook == null)
            grapplingHook = GetComponentInParent<GrapplingHook>();
        if (armAnimator == null)
            armAnimator = GetComponent<Animator>();

        SetVisible(false);
    }

    void Update()
    {
        // ── Visibilité : visible si le grappin est l'artefact équipé ──
        bool grappleEquipped = (abilityManager != null)
                            && abilityManager.CurrentArm == ArmAbility.Grapple;

        if (grappleEquipped != isVisible)
            SetVisible(grappleEquipped);

        if (!isVisible) return;

        // ── Rotation ──
        bool aiming = (grapplingHook != null) && grapplingHook.isUsingGrapple;
        if (aiming)
            AimAtMouse();
        else
            ReturnToRest();

        // ── Animations ──
        UpdateArmAnimations();
    }

    // ════════════════════════════════════════════════════════════
    //  Pilotage de l'Animator du bras
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Lit l'état du GrapplingHook et pilote l'Animator du bras.
    /// Détecte les TRANSITIONS d'état pour déclencher les triggers au bon moment.
    /// </summary>
    void UpdateArmAnimations()
    {
        if (grapplingHook == null || armAnimator == null) return;

        GrapplingHook.GrappleState currentState = grapplingHook.State;

        // ── Détection des transitions d'état ──
        if (currentState != previousState)
        {
            // Idle → Deploying : on vient de tirer → animation charge→tire
            if (previousState == GrapplingHook.GrappleState.Idle
                && currentState == GrapplingHook.GrappleState.Deploying)
            {
                armAnimator.SetTrigger(ChargeTrigger);
            }

            // N'importe quel état → Idle : on vient de relâcher → animation reprise
            if (currentState == GrapplingHook.GrappleState.Idle
                && previousState != GrapplingHook.GrappleState.Idle)
            {
                armAnimator.SetTrigger(ReleaseTrigger);
            }

            previousState = currentState;
        }

        // ── Paramètres continus ──

        // Hooked : vrai pendant tout l'état Hooked
        armAnimator.SetBool(HookedBool, currentState == GrapplingHook.GrappleState.Hooked);

        // Scrolling : direction du scroll (1 = dur, -1 = moux, 0 = rien)
        // On ne scrolle que quand on est accroché
        int scrolling = (currentState == GrapplingHook.GrappleState.Hooked)
                       ? grapplingHook.ScrollDirection
                       : 0;
        armAnimator.SetInteger(ScrollingInt, scrolling);
    }

    // ════════════════════════════════════════════════════════════
    //  Visibilité
    // ════════════════════════════════════════════════════════════

    void SetVisible(bool visible)
    {
        isVisible = visible;

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;

        if (visible)
            transform.localRotation = Quaternion.Euler(0f, 0f, restAngle);
    }

    // ════════════════════════════════════════════════════════════
    //  Rotation
    // ════════════════════════════════════════════════════════════

    bool IsPlayerFlipped()
    {
        if (playerBody != null)
            return playerBody.localScale.x < 0f;

        if (transform.parent != null)
            return transform.parent.localScale.x < 0f;

        return false;
    }

    void AimAtMouse()
    {
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mouseWorld - (Vector2)transform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (IsPlayerFlipped())
            angle = 180f - angle;

        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void ReturnToRest()
    {
        Quaternion targetRot = Quaternion.Euler(0f, 0f, restAngle);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot,
                                                  returnSpeed * Time.deltaTime);
    }
}