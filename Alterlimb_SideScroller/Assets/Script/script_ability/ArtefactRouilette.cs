using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la roue d'artefacts qui apparaît temporairement quand le joueur cycle
/// avec Q. Animation : flicker in → rotation overshoot vers l'angle cible →
/// flicker out. La roue n'est jamais affichée en permanence.
/// 
/// Architecture visuelle :
///   - Background tourne (avec ses placements enfants qui le suivent)
///   - Les icônes (enfants des placements) sont contre-tournées pour rester
///     droites visuellement, tout en suivant la position en arc de cercle
/// 
/// Au pickup d'un nouvel artefact : bounce au centre, puis la roue apparaît
/// pour montrer le nouvel artefact à sa position définitive.
/// 
/// Écoute AbilityManager via les événements OnArmChanged et OnArmUnlocked.
/// </summary>
public class ArtefactRoulette : MonoBehaviour
{
    [Header("Refs AbilityManager")]
    [SerializeField] AbilityManager abilityManager;

    [Header("Composants de la roue")]
    [SerializeField] CanvasGroup rouletteCanvasGroup;
    [SerializeField] RectTransform backgroundRolebar;

    [Header("Icônes par artefact")]
    [SerializeField] Image handIcon;
    [SerializeField] Image sawIcon;
    [SerializeField] Image grappleIcon;

    [Header("Placements (suivent la rotation du background)")]
    [SerializeField] RectTransform handPlacement;
    [SerializeField] RectTransform sawPlacement;
    [SerializeField] RectTransform grapplePlacement;

    [Header("Rotations cibles par artefact (en degrés)")]
    [SerializeField] float handTargetAngle = -50f;
    [SerializeField] float sawTargetAngle = 80f;
    [SerializeField] float grappleTargetAngle = 200f;

    [Header("Animation roue")]
    [SerializeField] float showDuration = 1.2f;
    [SerializeField] float flickerInDuration = 0.15f;
    [SerializeField] float flickerOutDuration = 0.15f;
    [SerializeField] int flickerSteps = 4;

    [Header("Bounce de rotation (overshoot)")]
    [Tooltip("Combien la roue dépasse sa cible avant de revenir, en degrés")]
    [SerializeField] float rotationOvershoot = 25f;
    [Tooltip("Durée totale du mouvement de rotation (overshoot + retour)")]
    [SerializeField] float rotationDuration = 0.5f;
    [Tooltip("Quelle portion de la durée est dédiée à l'overshoot")]
    [SerializeField, Range(0.3f, 0.8f)] float overshootRatio = 0.65f;

