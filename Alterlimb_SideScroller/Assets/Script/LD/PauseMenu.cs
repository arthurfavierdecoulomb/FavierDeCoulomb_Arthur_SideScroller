using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Écran de pause du jeu.
/// 
/// Déclenché par la touche Échap. Fige le jeu (Time.timeScale = 0) et affiche
/// un panneau : logo + 3 boutons (Reprendre, Retour au menu, Quitter) + les
/// statistiques de la partie (temps total et nombre de morts).
/// 
/// IMPORTANT : Time.timeScale = 0 fige TOUT le jeu (physique, animations,
/// ennemis...). Il est remis à 1 quand on reprend ou qu'on quitte la scène.
/// 
/// Setup :
///   - Un panneau UI "PausePanel" (logo + boutons + textes), caché au départ.
///   - Ce script sur un GameObject de la scène.
///   - Brancher les 3 boutons sur Reprendre() / RetourMenu() / Quitter().
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Touche de pause")]
    [SerializeField] KeyCode pauseKey = KeyCode.Escape;

    [Header("UI")]
    [Tooltip("Le panneau de pause (logo + boutons + stats). Caché quand le jeu tourne.")]
    [SerializeField] GameObject pausePanel;
    [Tooltip("Texte affichant le temps de jeu")]
    [SerializeField] TextMeshProUGUI timeText;
    [Tooltip("Texte affichant le nombre de morts")]
    [SerializeField] TextMeshProUGUI deathText;

    [Header("Navigation")]
    [Tooltip("Nom EXACT de la scène du menu principal (dans le Build Settings)")]
    [SerializeField] string menuSceneName = "Menu";

    bool isPaused;

    void Start()
    {
        // Sécurité : le jeu démarre toujours non-pausé
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused) Reprendre();
            else Pause();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Pause / Reprise
    // ════════════════════════════════════════════════════════════

    /// <summary>Met le jeu en pause et affiche le panneau.</summary>
    void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;  // fige tout le jeu

        if (pausePanel != null) pausePanel.SetActive(true);

        RefreshStats();
    }

    /// <summary>
    /// Reprend le jeu. Appelé par le bouton "Reprendre" et par la touche Échap.
    /// </summary>
    public void Reprendre()
    {
        isPaused = false;
        Time.timeScale = 1f;  // redémarre le jeu

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    /// <summary>
    /// Met à jour les textes de statistiques affichés sur le panneau.
    /// </summary>
    void RefreshStats()
    {
        if (GameStats.Instance == null) return;

        if (timeText != null)
            timeText.text = GameStats.Instance.GetFormattedTime();

        if (deathText != null)
            deathText.text = GameStats.Instance.DeathCount.ToString();
    }

    // ════════════════════════════════════════════════════════════
    //  Actions des boutons
    // ════════════════════════════════════════════════════════════

    /// <summary>Bouton "Retour au menu principal".</summary>
    public void RetourMenu()
    {
        // IMPORTANT : remettre timeScale à 1 avant de changer de scène,
        // sinon la scène du menu démarrerait figée.
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>Bouton "Quitter le jeu".</summary>
    public void Quitter()
    {
        Time.timeScale = 1f;
        Debug.Log("[PauseMenu] Quitter le jeu");
        Application.Quit();
    }
}