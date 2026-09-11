using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeMoverDamage : MonoBehaviour
{
    [Header("Cible")]
    [SerializeField] private string playerTag = "Player";

    [Header("Dégâts par phase")]
    [SerializeField] private float[] damagePerPhase = { 40f, 60f };
    [SerializeField] private bool damageAsPercentOfMax = true;

    [Header("Contact")]
    [SerializeField] private bool damageOnEnterOnly = true;
    [SerializeField] private float damagePerSecondFactor = 1f;
    [SerializeField] private float ownCooldown = 0f;

    [Header("Phase")]
    [SerializeField] private OxiOBossDirector director;
    [SerializeField] private int fallbackPhase = 1;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private float lastHitTime = -999f;

    private void Awake()
    {
        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        LogSetup();
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (damagePerPhase == null || damagePerPhase.Length == 0)
            Debug.LogError($"[SpikeDamage] '{name}' : le tableau Damage Per Phase est vide, aucun dégât ne sera infligé.", this);

        if (CompareTag("DamageZone"))
            Debug.LogError($"[SpikeDamage] '{name}' : cet objet est taggé 'DamageZone'. Le PlayerHealth lui appliquera AUSSI ses dégâts continus, donc le joueur encaissera deux fois. Change son tag.", this);

        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null && !collider.isTrigger && damageOnEnterOnly)
            Debug.LogWarning($"[SpikeDamage] '{name}' : le Collider2D n'est pas en Is Trigger. Les dégâts passeront par la collision physique, ce qui repoussera Azu.", this);

        if (director == null)
            Debug.LogWarning($"[SpikeDamage] '{name}' : aucun OxiOBossDirector trouvé, la phase {fallbackPhase} sera utilisée en permanence.", this);
    }

    private int CurrentPhase()
    {
        return director != null ? director.CurrentPhase : fallbackPhase;
    }

    private float DamageForCurrentPhase()
    {
        if (damagePerPhase == null || damagePerPhase.Length == 0)
            return 0f;

        int index = Mathf.Clamp(CurrentPhase() - 1, 0, damagePerPhase.Length - 1);
        return damagePerPhase[index];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!damageOnEnterOnly)
            return;

        TryDamage(other, DamageForCurrentPhase(), false);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageOnEnterOnly)
            return;

        TryDamage(other, DamageForCurrentPhase() * damagePerSecondFactor * Time.deltaTime, true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!damageOnEnterOnly)
            return;

        TryDamage(collision.collider, DamageForCurrentPhase(), false);
    }

    private void TryDamage(Collider2D other, float amount, bool continuous)
    {
        if (other == null || !other.CompareTag(playerTag))
            return;

        if (!continuous && Time.time - lastHitTime < ownCooldown)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health == null)
        {
            if (logDiagnostics)
                Debug.LogWarning($"[SpikeDamage] '{name}' : aucun PlayerHealth trouvé sur '{other.name}' ni sur ses parents.", this);

            return;
        }

        float finalAmount = damageAsPercentOfMax
            ? health.MaxHealth * amount * 0.01f
            : amount;

        if (finalAmount <= 0f)
            return;

        if (continuous)
        {
            health.ApplyContinuousDamage(finalAmount);
            return;
        }

        lastHitTime = Time.time;
        health.TakeDamage(finalAmount);

        if (logDiagnostics)
            Debug.Log($"[SpikeDamage] '{name}' : phase {CurrentPhase()}, {amount}{(damageAsPercentOfMax ? " %" : " pv")} infligés ({finalAmount:0.#} pv).", this);
    }
}