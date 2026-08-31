using UnityEngine;

public class OxiOAnimationRelay : MonoBehaviour
{
    [SerializeField] private OxiOCore core;

    private void Awake()
    {
        if (core == null)
            core = GetComponentInParent<OxiOCore>();

        if (core == null)
            Debug.LogError($"[OxiOAnimationRelay] '{name}' : aucun OxiCore trouvé, les AE ne font que dalle.", this);
    }

    public void CoreExplosion()
    {
        if (core != null)
            core.TriggerCoreExplosion();
    }
}