    [Header("Apparence des icônes")]
    [SerializeField, Range(0f, 1f)] float lockedAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] float unlockedAlpha = 1f;
    [SerializeField] Color selectedTint = Color.white;
    [SerializeField] Color unselectedTint = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Animation pickup (centre écran)")]
    [SerializeField] CanvasGroup pickupBounceCanvasGroup;
    [SerializeField] Image pickupIcon;
    [SerializeField] RectTransform pickupBounceAnchor;
    [SerializeField] Sprite handSprite;
    [SerializeField] Sprite sawSprite;
    [SerializeField] Sprite grappleSprite;
    [SerializeField] float pickupAppearDuration = 0.3f;
    [SerializeField] float pickupBounceDuration = 0.4f;
    [SerializeField] float pickupPauseDuration = 0.4f;
    [SerializeField] float pickupFlyDuration = 0.5f;
    [Tooltip("Intensité du bounce au pickup : 1.15 = subtil, 1.5 = exagéré")]
    [SerializeField] float pickupBounceScale = 1.15f;

    Coroutine showRoutine;
    Coroutine pickupRoutine;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindAnyObjectByType<AbilityManager>();

        if (rouletteCanvasGroup != null) rouletteCanvasGroup.alpha = 0f;
        if (pickupBounceCanvasGroup != null) pickupBounceCanvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        if (abilityManager != null)
        {
            abilityManager.OnArmChanged += HandleArmChanged;
            abilityManager.OnArmUnlocked += HandleArmUnlocked;
        }
    }

    void OnDisable()
    {
        if (abilityManager != null)
        {
            abilityManager.OnArmChanged -= HandleArmChanged;
            abilityManager.OnArmUnlocked -= HandleArmUnlocked;
        }
    }

    void Start()
    {
        RefreshIconStates();

        // Position initiale de la roue : angle de l'artefact actif au démarrage
        if (backgroundRolebar != null)
        {
            float initialAngle = GetTargetAngleFor(abilityManager.CurrentArm);
            backgroundRolebar.localEulerAngles = new Vector3(0f, 0f, initialAngle);
            ApplyCounterRotationToIcons(initialAngle);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Update : maintient les icônes droites pendant la rotation
    // ════════════════════════════════════════════════════════════

    void LateUpdate()
    {
        if (backgroundRolebar == null) return;

        // Contre-rotation des icônes pour qu'elles restent droites
        // même quand le background (et donc leurs placements) tournent.
        float currentBackgroundAngle = backgroundRolebar.localEulerAngles.z;
        ApplyCounterRotationToIcons(currentBackgroundAngle);
    }

    /// <summary>
    /// Applique une rotation inverse à chaque icône pour annuler visuellement
    /// la rotation du background. Les icônes suivent la position en arc de
    /// cercle mais restent toujours droites pour rester lisibles.
    /// </summary>
    void ApplyCounterRotationToIcons(float backgroundAngle)
    {
        float counter = -backgroundAngle;
        Vector3 counterRotation = new Vector3(0f, 0f, counter);

        if (handIcon != null) handIcon.rectTransform.localEulerAngles = counterRotation;
        if (sawIcon != null) sawIcon.rectTransform.localEulerAngles = counterRotation;
        if (grappleIcon != null) grappleIcon.rectTransform.localEulerAngles = counterRotation;
    }

    // ════════════════════════════════════════════════════════════
    //  Handlers d'événements
    // ════════════════════════════════════════════════════════════

    void HandleArmChanged(ArmAbility newArm)
    {
        RefreshIconStates();

        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRouletteRoutine(newArm));
    }

    void HandleArmUnlocked(ArmAbility unlockedArm)
    {
        RefreshIconStates();

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        pickupRoutine = StartCoroutine(PickupBounceRoutine(unlockedArm));
    }

    // ════════════════════════════════════════════════════════════
    //  Mise à jour visuelle des icônes (alpha + tint)
    // ════════════════════════════════════════════════════════════

    void RefreshIconStates()
    {
        ArmAbility current = abilityManager.CurrentArm;
        var unlocked = abilityManager.UnlockedArms;

        SetIconState(handIcon, ArmAbility.Hand, unlocked, current);
        SetIconState(sawIcon, ArmAbility.Saw, unlocked, current);
        SetIconState(grappleIcon, ArmAbility.Grapple, unlocked, current);
    }

    void SetIconState(Image icon, ArmAbility ability, IReadOnlyList<ArmAbility> unlocked, ArmAbility current)
    {
        if (icon == null) return;

        bool isUnlocked = unlocked.Contains(ability);
        bool isSelected = ability == current;

        Color c = isSelected ? selectedTint : unselectedTint;
        c.a = isUnlocked ? unlockedAlpha : lockedAlpha;
        icon.color = c;
    }

    // ════════════════════════════════════════════════════════════
    //  Animation roue : flicker in → rotation overshoot → flicker out
    // ════════════════════════════════════════════════════════════

    IEnumerator ShowRouletteRoutine(ArmAbility target)
    {
        // Flicker IN : toute la roue clignote en apparaissant
        yield return Flicker(rouletteCanvasGroup, 0f, 1f, flickerInDuration);

        // Rotation avec overshoot vers l'angle cible
        float targetAngle = GetTargetAngleFor(target);
        yield return RotateWithOvershoot(backgroundRolebar, targetAngle, rotationOvershoot, rotationDuration);

        // Pause de visibilité
        float remainingTime = showDuration - flickerInDuration - flickerOutDuration - rotationDuration;
        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);

        // Flicker OUT : toute la roue clignote en disparaissant
        yield return Flicker(rouletteCanvasGroup, 1f, 0f, flickerOutDuration);
    }

    IEnumerator Flicker(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float stepDuration = duration / flickerSteps;
        for (int i = 0; i < flickerSteps; i++)
        {
            cg.alpha = (i % 2 == 0) ? to : from;
            yield return new WaitForSecondsRealtime(stepDuration);
        }
        cg.alpha = to;
    }

    /// <summary>
    /// Tourne le RectTransform vers un angle cible avec un effet d'overshoot :
    /// dépasse de "overshoot" degrés dans le sens du mouvement, puis revient
    /// sur la cible.
    /// </summary>
    IEnumerator RotateWithOvershoot(RectTransform rt, float targetAngle, float overshoot, float duration)
    {
        if (rt == null) yield break;

        float startAngle = rt.localEulerAngles.z;

        // Normaliser pour prendre le chemin le plus court (évite de faire un tour complet)
        float delta = Mathf.DeltaAngle(startAngle, targetAngle);
        float effectiveTarget = startAngle + delta;

        // Direction du dépassement : dans le sens de la rotation
        float overshootDirection = Mathf.Sign(delta);
        if (overshootDirection == 0f) overshootDirection = 1f; // safety
        float overshootAngle = effectiveTarget + overshoot * overshootDirection;

        // Phase 1 : aller vers l'overshoot
        float overshootDuration = duration * overshootRatio;
        float t = 0f;
        while (t < overshootDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / overshootDuration);
            float angle = Mathf.Lerp(startAngle, overshootAngle, p);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        // Phase 2 : revenir sur la cible
        float returnDuration = duration - overshootDuration;
        t = 0f;
        while (t < returnDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / returnDuration);
            float angle = Mathf.Lerp(overshootAngle, effectiveTarget, p);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        rt.localEulerAngles = new Vector3(0f, 0f, effectiveTarget);
    }

    float GetTargetAngleFor(ArmAbility a)
    {
        switch (a)
        {
            case ArmAbility.Hand: return handTargetAngle;
            case ArmAbility.Saw: return sawTargetAngle;
            case ArmAbility.Grapple: return grappleTargetAngle;
            default: return 0f;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Animation pickup : centre écran → bounce léger → vol vers la roue
    // ════════════════════════════════════════════════════════════

    IEnumerator PickupBounceRoutine(ArmAbility ability)
    {
        if (pickupIcon == null || pickupBounceAnchor == null) yield break;

        pickupIcon.sprite = GetSpriteFor(ability);
        if (pickupIcon.sprite == null) yield break;

        pickupBounceAnchor.localScale = Vector3.zero;
        pickupBounceCanvasGroup.alpha = 0f;

        // 1. Apparition au centre écran
        yield return ScaleAndFade(pickupBounceAnchor, pickupBounceCanvasGroup,
                                   Vector3.zero, Vector3.one, 0f, 1f, pickupAppearDuration);

        // 2. Bounce subtil (1 rebond)
        yield return ScaleTo(pickupBounceAnchor, Vector3.one * pickupBounceScale, pickupBounceDuration * 0.5f);
        yield return ScaleTo(pickupBounceAnchor, Vector3.one, pickupBounceDuration * 0.5f);

        // 3. Pause d'admiration
        yield return new WaitForSecondsRealtime(pickupPauseDuration);

        // 4. Tourner la roue vers l'angle de l'artefact ramassé EN PARALLÈLE
        //    et faire apparaître la roue avec flicker pendant que le pickup
        //    vole vers sa position.
        //    On lance la roue qui se positionne sur le bon angle, et on attend
        //    qu'elle ait fini sa rotation pour faire voler l'icône au bon endroit.
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRouletteRoutine(ability));

        // 5. Attendre le flicker in + la rotation (pour que le placement soit à sa position finale)
        yield return new WaitForSecondsRealtime(flickerInDuration + rotationDuration);

        // 6. Vol vers la position de l'artefact (maintenant à sa position finale)
        RectTransform targetPlacement = GetPlacementFor(ability);
        if (targetPlacement != null)
        {
            yield return FlyToTarget(pickupBounceAnchor, targetPlacement, pickupFlyDuration);
        }

        // 7. Disparition du pickup
        pickupBounceCanvasGroup.alpha = 0f;
        pickupBounceAnchor.localScale = Vector3.one;
    }

    Sprite GetSpriteFor(ArmAbility a)
    {
        switch (a)
        {
            case ArmAbility.Hand: return handSprite;
            case ArmAbility.Saw: return sawSprite;
            case ArmAbility.Grapple: return grappleSprite;
            default: return null;
        }
    }

    RectTransform GetPlacementFor(ArmAbility a)
    {
        switch (a)
        {
            case ArmAbility.Hand: return handPlacement;
            case ArmAbility.Saw: return sawPlacement;
            case ArmAbility.Grapple: return grapplePlacement;
            default: return null;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Tweens utilitaires
    // ════════════════════════════════════════════════════════════

    IEnumerator ScaleTo(RectTransform rt, Vector3 target, float duration)
    {
        Vector3 start = rt.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rt.localScale = Vector3.Lerp(start, target, p);
            yield return null;
        }
        rt.localScale = target;
    }

    IEnumerator ScaleAndFade(RectTransform rt, CanvasGroup cg,
                              Vector3 scaleFrom, Vector3 scaleTo,
                              float alphaFrom, float alphaTo, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rt.localScale = Vector3.Lerp(scaleFrom, scaleTo, p);
            cg.alpha = Mathf.Lerp(alphaFrom, alphaTo, p);
            yield return null;
        }
        rt.localScale = scaleTo;
        cg.alpha = alphaTo;
    }

    IEnumerator FlyToTarget(RectTransform rt, RectTransform target, float duration)
    {
        Vector3 startPos = rt.position;
        Vector3 endPos = target.position;
        Vector3 startScale = rt.localScale;
        Vector3 endScale = target.localScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rt.position = Vector3.Lerp(startPos, endPos, p);
            rt.localScale = Vector3.Lerp(startScale, endScale, p);
            yield return null;
        }
        rt.position = endPos;
        rt.localScale = endScale;
    }
}