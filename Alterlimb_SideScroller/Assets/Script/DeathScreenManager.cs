using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gère la séquence d'animation de mort façon "BIOS reboot".
/// 
/// Déroulé :
///   1. Flicker initial (alternance rapide écran noir / écran normal)
///   2. Phase BIOS : écran noir stable, texte qui s'affiche ligne par ligne
///   3. Flicker final (avant respawn)
///   4. Nettoyage : tout est caché, le joueur est respawné
/// 
/// Singleton : accessible via DeathAnimationManager.Instance.
/// L'UI de jeu est masquée pendant toute la séquence via le champ "Game UI Canvas".
/// </summary>
public class DeathAnimationManager : MonoBehaviour
{
    public static DeathAnimationManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Références UI")]
    [Tooltip("Le GameObject qui contient l'image noire et le texte (sera activé/désactivé)")]
    [SerializeField] GameObject deathOverlay;
    [Tooltip("Le composant TextMeshPro qui affichera le log BIOS")]
    [SerializeField] TextMeshProUGUI biosText;
    [Tooltip("Canvas de l'interface de jeu à masquer pendant l'animation")]
    [SerializeField] GameObject gameUICanvas;

    [Header("Lignes du log BIOS")]
    [Tooltip("Une ligne par entrée. Elles s'afficheront successivement avec un délai entre chaque.")]
    [TextArea(1, 3)]
    [SerializeField]
    List<string> bootLog = new List<string>
    {
        "ALTERLIMB SYSTEM v0.4.2",
        "Memory test... OK",
        "Initializing limb interface...",
        "ERROR: Vital signs lost.",
        "Restoring last checkpoint...",
        "Recompiling consciousness...",
        "Boot sequence complete."
    };

    [Header("Timings — Phases")]
    [Tooltip("Durée du flicker initial (avant BIOS)")]
    [SerializeField] float initialFlickerDuration = 0.5f;
    [Tooltip("Durée d'affichage stable du BIOS")]
    [SerializeField] float biosDuration = 2.5f;
    [Tooltip("Durée du flicker final (avant respawn)")]
    [SerializeField] float finalFlickerDuration = 0.6f;

    [Header("Timings — Flicker")]
    [Tooltip("Vitesse du flicker (intervalle min entre changements)")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [Tooltip("Vitesse du flicker (intervalle max entre changements)")]
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Header("Timings — Texte")]
    [Tooltip("Délai entre l'apparition de chaque ligne du log")]
    [SerializeField] float lineDelay = 0.15f;
    [Tooltip("Délai au tout début, après le flicker initial, avant la 1re ligne")]
    [SerializeField] float biosStartDelay = 0.2f;

    [Header("Audio (optionnel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip lineTypeSound;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // S'assure que tout est caché au démarrage
        if (deathOverlay != null) deathOverlay.SetActive(false);
        if (biosText != null) biosText.text = "";
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Lance l'animation complète de mort + respawn.
    /// Le callback est appelé au moment où le joueur doit être effectivement respawné
    /// (entre le flicker final et la fin de l'animation).
    /// </summary>
    public void PlayDeathSequence(System.Action onRespawn)
    {
        StartCoroutine(DeathSequence(onRespawn));
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence principale
    // ════════════════════════════════════════════════════════════

    IEnumerator DeathSequence(System.Action onRespawn)
    {
        // Masque l'UI de jeu
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // ── Phase 1 : Flicker initial ───────────────────────
        yield return StartCoroutine(FlickerRoutine(initialFlickerDuration, endVisible: true));

        // ── Phase 2 : BIOS stable + texte qui s'affiche ─────
        if (deathOverlay != null) deathOverlay.SetActive(true);
        if (biosText != null) biosText.text = "";

        yield return new WaitForSeconds(biosStartDelay);
        yield return StartCoroutine(TypeBiosLog());

        // Petit temps de lecture stable
        yield return new WaitForSeconds(biosDuration);

        // ── Phase 2.5 : Respawn du joueur (caché derrière l'écran) ──
        // On respawn le joueur PENDANT l'écran noir, pour qu'il apparaisse
        // déjà à la bonne position quand le flicker final révèle la scène
        onRespawn?.Invoke();

        // ── Phase 3 : Flicker final ─────────────────────────
        yield return StartCoroutine(FlickerRoutine(finalFlickerDuration, endVisible: false));

        // ── Phase 4 : Nettoyage ─────────────────────────────
        if (deathOverlay != null) deathOverlay.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);
        if (biosText != null) biosText.text = "";
    }

    // ════════════════════════════════════════════════════════════
    //  Routines internes
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Fait clignoter l'overlay noir pendant `duration` secondes.
    /// `endVisible` détermine si l'overlay reste visible (true) ou caché (false) à la fin.
    /// </summary>
    IEnumerator FlickerRoutine(float duration, bool endVisible)
    {
        if (deathOverlay == null) yield break;

        float elapsed = 0f;
        bool visible = false;

        while (elapsed < duration)
        {
            visible = !visible;
            deathOverlay.SetActive(visible);

            // Son de flicker
            if (audioSource != null && flickerSound != null)
                audioSource.PlayOneShot(flickerSound, 0.4f);

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // État final demandé
        deathOverlay.SetActive(endVisible);
    }

    /// <summary>
    /// Affiche les lignes du bootLog une par une, avec un délai entre chaque.
    /// </summary>
    IEnumerator TypeBiosLog()
    {
        if (biosText == null) yield break;

        biosText.text = "";

        foreach (string line in bootLog)
        {
            biosText.text += line + "\n";

            // Son de typing
            if (audioSource != null && lineTypeSound != null)
                audioSource.PlayOneShot(lineTypeSound, 0.5f);

            yield return new WaitForSeconds(lineDelay);
        }
    }
}