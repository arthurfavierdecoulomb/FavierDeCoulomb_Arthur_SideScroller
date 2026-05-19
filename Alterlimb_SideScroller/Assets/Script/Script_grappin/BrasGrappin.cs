using UnityEngine;

/// <summary>
/// Bras du grappin : un GameObject séparé qui rotationne vers la souris.
/// 
/// VISIBILITÉ : le bras est visible dès que le grappin est l'artefact équipé
/// (AbilityManager.CurrentArm == Grapple). Quand un autre artefact est équipé,
/// le bras est masqué (c'est l'animation normale d'Azu qui prend le relais).
/// 
/// VISÉE : quand il est visible, le bras vise la souris pendant qu'on utilise
/// le grappin (clic). Au repos (pas de clic), il revient à une pose neutre.
/// Le flip du joueur (scale X négatif) est compensé.
/// 
/// Ce script gère la rotation et la visibilité. Les animations du bras
/// (charge, tire, etc.) seront branchées à l'étape suivante.
/// 
/// Setup :
///   - GameObject "BrasGrappin" enfant du joueur, pivot à l'épaule.
///   - Un SpriteRenderer (le visuel du bras).
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
    [Tooltip("Le GrapplingHook du joueur (pour savoir si on est en train de viser)")]
    [SerializeField] GrapplingHook grapplingHook;
    [Tooltip("Le Transform du corps du joueur, pour détecter le sens du flip (scale X)")]
    [SerializeField] Transform playerBody;

    Camera cam;
    SpriteRenderer spriteRenderer;
    bool isVisible;

    void Awake()
    {
        cam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (abilityManager == null)
            abilityManager = GetComponentInParent<AbilityManager>();
        if (grapplingHook == null)
            grapplingHook = GetComponentInParent<GrapplingHook>();

        SetVisible(false);
    }

    void Update()
    {
        // ── Visibilité : le bras est visible si le grappin est l'artefact équipé ──
        bool grappleEquipped = (abilityManager != null)
                            && abilityManager.CurrentArm == ArmAbility.Grapple;

        if (grappleEquipped != isVisible)
            SetVisible(grappleEquipped);

        if (!isVisible) return;

        // ── Visée : le bras vise la souris quand on utilise le grappin ──
        bool aiming = (grapplingHook != null) && grapplingHook.isUsingGrapple;

        if (aiming)
            AimAtMouse();
        else
            ReturnToRest();
    }

    // ════════════════════════════════════════════════════════════
    //  Visibilité
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Affiche ou masque le bras. On désactive le SpriteRenderer (pas le
    /// GameObject) pour que ce script continue de tourner.
    /// </summary>
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

    /// <summary>
    /// Vrai si le joueur est retourné (regarde à gauche).
    /// On se base sur le scale X du corps : négatif = flippé.
    /// </summary>
    bool IsPlayerFlipped()
    {
        if (playerBody != null)
            return playerBody.localScale.x < 0f;

        if (transform.parent != null)
            return transform.parent.localScale.x < 0f;

        return false;
    }

    /// <summary>
    /// Oriente le bras vers la position de la souris.
    /// Compense le flip du joueur pour que la visée reste correcte des deux côtés.
    /// </summary>
    void AimAtMouse()
    {
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mouseWorld - (Vector2)transform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (IsPlayerFlipped())
            angle = 180f - angle;

        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>Ramène doucement le bras à sa rotation neutre.</summary>
    void ReturnToRest()
    {
        Quaternion targetRot = Quaternion.Euler(0f, 0f, restAngle);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot,
                                                  returnSpeed * Time.deltaTime);
    }
}