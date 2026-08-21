using UnityEngine;
using System.Collections;

public class OxiOAnimationDriver : MonoBehaviour
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

    [Header("États tuyaux")]
    [SerializeField] string ventIdleState = "Tuyaux_vent_idle";
    [SerializeField] string ventErrorBoostState = "Tuyaux_vent_erreur_puis_boost";
    [SerializeField] string ventBoostState = "Tuyaux_vent_boost";

    [Header("Clignement")]
    [SerializeField] bool autoBlink = true;
    [SerializeField] float blinkDelayMin = 2.5f;
    [SerializeField] float blinkDelayMax = 6f;

    public event System.Action OnTransformationComplete;

    public bool IsEuphoric => isEuphoric;
    public bool IsTalking => isTalking;

    int idleHash;
    int idleBlinkHash;
    int talkHash;
    int talkBlinkHash;
    int transformationHash;
    int euphoriaIdleHash;

    int ventIdleHash;
    int ventErrorBoostHash;
    int ventBoostHash;

    bool isTalking;
    bool isEuphoric;
    bool isBlinking;
    bool isTransforming;

    Coroutine blinkRoutine;

    void Awake()
    {
        idleHash = Animator.StringToHash(idleState);
        idleBlinkHash = Animator.StringToHash(idleBlinkState);
        talkHash = Animator.StringToHash(talkState);
        talkBlinkHash = Animator.StringToHash(talkBlinkState);
        transformationHash = Animator.StringToHash(transformationState);
        euphoriaIdleHash = Animator.StringToHash(euphoriaIdleState);

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

        CheckState(ventAnimator, ventIdleHash, ventIdleState);
        CheckState(ventAnimator, ventErrorBoostHash, ventErrorBoostState);
        CheckState(ventAnimator, ventBoostHash, ventBoostState);
    }

    void CheckState(Animator animator, int hash, string stateName)
    {
        if (animator == null)
        {
            Debug.LogError($"[OxiOAnimationDriver] Animator non assigné pour l'état '{stateName}'.");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[OxiOAnimationDriver] Aucun Animator Controller sur {animator.name}.");
            return;
        }

        if (animator.HasState(0, hash)) return;

        Debug.LogError($"[OxiOAnimationDriver] L'état '{stateName}' est introuvable dans l'Animator de {animator.name}. Vérifie l'orthographe exacte.");
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
        if (isEuphoric || isTransforming) return;
        if (isTalking) return;

        isTalking = true;
        if (!isBlinking) PlayPreservingCycle(talkHash);
    }

    public void StopTalking()
    {
        if (!isTalking) return;

        isTalking = false;
        if (!isBlinking && !isEuphoric && !isTransforming) PlayPreservingCycle(idleHash);
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

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (ventAnimator != null)
            StartCoroutine(VentBoostRoutine());

        oxiAnimator.Play(transformationHash, 0, 0f);
        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

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

            yield return StartCoroutine(BlinkRoutine());
        }
    }

    IEnumerator BlinkRoutine()
    {
        isBlinking = true;

        yield return WaitForCycleStart();

        if (isEuphoric || isTransforming)
        {
            isBlinking = false;
            yield break;
        }

        oxiAnimator.Play(isTalking ? talkBlinkHash : idleBlinkHash, 0, 0f);
        yield return null;
        yield return WaitForStateEnd(oxiAnimator);

        if (!isEuphoric && !isTransforming)
            oxiAnimator.Play(isTalking ? talkHash : idleHash, 0, 0f);

        isBlinking = false;
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
}