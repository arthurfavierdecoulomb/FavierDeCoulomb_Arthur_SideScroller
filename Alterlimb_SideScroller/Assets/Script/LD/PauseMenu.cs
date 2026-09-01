using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] PanelAnimator pauseAnimator;
    [SerializeField] PanelAnimator settingsAnimator;

    [Header("Navigation")]
    [SerializeField] string menuSceneName = "Menu";

    [Header("Verrou")]
    [SerializeField] bool respectPauseLock = true;

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
        if (respectPauseLock && PauseLock.IsLocked) return;
        if (pauseAnimator.IsOpen) return;

        CameraShake.CancelHitStop();

        if (PauseAudioManager.Instance != null)
            PauseAudioManager.Instance.EnterPause();

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

        if (PauseAudioManager.Instance != null)
            PauseAudioManager.Instance.ExitPause();
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

        if (PauseAudioManager.Instance != null)
            PauseAudioManager.Instance.ExitPause();

        PauseLock.UnlockAll();

        if (PauseMusicPlayer.Instance != null)
            PauseMusicPlayer.Instance.StopImmediate();

        if (LevelMusicPlayer.Instance != null)
            LevelMusicPlayer.Instance.FadeOut(0.3f);
    }
}