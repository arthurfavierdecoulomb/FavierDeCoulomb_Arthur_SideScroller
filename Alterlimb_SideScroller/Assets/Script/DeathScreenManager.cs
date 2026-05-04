using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gère la séquence d'animation de mort façon "BIOS reboot".
/// 
/// Architecture des références :
///   - deathOverlay : conteneur principal qui reste actif pendant toute la séquence
///                    (pour que les coroutines puissent tourner sur ses enfants)
///   - blackBackground : l'Image noire fullscreen — c'est ELLE qu'on flicker (active/inactive)
///   - biosText : le texte BIOS, reste actif pendant la phase BIOS
///   - crtEffect : l'overlay shader CRT scanlines (optionnel) — actif pendant la phase BIOS
/// 
/// Déroulé :
///   1. Flicker initial (alternance rapide de l'Image noire)
///   2. Phase BIOS : Image noire stable + effet CRT + log de boot ligne par ligne
///   3. Flicker final (avant respawn)
///   4. Nettoyage : tout est caché, le joueur est respawné
/// </summary>
public class DeathAnimationManager : MonoBehaviour
{
    public static DeathAnimationManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Types
    // ════════════════════════════════════════════════════════════

    [System.Serializable]
    public class BootLine
    {
        [Tooltip("Texte de la ligne. Utilise {x} et {y} pour insérer les coordonnées du checkpoint.")]
        [TextArea(1, 3)]
        public string text;

        [Tooltip("Délai après cette ligne avant la suivante (en secondes)")]
        [Range(0f, 2f)]
        public float delayAfter = 0.15f;

        [Tooltip("Joue un son de typing pour cette ligne")]
        public bool playSound = true;
    }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Références UI")]
    [Tooltip("Conteneur principal (parent de l'image noire et du texte). Reste actif pendant toute la séquence.")]
    [SerializeField] GameObject deathOverlay;
    [Tooltip("L'Image noire fullscreen. C'est elle qu'on flicker.")]
    [SerializeField] GameObject blackBackground;
    [Tooltip("Le composant TextMeshPro qui affichera le log BIOS.")]
    [SerializeField] TextMeshProUGUI biosText;
    [Tooltip("Effet CRT scanlines (optionnel) — activé pendant la phase BIOS")]
    [SerializeField] GameObject crtEffect;
    [Tooltip("Canvas de l'interface de jeu à masquer pendant l'animation")]
    [SerializeField] GameObject gameUICanvas;

    [Header("Lignes du log BIOS")]
    [Tooltip("Une entrée = une ligne. Chaque ligne a son propre délai pour créer du rythme.")]
    [SerializeField]
    List<BootLine> bootLog = new List<BootLine>
    {
        new BootLine { text = "ALTERLIMB BIOS v0.4.2 — © FavierDeCoulomb Industries", delayAfter = 0.20f },
        new BootLine { text = "Copyright (c) 2089. All limbs reserved.",              delayAfter = 0.30f },
        new BootLine { text = "",                                                     delayAfter = 0.10f, playSound = false },
        new BootLine { text = "[ OK ] CPU detected: BioCortex M7 @ 2.4 GHz",          delayAfter = 0.08f },
        new BootLine { text = "[ OK ] Memory check ......... 4096 MB",               delayAfter = 0.08f },
        new BootLine { text = "[ OK ] Skeletal frame integrity: NOMINAL",             delayAfter = 0.10f },
        new BootLine { text = "[ .. ] Detecting limbs",                               delayAfter = 0.40f },
        new BootLine { text = "       > LeftArm  ......... CONNECTED",                delayAfter = 0.06f },
        new BootLine { text = "       > RightArm ......... CONNECTED",                delayAfter = 0.06f },
        new BootLine { text = "       > LeftLeg  ......... CONNECTED",                delayAfter = 0.06f },
        new BootLine { text = "       > RightLeg ......... CONNECTED",                delayAfter = 0.20f },
        new BootLine { text = "[ ERR ] FATAL: Vital signs lost.",                     delayAfter = 0.60f },
        new BootLine { text = "[ ERR ] Last known cause: TRAUMA",                     delayAfter = 0.50f },
        new BootLine { text = "",                                                     delayAfter = 0.15f, playSound = false },
        new BootLine { text = "Initiating recovery protocol...",                      delayAfter = 0.30f },
        new BootLine { text = "Loading checkpoint data ......... [{x}, {y}]",         delayAfter = 0.25f },
        new BootLine { text = "Recompiling consciousness .........",                  delayAfter = 0.35f },
        new BootLine { text = "Restoring neural pathways .........",                  delayAfter = 0.20f },
        new BootLine { text = "Calibrating camera to host position ...",              delayAfter = 0.30f },
        new BootLine { text = "[ OK ] Camera locked on host.",                        delayAfter = 0.20f },
        new BootLine { text = "",                                                     delayAfter = 0.10f, playSound = false },
        new BootLine { text = "> Booting host: Prinze",                               delayAfter = 0.30f },
        new BootLine { text = "> Press any key to resume operations_",                delayAfter = 0.40f },
    };

    [Header("Timings — Phases")]
    [Tooltip("Durée du flicker initial (avant BIOS)")]
    [SerializeField] float initialFlickerDuration = 0.5f;
    [Tooltip("Délai après la dernière ligne avant le flicker final")]
    [SerializeField] float biosEndPause = 0.6f;
    [Tooltip("Durée du flicker final (avant respawn)")]
    [SerializeField] float finalFlickerDuration = 0.6f;

