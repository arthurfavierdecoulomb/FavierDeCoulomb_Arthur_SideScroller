using UnityEngine;
using System.Collections;

public class OxiO_Animation : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] Animator oxiAnimator;
    [SerializeField] Animator ventAnimator;

    [Header("États Oxi-O")]
    [SerializeField] string idleState = "Oxi_idle_sans_blink";
    [SerializeField] string idleBlinkState = "Oxi_idle_avec_blink";
    [SerializeField] string talkState = "Oxi_dialogue_sans_blink";
    [SerializeField] string talkBlinkState = "Oxi_dialogue_avec_blink";
    [SerializeField] string transformationState = "Oxi_euphorie_transformation";
    [SerializeField] string euphoriaIdleState = "Oxi_euphorie_idle";

    [Header("États combat")]
    [SerializeField] string economyModeState = "mode_economie";
    [SerializeField] string economyModeEuphoriaState = "";
    [SerializeField] string[] slicedStates = { "phase_1_sliced", "phase_2_sliced" };
    [SerializeField] bool returnToIdleAfterSliced = true;

    [Header("États tuyaux")]
    [SerializeField] string ventIdleState = "Tuyaux_vent_idle";
    [SerializeField] string ventErrorBoostState = "Tuyaux_vent_erreur_puis_boost";
    [SerializeField] string ventBoostState = "Tuyaux_vent_boost";

    [Header("Durée de la transformation")]
    [SerializeField] float transformationDuration = 0f;

    [Header("Clignement")]
    [SerializeField] bool autoBlink = true;
    [SerializeField] float blinkDelayMin = 2.5f;
    [SerializeField] float blinkDelayMax = 6f;

    public event System.Action OnTransformationComplete;
    public event System.Action OnSlicedComplete;

    public bool IsEuphoric => isEuphoric;
    public bool IsTalking => isTalking;
    public bool IsEconomyMode => isEconomyMode;
    public bool IsSlicing => isSlicing;
    public bool IsBusyWithCombatAnimation => isEconomyMode || isSlicing;

    int idleHash;
    int idleBlinkHash;
    int talkHash;
    int talkBlinkHash;
    int transformationHash;
    int euphoriaIdleHash;
    int economyModeHash;
    int economyModeEuphoriaHash;
    int[] slicedHashes;
    int ventIdleHash;
    int ventErrorBoostHash;
    int ventBoostHash;

    bool isTalking;
    bool isEuphoric;
    bool isBlinking;
    bool isTransforming;
    bool isEconomyMode;
    bool isSlicing;

    Coroutine blinkRoutine;

    void Awake()
    {
        idleHash = Animator.StringToHash(idleState);
        idleBlinkHash = Animator.StringToHash(idleBlinkState);
        talkHash = Animator.StringToHash(talkState);
        talkBlinkHash = Animator.StringToHash(talkBlinkState);
        transformationHash = Animator.StringToHash(transformationState);
        euphoriaIdleHash = Animator.StringToHash(euphoriaIdleState);
        economyModeHash = Animator.StringToHash(economyModeState);
        economyModeEuphoriaHash = string.IsNullOrEmpty(economyModeEuphoriaState)
            ? economyModeHash
            : Animator.StringToHash(economyModeEuphoriaState);

        slicedHashes = new int[slicedStates.Length];
        for (int i = 0; i < slicedStates.Length; i++)
            slicedHashes[i] = Animator.StringToHash(slicedStates[i]);

        ventIdleHash = Animator.StringToHash(ventIdleState);
        ventErrorBoostHash = Animator.StringToHash(ventErrorBoostState);
        ventBoostHash = Animator.StringToHash(ventBoostState);

        ValidateStates();
    }

    void ValidateStates()
    {
        CheckState(oxiAnimator, idleHash, idleState);
        CheckState(oxiAnimator, idleBlinkHash, idleBlinkState);
        CheckState(oxiAnimator, talkHash, talkState);
        CheckState(oxiAnimator, talkBlinkHash, talkBlinkState);
        CheckState(oxiAnimator, transformationHash, transformationState);
        CheckState(oxiAnimator, euphoriaIdleHash, euphoriaIdleState);
        CheckState(oxiAnimator, economyModeHash, economyModeState);

        if (!string.IsNullOrEmpty(economyModeEuphoriaState))
            CheckState(oxiAnimator, economyModeEuphoriaHash, economyModeEuphoriaState);

        for (int i = 0; i < slicedHashes.Length; i++)
            CheckState(oxiAnimator, slicedHashes[i], slicedStates[i]);

        CheckState(ventAnimator, ventIdleHash, ventIdleState);
        CheckState(ventAnimator, ventErrorBoostHash, ventErrorBoostState);
        CheckState(ventAnimator, ventBoostHash, ventBoostState);
    }

    void CheckState(Animator animator, int hash, string stateName)
    {
        if (animator == null)
        {
            Debug.LogError($"[OxiOAnimationDriver] Animator non assigné pour l'état '{stateName}' troue du cul vas...");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[OxiOAnimationDriver] ça merde grave à Animator Controller sur {animator.name}.");
            return;
        }

        if (animator.HasState(0, hash)) return;

        Debug.LogError($"[OxiOAnimationDriver] ça merde au niveau de '{stateName}' il est introuvable dans l'Animator de {animator.name}");
    }

    void Start()
    {
        if (oxiAnimator != null) oxiAnimator.Play(idleHash, 0, 0f);
        if (ventAnimator != null) ventAnimator.Play(ventIdleHash, 0, 0f);

        if (autoBlink)
            blinkRoutine = StartCoroutine(AutoBlinkRoutine());
    }

    public void StartTalking()
    {
        if (isTransforming) return;
        if (isEconomyMode || isSlicing) return;
        if (isTalking) return;

        isTalking = true;

        if (isEuphoric) return;

        if (!isBlinking) PlayPreservingCycle(talkHash);
    }

    public void StopTalking()
    {
        if (!isTalking) return;

        isTalking = false;

        if (isEuphoric) return;

        if (!isBlinking && !isTransforming && !isEconomyMode && !isSlicing)
            PlayPreservingCycle(idleHash);
    }

    public void EnterEconomyMode()
    {
        if (isSlicing || oxiAnimator == null) return;

        isEconomyMode = true;
        isTalking = false;
        isBlinking = false;

        oxiAnimator.Play(isEuphoric ? economyModeEuphoriaHash : economyModeHash, 0, 0f);
    }

    public void ExitEconomyMode()
    {
        if (!isEconomyMode) return;

        isEconomyMode = false;

        if (!isSlicing && !isTransforming && oxiAnimator != null)
            oxiAnimator.Play(RestHash(), 0, 0f);
    }

    public void PlaySliced(int phaseIndex)
    {
        if (isSlicing || oxiAnimator == null) return;

        StartCoroutine(SlicedRoutine(phaseIndex));
    }

    IEnumerator SlicedRoutine(int phaseIndex)
    {
        isSlicing = true;
        isEconomyMode = false;
        isTalking = false;
        isBlinking = false;

        int index = Mathf.Clamp(phaseIndex - 1, 0, slicedHashes.Length - 1);
        oxiAnimator.Play(slicedHashes[index], 0, 0f);

        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

        if (returnToIdleAfterSliced && !isTransforming)
            oxiAnimator.Play(RestHash(), 0, 0f);

        isSlicing = false;
        OnSlicedComplete?.Invoke();
    }

    public float GetSlicedDuration(int phaseIndex)
    {
        if (oxiAnimator == null || oxiAnimator.runtimeAnimatorController == null)
            return 0f;

        int index = Mathf.Clamp(phaseIndex - 1, 0, slicedStates.Length - 1);
        return GetClipLength(slicedStates[index]);
    }

    public float GetTransformationDuration()
    {
        if (transformationDuration > 0f)
            return transformationDuration;

        return GetClipLength(transformationState);
    }

    float GetClipLength(string stateName)
    {
        if (oxiAnimator == null || oxiAnimator.runtimeAnimatorController == null)
            return 0f;

        foreach (AnimationClip clip in oxiAnimator.runtimeAnimatorController.animationClips)
            if (clip.name == stateName)
                return clip.length;

        return 0f;
    }

    public void ResetToNormal()
    {
        StopAllCoroutines();

        isEuphoric = false;
        isTransforming = false;
        isEconomyMode = false;
        isSlicing = false;
        isTalking = false;
        isBlinking = false;

        if (oxiAnimator != null)
        {
            oxiAnimator.speed = 1f;
            oxiAnimator.Play(idleHash, 0, 0f);
        }

        if (ventAnimator != null)
            ventAnimator.Play(ventIdleHash, 0, 0f);

        if (autoBlink)
            blinkRoutine = StartCoroutine(AutoBlinkRoutine());
    }

    public void TransformToEuphoria()
    {
        if (isEuphoric || isTransforming) return;
        StartCoroutine(TransformationRoutine());
    }

    IEnumerator TransformationRoutine()
    {
        isTransforming = true;
        isTalking = false;
        isBlinking = false;
        isEconomyMode = false;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (ventAnimator != null)
            StartCoroutine(VentBoostRoutine());

        float clipLength = GetClipLength(transformationState);

        if (transformationDuration > 0f && clipLength > 0f)
            oxiAnimator.speed = clipLength / transformationDuration;

        oxiAnimator.Play(transformationHash, 0, 0f);

        yield return null;
        yield return WaitForState(oxiAnimator, transformationHash, transformationState);

        oxiAnimator.speed = 1f;
        oxiAnimator.Play(euphoriaIdleHash, 0, 0f);

        isEuphoric = true;
        isTransforming = false;
        OnTransformationComplete?.Invoke();
    }

    IEnumerator VentBoostRoutine()
    {
        ventAnimator.Play(ventErrorBoostHash, 0, 0f);
        yield return null;
        yield return WaitForStateEnd(ventAnimator);
        ventAnimator.Play(ventBoostHash, 0, 0f);
    }

    IEnumerator AutoBlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkDelayMin, blinkDelayMax));

            if (isEuphoric || isTransforming || isBlinking) continue;
            if (isEconomyMode || isSlicing) continue;

            yield return StartCoroutine(BlinkRoutine());
        }
    }

    IEnumerator BlinkRoutine()
    {
        isBlinking = true;

        yield return WaitForCycleStart();

        if (isEuphoric || isTransforming || isEconomyMode || isSlicing)
        {
            isBlinking = false;
            yield break;
        }

        oxiAnimator.Play(isTalking ? talkBlinkHash : idleBlinkHash, 0, 0f);

        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

        if (!isEuphoric && !isTransforming && !isEconomyMode && !isSlicing)
            oxiAnimator.Play(isTalking ? talkHash : idleHash, 0, 0f);

        isBlinking = false;
    }

    int RestHash()
    {
        return isEuphoric ? euphoriaIdleHash : idleHash;
    }

    void PlayPreservingCycle(int stateHash)
    {
        if (oxiAnimator == null) return;
        oxiAnimator.Play(stateHash, 0, CurrentCycle(oxiAnimator));
    }

    float CurrentCycle(Animator animator)
    {
        return animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
    }

    IEnumerator WaitForCycleStart()
    {
        float previous = CurrentCycle(oxiAnimator);

        while (true)
        {
            yield return null;
            float current = CurrentCycle(oxiAnimator);
            if (current < previous) yield break;
            previous = current;
        }
    }

    IEnumerator WaitForStateEnd(Animator animator)
    {
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
    }

    IEnumerator WaitForState(Animator animator, int hash, string stateName)
    {
        float guard = 0f;

        while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash && guard < 2f)
        {
            guard += Time.deltaTime;
            yield return null;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash)
        {
            Debug.LogError($"[OxiOAnimation] L'Animator n'est jamais entré dans l'état '{stateName}'. Une transition automatique le renvoie ailleurs ?");
            yield break;
        }

        while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash
            && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
    }
}