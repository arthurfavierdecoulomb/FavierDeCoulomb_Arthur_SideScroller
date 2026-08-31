using System.Collections;
using UnityEngine;

public abstract class OxiOAttack : MonoBehaviour
{
    [Header("Sélection")]
    [SerializeField] private float weight = 1f;
    [SerializeField] private float cooldown = 4f;
    [SerializeField] private int minPhase = 1;
    [SerializeField] private int maxPhase = 99;

    private float lastUsedTime = -999f;

    public float Weight => Mathf.Max(0f, weight);

    public bool IsAvailable(int currentPhase)
    {
        return currentPhase >= minPhase
            && currentPhase <= maxPhase
            && Time.time - lastUsedTime >= cooldown;
    }

    public IEnumerator Execute(int currentPhase)
    {
        lastUsedTime = Time.time;
        yield return Run(currentPhase);
    }

    public void ResetCooldown()
    {
        lastUsedTime = -999f;
    }

    public virtual void Interrupt()
    {
        StopAllCoroutines();
    }

    protected abstract IEnumerator Run(int currentPhase);
}