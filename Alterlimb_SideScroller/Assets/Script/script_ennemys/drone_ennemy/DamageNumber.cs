using UnityEngine;
using TMPro;

/// <summary>
/// Nombre de dégâts flottant ("-30") qui apparaît à l'impact.
/// 
/// Animation : le texte monte doucement, tremble légèrement (shake),
/// et s'estompe (fade out). Une fois l'animation finie, il s'autodétruit.
/// 
/// Objet jetable et réutilisable : on l'instancie via Setup(), il gère
/// tout seul son cycle de vie.
/// 
/// Setup du prefab :
///   - Un GameObject avec un composant TextMeshPro (3D, pas UGUI)
///   - Ce script attaché
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamageNumber : MonoBehaviour
{
    [Header("Mouvement")]
    [Tooltip("Vitesse de montée du texte (unités/seconde)")]
    [SerializeField] float riseSpeed = 1.5f;
    [Tooltip("Durée totale de vie du nombre avant disparition")]
    [SerializeField] float lifetime = 0.8f;

    [Header("Shake")]
    [Tooltip("Amplitude du tremblement horizontal (unités Unity)")]
    [SerializeField] float shakeAmount = 0.08f;
    [Tooltip("Vitesse du tremblement")]
    [SerializeField] float shakeSpeed = 35f;

    [Header("Apparence")]
    [Tooltip("Échelle de départ (petit punch au spawn)")]
    [SerializeField] float startScale = 0.6f;
    [Tooltip("Échelle stable après le punch")]
    [SerializeField] float normalScale = 1f;
    [Tooltip("Durée du punch d'échelle au spawn")]
    [SerializeField] float punchDuration = 0.15f;

    TextMeshPro tmp;
    float elapsed;
    Vector3 basePosition;
    Color startColor;

    void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// Initialise le nombre de dégâts. À appeler juste après l'instanciation.
    /// </summary>
    /// <param name="damageAmount">Le montant de dégâts à afficher</param>
    public void Setup(float damageAmount)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();

        // Affiche "-30" (arrondi, pas de décimales)
        tmp.text = "-" + Mathf.RoundToInt(damageAmount).ToString();

        basePosition = transform.position;
        startColor = tmp.color;
        elapsed = 0f;

        transform.localScale = Vector3.one * startScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // ── Montée ──
        basePosition += Vector3.up * riseSpeed * Time.deltaTime;

        // ── Shake horizontal ──
        float shakeOffset = Mathf.Sin(elapsed * shakeSpeed) * shakeAmount * (1f - t);
        transform.position = basePosition + Vector3.right * shakeOffset;

        // ── Punch d'échelle au spawn ──
        if (elapsed < punchDuration)
        {
            float punchT = elapsed / punchDuration;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, normalScale, punchT);
        }
        else
        {
            transform.localScale = Vector3.one * normalScale;
        }

        // ── Fade out (sur la 2e moitié de vie) ──
        if (tmp != null)
        {
            float alpha = (t < 0.5f) ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) * 2f);
            Color c = startColor;
            c.a = alpha;
            tmp.color = c;
        }
    }
}