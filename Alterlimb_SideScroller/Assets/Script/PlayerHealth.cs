using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la vie du joueur. Supporte 3 modes d'application des dégâts :
///   1. Appel direct via TakeDamage(amount) depuis n'importe quel script
///   2. Collision physique avec un objet tagué (ennemi, projectile)
///   3. Trigger avec un objet tagué (zone de dégâts, hazard)
/// 
/// Régénération : après un délai sans dégâts, la vie remonte progressivement.
/// 
/// Tags reconnus pour les dégâts :
///   - "DroneEnemy" : dégâts du drone (collision physique)
///   - "Bullet" : dégâts d'une bullet (collision OU trigger)
///   - "DamageZone" : dégâts d'une zone (trigger uniquement)
/// 
/// Mort : appelle CharaController.Die() qui gère le respawn + animation BIOS.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float droneDamage = 20f;
    [SerializeField] float bulletDamage = 10f;
    [SerializeField] float invincibilityDuration = 0.5f;

    [Header("Regeneration")]
    [Tooltip("Délai en secondes sans dégâts avant que la régen commence")]
    [SerializeField] float regenDelay = 10f;
    [Tooltip("Quantité de vie restaurée par seconde une fois la régen active")]
    [SerializeField] float regenRate = 5f;

    [Header("UI")]
    [SerializeField] Image healthBar;
    [SerializeField] float barSmoothSpeed = 5f;

    [Header("Debug")]
    [Tooltip("Affiche dans la Console chaque application de dégâts")]
    [SerializeField] bool debugMode = false;

    float currentHealth;
    float displayedHealth;
    float invincibilityTimer = 0f;
    float timeSinceLastDamage = 0f;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Start()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
        timeSinceLastDamage = regenDelay; // Permet la régen immédiate au début (peu importe ici car full HP)
    }

    void Update()
    {
        // Lissage de la barre de vie
        displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, barSmoothSpeed * Time.deltaTime);
        if (healthBar != null)
            healthBar.fillAmount = displayedHealth / maxHealth;

        // Décompte de l'invincibilité
        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;

        // Régénération de vie
        HandleRegeneration();
    }

    // ════════════════════════════════════════════════════════════
    //  Régénération
    // ════════════════════════════════════════════════════════════

    void HandleRegeneration()
    {
        // Pas besoin de régen si full HP ou mort
        if (currentHealth >= maxHealth || currentHealth <= 0f) return;

        timeSinceLastDamage += Time.deltaTime;

        // Attente du délai avant de commencer la régen
        if (timeSinceLastDamage < regenDelay) return;

        currentHealth += regenRate * Time.deltaTime;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    // ════════════════════════════════════════════════════════════
    //  API publique : appel direct par scripts (drone, etc.)
    // ════════════════════════════════════════════════════════════

    public void TakeDamage(float amount)
    {
        if (invincibilityTimer > 0f)
        {
            if (debugMode) Debug.Log($"[PlayerHealth] Dégâts ignorés (invincibilité) : {amount}");
            return;
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        invincibilityTimer = invincibilityDuration;
        timeSinceLastDamage = 0f; // Reset du timer de régen

        if (debugMode) Debug.Log($"[PlayerHealth] Dégâts reçus : {amount}. Vie restante : {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            GetComponent<CharaController>()?.Die();
    }

    // ════════════════════════════════════════════════════════════
    //  Reset (appelé par CharaController.Revive)
    // ════════════════════════════════════════════════════════════

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
        invincibilityTimer = 0f;
        timeSinceLastDamage = regenDelay;
    }

    // ════════════════════════════════════════════════════════════
    //  Détection collision physique (drone qui touche le perso, bullet en collision)
    // ════════════════════════════════════════════════════════════

    void OnCollisionEnter2D(Collision2D col)
    {
        HandleDamageFromObject(col.gameObject);
    }

    // ════════════════════════════════════════════════════════════
    //  Détection trigger (bullet en trigger, zones de dégâts)
    // ════════════════════════════════════════════════════════════

    void OnTriggerEnter2D(Collider2D col)
    {
        HandleDamageFromObject(col.gameObject);
    }

    /// <summary>
    /// Méthode unique de gestion des tags : marche à la fois pour collision ET trigger.
    /// Comme ça, peu importe si la bullet est en trigger ou non, elle est détectée.
    /// </summary>
    void HandleDamageFromObject(GameObject obj)
    {
        if (obj.CompareTag("DroneEnemy"))
        {
            TakeDamage(droneDamage);
            if (debugMode) Debug.Log($"[PlayerHealth] Touché par DroneEnemy : {obj.name}");
        }
        else if (obj.CompareTag("Bullet"))
        {
            TakeDamage(bulletDamage);
            if (debugMode) Debug.Log($"[PlayerHealth] Touché par Bullet : {obj.name}");
            
            Destroy(obj);
        }
        else if (obj.CompareTag("DamageZone"))
        {
            TakeDamage(droneDamage);
            if (debugMode) Debug.Log($"[PlayerHealth] Dans une DamageZone : {obj.name}");
        }
    }
}