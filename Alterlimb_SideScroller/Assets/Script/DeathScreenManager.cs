using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gère la séquence d'animation de mort façon "BIOS reboot".
/// 
/// Architecture des références :
///   - deathOverlay : conteneur principal qui reste actif pendant toute la séquence
///   - blackBackground : l'Image noire fullscreen (flicker)
///   - disconnectedTitle : le grand titre "DISCONNECTED" glitché et flickeré au centre
///   - biosText : le log BIOS qui s'affiche ligne par ligne
///   - crtEffect : l'overlay shader CRT scanlines (optionnel)
/// 
/// Déroulé :
///   1. Flicker initial (alternance rapide de l'Image noire)
///   2. Titre DISCONNECTED : glitch des lettres + flicker en parallèle
///   3. Phase BIOS : Image noire stable + effet CRT + log de boot ligne par ligne
///   4. Flicker final (avant respawn)
///   5. Nettoyage : tout est caché, le joueur est respawné
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
    [Tooltip("Conteneur principal (parent de tout). Reste actif pendant toute la séquence.")]
    [SerializeField] GameObject deathOverlay;
    [Tooltip("L'Image noire fullscreen. C'est elle qu'on flicker.")]
    [SerializeField] GameObject blackBackground;
    [Tooltip("Titre DISCONNECTED glitché au centre de l'écran")]
    [SerializeField] TextMeshProUGUI disconnectedTitle;
    [Tooltip("Le composant TextMeshPro qui affichera le log BIOS.")]
    [SerializeField] TextMeshProUGUI biosText;
    [Tooltip("Effet CRT scanlines (optionnel) — activé pendant la phase BIOS")]
    [SerializeField] GameObject crtEffect;
    [Tooltip("Canvas de l'interface de jeu à masquer pendant l'animation")]
    [SerializeField] GameObject gameUICanvas;

    [Header("Titre DISCONNECTED — Texte")]
    [Tooltip("Texte à afficher (par défaut : DISCONNECTED)")]
    [SerializeField] string disconnectedText = "DISCONNECTED";
    [Tooltip("Durée totale d'affichage du titre")]
    [SerializeField] float disconnectedDuration = 1.8f;

    [Header("Titre DISCONNECTED — Glitch (lettres corrompues)")]
    [Tooltip("Probabilité qu'une lettre soit corrompue à chaque tick (0 = jamais, 1 = toujours)")]
    [Range(0f, 1f)]
    [SerializeField] float glitchProbability = 0.25f;
    [Tooltip("Intervalle entre chaque mise à jour du glitch (en secondes)")]
    [SerializeField] float glitchUpdateInterval = 0.05f;
    [Tooltip("Caractères utilisés pour remplacer aléatoirement les lettres")]
    [SerializeField] string glitchChars = "█▓▒░#@%&*?!|/\\<>";

    [Header("Titre DISCONNECTED — Flicker (apparition/disparition)")]
    [Tooltip("Active le flicker du titre (en plus du glitch des lettres)")]
    [SerializeField] bool useFlicker = true;
    [Tooltip("Intervalle MIN entre chaque changement de visibilité (en secondes)")]
    [SerializeField] float flickerTitleMin = 0.04f;
    [Tooltip("Intervalle MAX entre chaque changement de visibilité (en secondes)")]
    [SerializeField] float flickerTitleMax = 0.18f;
    [Tooltip("Probabilité que le titre soit visible à chaque tick (0.7 = visible 70% du temps)")]
    [Range(0.1f, 1f)]
    [SerializeField] float flickerVisibility = 0.65f;

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
    [Tooltip("Durée du flicker initial (avant titre DISCONNECTED)")]
    [SerializeField] float initialFlickerDuration = 0.5f;
    [Tooltip("Délai entre la disparition du titre et le début du BIOS")]
    [SerializeField] float titleToBiosDelay = 0.3f;
    [Tooltip("Délai après la dernière ligne avant le flicker final")]
    [SerializeField] float biosEndPause = 0.6f;
    [Tooltip("Durée du flicker final (avant respawn)")]
    [SerializeField] float finalFlickerDuration = 0.6f;

    [Header("Timings — Flicker écran")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Header("Timings — Texte")]
    [SerializeField] float biosStartDelay = 0.2f;
    [SerializeField] bool useBlinkingCursor = true;

    [Header("Audio (optionnel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [SerializeField] AudioClip lineTypeSound;
    [SerializeField] AudioClip disconnectSound;

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

        if (deathOverlay != null) deathOverlay.SetActive(true);

        if (blackBackground != null) blackBackground.SetActive(false);
        if (crtEffect != null) crtEffect.SetActive(false);
        if (disconnectedTitle != null)
        {
            disconnectedTitle.text = "";
            disconnectedTitle.gameObject.SetActive(false);
        }
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

        if (gameUICanvas != null) gameUICanvas.SetActive(false);

        // ── Phase 1 : Flicker initial ───────────────────────
        yield return StartCoroutine(FlickerRoutine(initialFlickerDuration, endVisible: true));

        // ── Phase 2 : Titre DISCONNECTED glitché + flickeré ─
        if (blackBackground != null) blackBackground.SetActive(true);
        if (crtEffect != null) crtEffect.SetActive(true);

        if (audioSource != null && disconnectSound != null)
            audioSource.PlayOneShot(disconnectSound, 0.7f);

        yield return StartCoroutine(ShowDisconnectedTitle());

        yield return new WaitForSeconds(titleToBiosDelay);

        // ── Phase 3 : BIOS — texte qui s'affiche ────────────
        if (biosText != null)
        {
            biosText.text = "";
            biosText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(biosStartDelay);

        Coroutine cursorRoutine = null;
        if (useBlinkingCursor && biosText != null)
            cursorRoutine = StartCoroutine(BlinkCursorRoutine());

        yield return StartCoroutine(TypeBiosLog());

        yield return new WaitForSeconds(biosEndPause);

        if (cursorRoutine != null) StopCoroutine(cursorRoutine);

        // ── Phase 3.5 : Respawn caché ───────────────────────
        onRespawn?.Invoke();

        if (biosText != null)
        {
            biosText.text = "";
            biosText.gameObject.SetActive(false);
        }
        if (crtEffect != null) crtEffect.SetActive(false);

        // ── Phase 4 : Flicker final ─────────────────────────
        yield return StartCoroutine(FlickerRoutine(finalFlickerDuration, endVisible: false));

        // ── Phase 5 : Nettoyage ─────────────────────────────
        if (blackBackground != null) blackBackground.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        isPlaying = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Titre DISCONNECTED — Glitch + Flicker en parallèle
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Affiche le titre "DISCONNECTED" pendant `disconnectedDuration` secondes.
    /// Lance EN PARALLÈLE deux coroutines :
    ///   - GlitchTextRoutine : modifie aléatoirement les lettres
    ///   - FlickerTitleRoutine : fait apparaître/disparaître le titre rapidement
    /// </summary>
    IEnumerator ShowDisconnectedTitle()
    {
        if (disconnectedTitle == null) yield break;

        // Active le titre et initialise le texte
        disconnectedTitle.gameObject.SetActive(true);
        disconnectedTitle.text = disconnectedText;

        // Lance les deux effets en parallèle
        Coroutine glitchRoutine = StartCoroutine(GlitchTextRoutine());
        Coroutine flickerRoutine = null;

        if (useFlicker)
            flickerRoutine = StartCoroutine(FlickerTitleRoutine());

        // Attend la durée totale
        yield return new WaitForSeconds(disconnectedDuration);

        // Stoppe les deux effets
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);

        // Cache le titre
        disconnectedTitle.text = "";
        disconnectedTitle.gameObject.SetActive(false);
    }

    /// <summary>
    /// Boucle infinie qui modifie le texte du titre avec des lettres corrompues.
    /// Doit être stoppée par StopCoroutine depuis l'extérieur.
    /// </summary>
    IEnumerator GlitchTextRoutine()
    {
        while (true)
        {
            if (disconnectedTitle != null)
            {
                disconnectedTitle.text = GenerateGlitchedText(disconnectedText);
                disconnectedTitle.ForceMeshUpdate();
            }
            yield return new WaitForSeconds(glitchUpdateInterval);
        }
    }

    /// <summary>
    /// Boucle infinie qui fait clignoter le titre (active/désactive son GameObject).
    /// Doit être stoppée par StopCoroutine depuis l'extérieur.
    /// </summary>
    IEnumerator FlickerTitleRoutine()
    {
        while (true)
        {
            if (disconnectedTitle != null)
            {
                // Tirage aléatoire pour décider si le titre est visible ou caché
                bool visible = Random.value < flickerVisibility;
                disconnectedTitle.gameObject.SetActive(visible);
            }

            float interval = Random.Range(flickerTitleMin, flickerTitleMax);
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// Renvoie le texte avec une partie des lettres aléatoirement remplacées
    /// par des caractères de la palette glitchChars.
    /// </summary>
    string GenerateGlitchedText(string original)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(glitchChars))
            return original;

        char[] chars = original.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ') continue;

            if (Random.value < glitchProbability)
            {
                int idx = Random.Range(0, glitchChars.Length);
                chars[i] = glitchChars[idx];
            }
        }
        return new string(chars);
    }

    // ════════════════════════════════════════════════════════════
    //  Routines internes (flicker écran, BIOS, curseur)
    // ════════════════════════════════════════════════════════════

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

    IEnumerator TypeBiosLog()
    {
        if (biosText == null) yield break;

        biosText.text = "";

        foreach (BootLine line in bootLog)
        {
            string formatted = FormatLine(line.text);
            biosText.text += formatted + "\n";
            biosText.ForceMeshUpdate();

            if (line.playSound && audioSource != null && lineTypeSound != null)
                audioSource.PlayOneShot(lineTypeSound, 0.5f);

            yield return new WaitForSeconds(line.delayAfter);
        }
    }

    string FormatLine(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw
            .Replace("{x}", lastCheckpointPosition.x.ToString("F1"))
            .Replace("{y}", lastCheckpointPosition.y.ToString("F1"));
    }

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