using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float droneDamage = 20f;
    [SerializeField] float bulletDamage = 10f;
    [SerializeField] float invincibilityDuration = 0.5f; // invincible brièvement après un hit

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
        // Barre fluide
        displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, barSmoothSpeed * Time.deltaTime);
        healthBar.fillAmount = displayedHealth / maxHealth;

        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(float amount)
    {
        if (invincibilityTimer > 0f) return; // invincible, on ignore

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        invincibilityTimer = invincibilityDuration;

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("DroneEnemy"))
            TakeDamage(droneDamage);

        if (col.gameObject.CompareTag("Bullet"))
            TakeDamage(bulletDamage);
    }

    // Contact avec un projectile
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("DeadZone"))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}