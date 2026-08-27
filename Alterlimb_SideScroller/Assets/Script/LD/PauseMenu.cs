using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] PanelAnimator pauseAnimator;
    [SerializeField] PanelAnimator settingsAnimator;

    [Header("Navigation")]
    [SerializeField] string menuSceneName = "Menu";

    void Start()
    {
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

        if (PauseMusicPlayer.Instance != null)
            PauseMusicPlayer.Instance.EnterPause();
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        pauseAnimator.Close();

        if (PauseMusicPlayer.Instance != null)
            PauseMusicPlayer.Instance.ExitPause();
    }

    public void ReturnToMenu()
    {
        LeaveLevel();
        SceneManager.LoadScene(menuSceneName);
    }

    public void Quitter()
    {
        LeaveLevel();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LeaveLevel()
    {
        Time.timeScale = 1f;

        if (PauseMusicPlayer.Instance != null)
            PauseMusicPlayer.Instance.StopImmediate();

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(0.3f);
    }
}