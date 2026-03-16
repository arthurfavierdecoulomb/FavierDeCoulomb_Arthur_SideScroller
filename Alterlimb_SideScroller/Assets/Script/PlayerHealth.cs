using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float droneDamage = 20f;
    [SerializeField] float bulletDamage = 10f;
    [SerializeField] float invincibilityDuration = 0.5f;

    [Header("UI")]
    [SerializeField] Image healthBar;
    [SerializeField] float barSmoothSpeed = 5f;

    float currentHealth;
    float displayedHealth;
    float invincibilityTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
    }

    void Update()
    {
        displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, barSmoothSpeed * Time.deltaTime);
        if (healthBar != null)
            healthBar.fillAmount = displayedHealth / maxHealth;

        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(float amount)
    {
        if (invincibilityTimer > 0f) return;
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        invincibilityTimer = invincibilityDuration;

        if (currentHealth <= 0f)
            GetComponent<CharaController>()?.Die(); // ← utilise le respawn, pas LoadScene
    }

    // Appelé par CharaController.Revive() pour reset la vie au respawn
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        displayedHealth = maxHealth;
        invincibilityTimer = 0f;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("DroneEnemy"))
            TakeDamage(droneDamage);
        if (col.gameObject.CompareTag("Bullet"))
            TakeDamage(bulletDamage);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("DeadZone"))
            GetComponent<CharaController>()?.Die(); // ← même chose ici
    }
}