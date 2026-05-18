using UnityEngine;
using System.Collections;

/// <summary>
/// Feedback visuel de dégâts pour un ennemi.
/// 
/// À chaque coup reçu (méthode ShowDamage appelée par DroneEnemy) :
///   - Le sprite clignote brièvement en BLANC (flash d'impact)
///   - Un DamageNumber ("-30") apparaît au-dessus de l'ennemi
/// 
/// Script séparé : le DroneEnemy gère l'IA, ce script gère le ressenti.
/// Réutilisable sur n'importe quel ennemi ayant un SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DamageFeedback : MonoBehaviour
{
    [Header("Flash blanc")]
    [Tooltip("Couleur du flash à l'impact")]
    [SerializeField] Color flashColor = Color.white;
    [Tooltip("Durée du flash (secondes)")]
    [SerializeField] float flashDuration = 0.08f;

    [Header("Damage Number")]
    [Tooltip("Prefab du nombre de dégâts flottant")]
    [SerializeField] GameObject damageNumberPrefab;
    [Tooltip("Décalage d'apparition du nombre par rapport au centre de l'ennemi")]
    [SerializeField] Vector2 numberSpawnOffset = new Vector2(0f, 0.8f);
    [Tooltip("Variation aléatoire de la position d'apparition (évite la superposition)")]
    [SerializeField] float numberSpawnRandomness = 0.3f;

    SpriteRenderer spriteRenderer;
    Color originalColor;
    Coroutine flashCoroutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    /// <summary>
    /// Déclenche le feedback de dégâts : flash blanc + damage number.
    /// Appelé par le DroneEnemy (ou tout autre ennemi) quand il prend des dégâts.
    /// </summary>
    public void ShowDamage(float amount)
    {
        // ── Flash blanc ──
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());

        // ── Damage number ──
        if (damageNumberPrefab != null)
        {
            Vector3 spawnPos = transform.position + (Vector3)numberSpawnOffset;

            // Petit décalage horizontal aléatoire
            spawnPos.x += Random.Range(-numberSpawnRandomness, numberSpawnRandomness);

            GameObject obj = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
            DamageNumber dn = obj.GetComponent<DamageNumber>();
            if (dn != null) dn.Setup(amount);
        }
    }

    IEnumerator FlashRoutine()
    {
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
        flashCoroutine = null;
    }
}