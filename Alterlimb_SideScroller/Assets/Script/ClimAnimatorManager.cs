using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AirConditioner : MonoBehaviour
{
    public enum State
    {
        Ventilation,
        HS,
        Eco
    }

    [Header("État")]
    [SerializeField] private State currentState = State.Ventilation;

    [Header("Rendu")]
    [SerializeField] private Animator animator;

    [Header("Noms des states de l'Animator")]
    [SerializeField] private string ventilationStateName = "ventilation";
    [SerializeField] private string hsStateName = "mode_hs";
    [SerializeField] private string ecoStateName = "mode_economie";

    private int ventilationStateHash;
    private int hsStateHash;
    private int ecoStateHash;

    public State CurrentState => currentState;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        CacheStateHashes();
    }

    private void OnEnable()
    {
        ApplyState(currentState);
    }

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (!Application.isPlaying)
            return;

        CacheStateHashes();
        ApplyState(currentState);
    }

    public void SetState(State newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        ApplyState(newState);
    }

    public void SetStateIndex(int index)
    {
        SetState((State)Mathf.Clamp(index, 0, 2));
    }

    public void CycleState()
    {
        SetState((State)(((int)currentState + 1) % 3));
    }

    private void CacheStateHashes()
    {
        ventilationStateHash = Animator.StringToHash(ventilationStateName);
        hsStateHash = Animator.StringToHash(hsStateName);
        ecoStateHash = Animator.StringToHash(ecoStateName);
    }

    private void ApplyState(State state)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        int stateHash = StateHashFor(state);

        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"{name} : le state \"{StateNameFor(state)}\" est introuvable dans {animator.runtimeAnimatorController.name}", this);
            return;
        }

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
    }

    private int StateHashFor(State state)
    {
        switch (state)
        {
            case State.Ventilation:
                return ventilationStateHash;
            case State.HS:
                return hsStateHash;
            case State.Eco:
                return ecoStateHash;
        }

        return 0;
    }

    private string StateNameFor(State state)
    {
        switch (state)
        {
            case State.Ventilation:
                return ventilationStateName;
            case State.HS:
                return hsStateName;
            case State.Eco:
                return ecoStateName;
        }

        return string.Empty;
    }
}