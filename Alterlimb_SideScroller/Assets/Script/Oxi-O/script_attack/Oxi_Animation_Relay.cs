using UnityEngine;

public class OxiOAnimationRelay : MonoBehaviour
{
    [SerializeField] private OxiOCore core;
    [SerializeField] private OxiO_Animation animationDriver;

    private void Awake()
    {
        if (core == null)
            core = GetComponentInParent<OxiOCore>();

        if (core == null)
            core = FindAnyObjectByType<OxiOCore>();

        if (animationDriver == null)
            animationDriver = GetComponentInParent<OxiO_Animation>();

        if (animationDriver == null)
            animationDriver = FindAnyObjectByType<OxiO_Animation>();

        if (core == null)
            Debug.LogError($"[OxiOAnimationRelay] '{name}' : aucun OxiOCore trouvé, l'Animation Event CoreExplosion ne fera rien.", this);

        if (animationDriver == null)
            Debug.LogError($"[OxiOAnimationRelay] '{name}' : aucun OxiO_Animation trouvé, l'Animation Event OxiFall ne fera rien.", this);
    }

    public void CoreExplosion()
    {
        if (core != null)
            core.TriggerCoreExplosion();
    }

    public void OxiFall()
    {
        if (animationDriver != null)
            animationDriver.TriggerFinalFall();
    }
}