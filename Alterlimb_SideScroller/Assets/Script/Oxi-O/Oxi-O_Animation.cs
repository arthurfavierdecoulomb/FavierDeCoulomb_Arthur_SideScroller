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

    [Header("Dernier coup")]
    [SerializeField] string finalBlowState = "oxi_falling";
    [SerializeField] string ventShutdownState = "Tuyaux_vent_shutdown";

    [Header("Coup de main")]
    [SerializeField] string slapLeftState = "oxi_coup_gauche";
    [SerializeField] string slapRightState = "oxi_coup_droite";
    [SerializeField] string slapLeftEuphoriaState = "oxi_coup_gauche_euphorie";
    [SerializeField] string slapRightEuphoriaState = "oxi_coup_droit_euphorie";

    [Header("Durée de la transformation")]
    [SerializeField] float transformationDuration = 0f;

    [Header("Clignement")]
    [SerializeField] bool autoBlink = true;
    [SerializeField] float blinkDelayMin = 2.5f;
    [SerializeField] float blinkDelayMax = 6f;

    public event System.Action OnTransformationStarted;
    public event System.Action OnTransformationComplete;
    public event System.Action OnSlicedComplete;
    public event System.Action OnFinalFall;
    public event System.Action OnSlapImpact;

    public bool IsEuphoric => isEuphoric;
    public bool IsTalking => isTalking;
    public bool IsEconomyMode => isEconomyMode;
    public bool IsSlicing => isSlicing;
    public bool IsBusyWithCombatAnimation => isEconomyMode || isSlicing;
    public bool IsFinalBlow => isFinalBlow;
    public bool HasFallen => hasFallen;
    public bool IsSlapping => isSlapping;

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
    int finalBlowHash;
    int ventShutdownHash;
    int slapLeftHash;
    int slapRightHash;
    int slapLeftEuphoriaHash;
    int slapRightEuphoriaHash;

    bool isTalking;
    bool isEuphoric;
    bool isBlinking;
    bool isTransforming;
    bool isEconomyMode;
    bool isSlicing;
    bool isFinalBlow;
    bool hasFallen;
    bool ventShutDown;
    bool isSlapping;

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
        finalBlowHash = Animator.StringToHash(finalBlowState);
        ventShutdownHash = Animator.StringToHash(ventShutdownState);
        slapLeftHash = Animator.StringToHash(slapLeftState);
        slapRightHash = Animator.StringToHash(slapRightState);
        slapLeftEuphoriaHash = Animator.StringToHash(slapLeftEuphoriaState);
        slapRightEuphoriaHash = Animator.StringToHash(slapRightEuphoriaState);

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
        CheckState(oxiAnimator, finalBlowHash, finalBlowState);
        CheckState(ventAnimator, ventShutdownHash, ventShutdownState);

        if (!string.IsNullOrEmpty(slapLeftState))
            CheckState(oxiAnimator, slapLeftHash, slapLeftState);

        if (!string.IsNullOrEmpty(slapRightState))
            CheckState(oxiAnimator, slapRightHash, slapRightState);

        if (!string.IsNullOrEmpty(slapLeftEuphoriaState))
            CheckState(oxiAnimator, slapLeftEuphoriaHash, slapLeftEuphoriaState);

        if (!string.IsNullOrEmpty(slapRightEuphoriaState))
            CheckState(oxiAnimator, slapRightEuphoriaHash, slapRightEuphoriaState);
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
        if (isTransforming || isFinalBlow) return;
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

        if (!isBlinking && !isTransforming && !isEconomyMode && !isSlicing && !isFinalBlow)
            PlayPreservingCycle(idleHash);
    }

    public void EnterEconomyMode()
    {
        if (isSlicing || isFinalBlow || oxiAnimator == null) return;

        isEconomyMode = true;
        isTalking = false;
        isBlinking = false;

        oxiAnimator.Play(isEuphoric ? economyModeEuphoriaHash : economyModeHash, 0, 0f);
    }

    public void ExitEconomyMode()
    {
        if (!isEconomyMode) return;

        isEconomyMode = false;

        if (!isSlicing && !isTransforming && !isFinalBlow && oxiAnimator != null)
            oxiAnimator.Play(RestHash(), 0, 0f);
    }

    public void PlaySliced(int phaseIndex)
    {
        if (isSlicing || isFinalBlow || oxiAnimator == null) return;

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

        if (returnToIdleAfterSliced && !isTransforming && !isFinalBlow)
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
        isFinalBlow = false;
        hasFallen = false;
        ventShutDown = false;

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

    public void PlaySlap(bool leftHand)
    {
        if (oxiAnimator == null || isSlicing || isFinalBlow || isTransforming) return;

        string euphoriaState = leftHand ? slapLeftEuphoriaState : slapRightEuphoriaState;
        bool useEuphoria = isEuphoric && !string.IsNullOrEmpty(euphoriaState);

        string state = useEuphoria ? euphoriaState : (leftHand ? slapLeftState : slapRightState);

        if (string.IsNullOrEmpty(state))
        {
            Debug.LogWarning($"[OxiOAnimation] Aucun état de coup de main {(leftHand ? "gauche" : "droite")} renseigné, l'animation est sautée.", this);
            return;
        }

        int hash = useEuphoria
            ? (leftHand ? slapLeftEuphoriaHash : slapRightEuphoriaHash)
            : (leftHand ? slapLeftHash : slapRightHash);

        Debug.Log($"[OxiOAnimation] Coup de main : '{state}'.", this);

        StartCoroutine(SlapRoutine(hash));
    }

    IEnumerator SlapRoutine(int stateHash)
    {
        isSlapping = true;
        isSlicing = true;
        isEconomyMode = false;
        isTalking = false;
        isBlinking = false;

        oxiAnimator.Play(stateHash, 0, 0f);

        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

        if (!isTransforming && !isFinalBlow)
            oxiAnimator.Play(RestHash(), 0, 0f);

        isSlicing = false;
        isSlapping = false;
    }

    public void NotifySlapImpact()
    {
        if (!isSlapping) return;

        OnSlapImpact?.Invoke();
    }

    public void PlayFinalBlow()
    {
        if (oxiAnimator == null || isFinalBlow) return;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        isFinalBlow = true;
        isSlicing = false;
        isEconomyMode = false;
        isTalking = false;
        isBlinking = false;

        oxiAnimator.speed = 1f;
        oxiAnimator.Play(finalBlowHash, 0, 0f);

        Debug.Log($"[OxiOAnimation] Dernier coup : '{finalBlowState}' lancé.", this);
    }

    public void HandleCoreExplosion()
    {
        if (isFinalBlow)
            ShutDownVents();
    }

    public void TriggerFinalFall()
    {
        if (hasFallen) return;

        hasFallen = true;
        ShutDownVents();

        Debug.Log("[OxiOAnimation] Chute d'Oxi-O déclenchée.", this);
        OnFinalFall?.Invoke();
    }

    void ShutDownVents()
    {
        if (ventShutDown || ventAnimator == null) return;

        ventShutDown = true;
        ventAnimator.Play(ventShutdownHash, 0, 0f);

        Debug.Log($"[OxiOAnimation] Ventilateurs : '{ventShutdownState}' lancé.", this);
    }

    public void TransformToEuphoria()
    {
        if (isEuphoric || isTransforming) return;
        StartCoroutine(TransformationRoutine());
    }

    IEnumerator TransformationRoutine()
    {
        isTransforming = true;
        OnTransformationStarted?.Invoke();

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
            if (isEconomyMode || isSlicing || isFinalBlow) continue;

            yield return StartCoroutine(BlinkRoutine());
        }
    }

    IEnumerator BlinkRoutine()
    {
        isBlinking = true;

        yield return WaitForCycleStart();

        if (isEuphoric || isTransforming || isEconomyMode || isSlicing || isFinalBlow)
        {
            isBlinking = false;
            yield break;
        }

        oxiAnimator.Play(isTalking ? talkBlinkHash : idleBlinkHash, 0, 0f);

        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

        if (!isEuphoric && !isTransforming && !isEconomyMode && !isSlicing && !isFinalBlow)
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