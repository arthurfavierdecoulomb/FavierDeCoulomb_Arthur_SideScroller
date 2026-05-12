using UnityEngine;

/// <summary>
/// Capacité d'attaque à la scie. Inflige des dégâts aux drones dans une zone
/// circulaire devant le joueur. Notifie l'AbilityEnergySystem pour vider la
/// barre d'énergie et le PlayerAnimator pour jouer l'animation d'attaque.
/// 
/// Activée uniquement quand le joueur a équipé l'altermembre Scie via
/// AbilityManager (sinon enabled = false, donc Update ne tourne pas).
/// </summary>
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

        if (Input.GetMouseButtonDown(0) && cooldownCounter <= 0f)
        {
            Attack();
            cooldownCounter = attackCooldown;
        }
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