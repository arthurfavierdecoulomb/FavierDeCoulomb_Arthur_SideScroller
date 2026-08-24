using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;


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

    [Header("Animation")]
    [SerializeField] PanelAnimator pauseAnimator;
    [SerializeField] PanelAnimator settingsAnimator;

    bool isPaused;

    void Start()
    {
        
        if (pausePanel != null) pausePanel.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (!KeyBindings.GetDown(GameAction.Pause)) return;

        if (settingsAnimator != null && settingsAnimator.IsOpen)
        {
            settingsAnimator.Close();
            return;
        }

        if (pauseAnimator.IsOpen) Resume();
        else Pause();
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        pauseAnimator.Open();
        LevelMusicPlayer.Instance.MuffleMusic();
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        pauseAnimator.Close();
        LevelMusicPlayer.Instance.UnmuffleMusic();
    }


    void RefreshStats()
    {
        if (GameStats.Instance == null) return;

        if (timeText != null)
            timeText.text = GameStats.Instance.GetFormattedTime();

        if (deathText != null)
            deathText.text = GameStats.Instance.DeathCount.ToString();
    }




    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }


    public void Quitter()
    {
        Time.timeScale = 1f;
        Debug.Log("[PauseMenu] Quitter le jeu");
        Application.Quit();
    }
}