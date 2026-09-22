using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class ArtefactRoulette : MonoBehaviour
{
    [Header("Refs AbilityManager")]
    [SerializeField] AbilityManager abilityManager;

    [Header("Composants de la roue")]
    [SerializeField] CanvasGroup rouletteCanvasGroup;
    [SerializeField] RectTransform rouletteRoot;
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
    [SerializeField] float slideInDuration = 0.2f;
    [SerializeField] float slideOutDuration = 0.2f;
    [SerializeField] Vector2 hiddenOffset = new Vector2(0f, -250f);

    [Header("Bounce de rotation (overshoot)")]
    [SerializeField] float rotationOvershoot = 25f;
    [SerializeField] float rotationDuration = 0.5f;
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
    [SerializeField] float pickupBounceScale = 1.15f;

    [Header("Audio")]
    [SerializeField] AudioMixerGroup sfxOutput;
    [SerializeField] AudioClip[] swooshInClips;
    [SerializeField] AudioClip[] swooshOutClips;
    [Range(0f, 1f)]
    [SerializeField] float swooshVolume = 0.8f;
    [SerializeField] AudioClip[] rouletteTickClips;
    [SerializeField] float tickStepDegrees = 12f;
    [Range(0f, 1f)]
    [SerializeField] float tickVolume = 0.6f;
    [SerializeField] Vector2 tickPitchRange = new Vector2(0.92f, 1.08f);
    [SerializeField] AudioClip[] pickupAppearClips;
    [Range(0f, 1f)]
    [SerializeField] float pickupAppearVolume = 0.8f;

    Coroutine showRoutine;
    Coroutine pickupRoutine;
    Vector2 rouletteShownPos;
    Vector2 pickupHomePos;
    AudioSource sfxSource;
    AudioClip lastSfxClip;

    void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.outputAudioMixerGroup = sfxOutput;

        if (rouletteCanvasGroup != null) rouletteCanvasGroup.alpha = 0f;
        if (pickupBounceCanvasGroup != null) pickupBounceCanvasGroup.alpha = 0f;

        if (pickupBounceAnchor != null)
            pickupHomePos = pickupBounceAnchor.anchoredPosition;

        if (rouletteRoot != null)
        {
            rouletteShownPos = rouletteRoot.anchoredPosition;
            rouletteRoot.anchoredPosition = rouletteShownPos + hiddenOffset;
        }
        else
        {
            Debug.LogError("ArtefactRoulette : rouletteRoot non assigné.", this);
        }
    }

    void OnEnable()
    {
        StartCoroutine(WaitForAbilityManager());
    }

    void OnDisable()
    {
        if (abilityManager != null)
        {
            abilityManager.OnArmChanged -= HandleArmChanged;
            abilityManager.OnArmUnlocked -= HandleArmUnlocked;
        }
    }

    IEnumerator WaitForAbilityManager()
    {
        float waited = 0f;
        while (abilityManager == null)
        {
            abilityManager = FindAnyObjectByType<AbilityManager>();
            if (abilityManager != null) break;

            waited += Time.unscaledDeltaTime;
            if (waited > 5f)
            {
                Debug.LogError("ArtefactRoulette : AbilityManager introuvable après 5s, roue désactivée.", this);
                yield break;
            }
            yield return null;
        }

        abilityManager.OnArmChanged += HandleArmChanged;
        abilityManager.OnArmUnlocked += HandleArmUnlocked;

        RefreshIconStates();

        if (backgroundRolebar != null)
        {
            float initialAngle = GetTargetAngleFor(abilityManager.CurrentArm);
            backgroundRolebar.localEulerAngles = new Vector3(0f, 0f, initialAngle);
            ApplyCounterRotationToIcons(initialAngle);
        }
    }

    void LateUpdate()
    {
        if (backgroundRolebar == null) return;

        float currentBackgroundAngle = backgroundRolebar.localEulerAngles.z;
        ApplyCounterRotationToIcons(currentBackgroundAngle);
    }

    void ApplyCounterRotationToIcons(float backgroundAngle)
    {
        float counter = -backgroundAngle;
        Vector3 counterRotation = new Vector3(0f, 0f, counter);

        if (handIcon != null) handIcon.rectTransform.localEulerAngles = counterRotation;
        if (sawIcon != null) sawIcon.rectTransform.localEulerAngles = counterRotation;
        if (grappleIcon != null) grappleIcon.rectTransform.localEulerAngles = counterRotation;
    }

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

    IEnumerator ShowRouletteRoutine(ArmAbility target)
    {
        yield return SlideRoulette(true);

        float targetAngle = GetTargetAngleFor(target);
        yield return RotateWithOvershoot(backgroundRolebar, targetAngle, rotationOvershoot, rotationDuration);

        float remainingTime = showDuration - slideInDuration - slideOutDuration - rotationDuration;
        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);

        yield return SlideRoulette(false);
    }

    IEnumerator SlideRoulette(bool show)
    {
        if (rouletteRoot == null || rouletteCanvasGroup == null) yield break;

        Vector2 hiddenPos = rouletteShownPos + hiddenOffset;
        Vector2 start = show ? hiddenPos : rouletteShownPos;
        Vector2 end = show ? rouletteShownPos : hiddenPos;
        float duration = show ? slideInDuration : slideOutDuration;

        if (show)
        {
            rouletteRoot.anchoredPosition = start;
            rouletteCanvasGroup.alpha = 1f;
            PlaySfx(swooshInClips, swooshVolume, 1f);
        }
        else
        {
            PlaySfx(swooshOutClips, swooshVolume, 1f);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rouletteRoot.anchoredPosition = Vector2.Lerp(start, end, p);
            yield return null;
        }
        rouletteRoot.anchoredPosition = end;

        if (!show)
            rouletteCanvasGroup.alpha = 0f;
    }

    IEnumerator RotateWithOvershoot(RectTransform rt, float targetAngle, float overshoot, float duration)
    {
        if (rt == null) yield break;

        float startAngle = rt.localEulerAngles.z;

        float delta = Mathf.DeltaAngle(startAngle, targetAngle);
        float effectiveTarget = startAngle + delta;

        float overshootDirection = Mathf.Sign(delta);
        if (overshootDirection == 0f) overshootDirection = 1f;
        float overshootAngle = effectiveTarget + overshoot * overshootDirection;

        float tickAccumulator = 0f;
        float previousAngle = startAngle;

        float overshootDuration = duration * overshootRatio;
        float t = 0f;
        while (t < overshootDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / overshootDuration);
            float angle = Mathf.Lerp(startAngle, overshootAngle, p);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);

            tickAccumulator += Mathf.Abs(angle - previousAngle);
            previousAngle = angle;
            if (tickAccumulator >= tickStepDegrees)
            {
                tickAccumulator %= tickStepDegrees;
                PlaySfx(rouletteTickClips, tickVolume, Random.Range(tickPitchRange.x, tickPitchRange.y));
            }

            yield return null;
        }

        float returnDuration = duration - overshootDuration;
        t = 0f;
        while (t < returnDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / returnDuration);
            float angle = Mathf.Lerp(overshootAngle, effectiveTarget, p);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);

            tickAccumulator += Mathf.Abs(angle - previousAngle);
            previousAngle = angle;
            if (tickAccumulator >= tickStepDegrees)
            {
                tickAccumulator %= tickStepDegrees;
                PlaySfx(rouletteTickClips, tickVolume, Random.Range(tickPitchRange.x, tickPitchRange.y));
            }

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

    IEnumerator PickupBounceRoutine(ArmAbility ability)
    {
        if (pickupIcon == null || pickupBounceAnchor == null) yield break;

        pickupIcon.sprite = GetSpriteFor(ability);
        if (pickupIcon.sprite == null) yield break;

        pickupBounceAnchor.anchoredPosition = pickupHomePos;
        pickupBounceAnchor.localScale = Vector3.zero;
        pickupBounceCanvasGroup.alpha = 0f;
        PlaySfx(pickupAppearClips, pickupAppearVolume, 1f);

        yield return ScaleAndFade(pickupBounceAnchor, pickupBounceCanvasGroup,
                                   Vector3.zero, Vector3.one, 0f, 1f, pickupAppearDuration);

        yield return ScaleTo(pickupBounceAnchor, Vector3.one * pickupBounceScale, pickupBounceDuration * 0.5f);
        yield return ScaleTo(pickupBounceAnchor, Vector3.one, pickupBounceDuration * 0.5f);

        yield return new WaitForSecondsRealtime(pickupPauseDuration);

        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRouletteRoutine(ability));

        yield return new WaitForSecondsRealtime(slideInDuration + rotationDuration);

        RectTransform targetPlacement = GetPlacementFor(ability);
        if (targetPlacement != null)
        {
            yield return FlyToTarget(pickupBounceAnchor, targetPlacement, pickupFlyDuration);
        }

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

    void PlaySfx(AudioClip[] clips, float volume, float pitch)
    {
        if (sfxSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastSfxClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastSfxClip = clip;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }
}