    [Header("Timings — Flicker")]
    [Tooltip("Vitesse du flicker (intervalle min entre changements)")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [Tooltip("Vitesse du flicker (intervalle max entre changements)")]
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Header("Timings — Texte")]
    [Tooltip("Délai au tout début, après le flicker initial, avant la 1re ligne")]
    [SerializeField] float biosStartDelay = 0.2f;
    [Tooltip("Curseur clignotant à la fin du texte (active/désactive)")]
    [SerializeField] bool useBlinkingCursor = true;

    [Header("Audio (optionnel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip lineTypeSound;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Vector3 lastCheckpointPosition;
    bool isPlaying;

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

        // Le conteneur principal reste actif (sinon les coroutines plantent)
        if (deathOverlay != null) deathOverlay.SetActive(true);

        // Tout le reste est caché au démarrage
        if (blackBackground != null) blackBackground.SetActive(false);
        if (crtEffect != null) crtEffect.SetActive(false);
        if (biosText != null)
        {
            biosText.text = "";
            biosText.gameObject.SetActive(false);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    public void PlayDeathSequence(System.Action onRespawn, Vector3 checkpointPosition = default)
    {
        if (isPlaying)
        {
            Debug.LogWarning("[DeathAnimationManager] Une séquence est déjà en cours. Ignoré.");
            return;
        }

        lastCheckpointPosition = checkpointPosition;
        StartCoroutine(DeathSequence(onRespawn));
    }

    // ════════════════════════════════════════════════════════════
    //  Séquence principale
    // ════════════════════════════════════════════════════════════

    IEnumerator DeathSequence(System.Action onRespawn)
    {
        isPlaying = true;

        // Masque l'UI de jeu
        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // ── Phase 1 : Flicker initial ───────────────────────
        // On flicker juste le fond noir (pas d'effet CRT, pas de texte)
        yield return StartCoroutine(FlickerRoutine(initialFlickerDuration, endVisible: true));

        // ── Phase 2 : BIOS stable + effet CRT + texte ────────
        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);
        if (biosText != null)
        {
            biosText.text = "";
            biosText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(biosStartDelay);

        // Curseur clignotant en parallèle
        Coroutine cursorRoutine = null;
        if (useBlinkingCursor && biosText != null)
            cursorRoutine = StartCoroutine(BlinkCursorRoutine());

        // Affiche le log ligne par ligne
        yield return StartCoroutine(TypeBiosLog());

        // Pause finale pour la lecture
        yield return new WaitForSeconds(biosEndPause);

        // Stoppe le curseur
        if (cursorRoutine != null) StopCoroutine(cursorRoutine);

        // ── Phase 2.5 : Respawn caché ───────────────────────
        onRespawn?.Invoke();

        // Cache le texte ET l'effet CRT avant le flicker final
        if (biosText != null)
        {
            biosText.text = "";
            biosText.gameObject.SetActive(false);
        }
        if (crtEffect != null) crtEffect.SetActive(false);

        // ── Phase 3 : Flicker final ─────────────────────────
        yield return StartCoroutine(FlickerRoutine(finalFlickerDuration, endVisible: false));

        // ── Phase 4 : Nettoyage ─────────────────────────────
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        isPlaying = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Routines internes
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Fait clignoter UNIQUEMENT le fond noir (pas l'effet CRT ni le texte).
    /// </summary>
    IEnumerator FlickerRoutine(float duration, bool endVisible)
    {
        if (blackBackground == null) yield break;

        float elapsed = 0f;
        bool visible = false;

        while (elapsed < duration)
        {
            visible = !visible;
            blackBackground.SetActive(visible);

            if (audioSource != null && flickerSound != null)
                audioSource.PlayOneShot(flickerSound, 0.4f);

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        blackBackground.SetActive(endVisible);
    }

    /// <summary>
    /// Affiche les lignes du bootLog une par une, avec leur délai individuel.
    /// </summary>
    IEnumerator TypeBiosLog()
    {
        if (biosText == null) yield break;

        biosText.text = "";

        foreach (BootLine line in bootLog)
        {
            string formatted = FormatLine(line.text);
            biosText.text += formatted + "\n";

            // Force TMP à mettre à jour son rendu maintenant
            biosText.ForceMeshUpdate();

            if (line.playSound && audioSource != null && lineTypeSound != null)
                audioSource.PlayOneShot(lineTypeSound, 0.5f);

            yield return new WaitForSeconds(line.delayAfter);
        }
    }

    /// <summary>
    /// Remplace les placeholders {x}, {y} par les coordonnées du checkpoint.
    /// </summary>
    string FormatLine(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw
            .Replace("{x}", lastCheckpointPosition.x.ToString("F1"))
            .Replace("{y}", lastCheckpointPosition.y.ToString("F1"));
    }

    /// <summary>
    /// Curseur clignotant à la fin du texte, pendant que le BIOS écrit.
    /// </summary>
    IEnumerator BlinkCursorRoutine()
    {
        const string cursor = "_";
        bool visible = true;

        while (true)
        {
            if (biosText != null)
            {
                if (visible && !biosText.text.EndsWith(cursor))
                    biosText.text += cursor;
                else if (!visible && biosText.text.EndsWith(cursor))
                    biosText.text = biosText.text.Substring(0, biosText.text.Length - cursor.Length);
            }
            visible = !visible;
            yield return new WaitForSeconds(0.4f);
        }
    }
}