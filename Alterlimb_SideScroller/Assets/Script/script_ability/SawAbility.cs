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
    PlayerAnimator playerAnimator;

    void Awake()
    {
        energySystem = GetComponent<AbilityEnergySystem>();
        playerAnimator = GetComponent<PlayerAnimator>();
    }

    void Update()
    {
        if (cooldownCounter > 0f)
            cooldownCounter -= Time.deltaTime;

        if (!Input.GetMouseButtonDown(0) || cooldownCounter > 0f) return;
        if (energySystem != null && !energySystem.CanUseSaw()) return;

        Attack();
        cooldownCounter = attackCooldown;
    }

    void Attack()
    {
        energySystem?.OnSawUsed();
        playerAnimator?.TriggerAttack();

        float multiplier = energySystem != null ? energySystem.GetSawMultiplier() : 1f;
        float finalDamage = attackDamage * multiplier;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
        foreach (var hit in hits)
        {
            DroneEnemy drone = hit.GetComponentInParent<DroneEnemy>();
            if (drone != null)
                drone.TakeDamage(finalDamage);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}