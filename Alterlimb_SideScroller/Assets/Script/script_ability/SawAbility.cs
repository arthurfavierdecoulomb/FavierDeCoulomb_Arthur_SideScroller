using UnityEngine;

public class SawAbility : MonoBehaviour
{
    [Header("Saw Settings")]
    [SerializeField] float attackRange = 1.2f;
    [SerializeField] float attackDamage = 25f;
    [SerializeField] float attackCooldown = 0.4f;
    [SerializeField] LayerMask enemyLayer;

    float cooldownCounter;
    AbilityEnergySystem energySystem;
    PlayerAnimator playerAnimator; // ← AJOUTÉ

    void Awake()
    {
        energySystem = GetComponent<AbilityEnergySystem>();
        playerAnimator = GetComponent<PlayerAnimator>(); // ← AJOUTÉ
    }

    void Update()
    {
        if (cooldownCounter > 0f)
            cooldownCounter -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[SawAbility] Clic détecté. Cooldown : {cooldownCounter}, Enabled : {enabled}");
        }

        if (Input.GetMouseButtonDown(0) && cooldownCounter <= 0f)
        {
            Attack();
            cooldownCounter = attackCooldown;
        }
    }

    void Attack()
    {
        // Notifie le système d'énergie
        energySystem?.OnSawUsed();

        // Déclenche l'animation d'attaque
        playerAnimator?.TriggerAttack(); // ← AJOUTÉ

        // Multiplicateur selon énergie restante
        float multiplier = energySystem != null ? energySystem.GetSawMultiplier() : 1f;
        float finalDamage = attackDamage * multiplier;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
        foreach (var hit in hits)
        {
            DroneEnemy drone = hit.GetComponent<DroneEnemy>();
            if (drone != null && drone.isHooked)
                drone.TakeDamage(finalDamage);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}