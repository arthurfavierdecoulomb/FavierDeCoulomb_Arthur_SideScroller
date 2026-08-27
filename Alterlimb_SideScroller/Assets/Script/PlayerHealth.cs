using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float droneDamage = 20f;
    [SerializeField] float bulletDamage = 10f;
    [SerializeField] float invincibilityDuration = 0.5f;

    [Header("Damage Zone")]
    [SerializeField] float damageZoneDamagePerSecond = 15f;

    [Header("Regeneration")]
    [SerializeField] float regenDelay = 10f;
    [SerializeField] float regenRate = 5f;

    [Header("UI")]
    [SerializeField] Image healthBar;
    [SerializeField] float barSmoothSpeed = 5f;

    [Header("Debug")]
    [SerializeField] bool debugMode = false;

    float currentHealth;
    float displayedHealth;
    float invincibilityTimer = 0f;
    float timeSinceLastDamage = 0f;
    bool isDead = false;

    readonly HashSet<GameObject> activeDamageZones = new HashSet<GameObject>();

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Start()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
        timeSinceLastDamage = regenDelay;
    }

    void Update()
    {
        displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, barSmoothSpeed * Time.deltaTime);
        if (healthBar != null)
            healthBar.fillAmount = displayedHealth / maxHealth;

        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;

        HandleDamageZones();
        HandleRegeneration();
    }

    void HandleDamageZones()
    {
        if (isDead)
        {
            activeDamageZones.Clear();
            return;
        }

        activeDamageZones.RemoveWhere(zone => zone == null || !zone.activeInHierarchy);
        if (activeDamageZones.Count == 0) return;

        ApplyContinuousDamage(damageZoneDamagePerSecond * Time.deltaTime);
    }

    void HandleRegeneration()
    {
        if (currentHealth >= maxHealth || currentHealth <= 0f) return;

        timeSinceLastDamage += Time.deltaTime;
        if (timeSinceLastDamage < regenDelay) return;

        currentHealth += regenRate * Time.deltaTime;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        if (invincibilityTimer > 0f)
        {
            if (debugMode) Debug.Log($"[PlayerHealth] Dégâts ignorés (invincibilité) : {amount}");
            return;
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        invincibilityTimer = invincibilityDuration;
        timeSinceLastDamage = 0f;

        if (debugMode) Debug.Log($"[PlayerHealth] Dégâts reçus : {amount}. Vie restante : {currentHealth}/{maxHealth}");

        CheckDeath();
    }

    public void ApplyContinuousDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        timeSinceLastDamage = 0f;

        CheckDeath();
    }

    void CheckDeath()
    {
        if (currentHealth > 0f) return;

        isDead = true;
        activeDamageZones.Clear();
        GetComponent<CharaController>()?.Die();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
        invincibilityTimer = 0f;
        timeSinceLastDamage = regenDelay;
        isDead = false;
        activeDamageZones.Clear();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        HandleContactEnter(col.gameObject);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        HandleContactExit(col.gameObject);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        HandleContactEnter(col.gameObject);
    }

    void OnTriggerExit2D(Collider2D col)
    {
        HandleContactExit(col.gameObject);
    }

    void HandleContactEnter(GameObject obj)
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
            activeDamageZones.Add(obj);
            if (debugMode) Debug.Log($"[PlayerHealth] Entrée dans DamageZone : {obj.name}");
        }
    }

    void HandleContactExit(GameObject obj)
    {
        if (!obj.CompareTag("DamageZone")) return;

        activeDamageZones.Remove(obj);
        if (debugMode) Debug.Log($"[PlayerHealth] Sortie de DamageZone : {obj.name}");
    }
}