using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("Animations")]
    [SerializeField] PanelAnimator mainMenuAnimator;
    [SerializeField] PanelAnimator difficultyPopupAnimator;

    [Header("Lancement de la partie")]
    [SerializeField] string gameSceneName = "Jeu";
    [SerializeField] string tutorialPrefsKey = "TutorialEnabled";

    bool starting;

    void Awake()
    {
        if (mainMenuAnimator == null)
            Debug.LogError($"{name}: aucune animations de present");

        if (difficultyPopupAnimator == null)
            Debug.LogError($"{name}: aucune animation de difficulté");

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            Debug.LogError($"{name}: la scène '{gameSceneName}' n'est pas dans les Build Settings, la partie ne pourra pas démarrer.", this);
    }

    public void OnPlayClicked()
    {
        if (starting) return;

        mainMenuAnimator.Close();
        difficultyPopupAnimator.Open();
    }

    public void OnClosePopupClicked()
    {
        if (starting) return;

        difficultyPopupAnimator.Close();
        mainMenuAnimator.Open();
    }

    public void OnBeginnerSelected()
    {
        StartGame(true);
    }

    public void OnQualifiedSelected()
    {
        StartGame(false);
    }

    public void OnQuitClicked()
    {
        if (starting) return;

        Debug.Log("[MainMenuController] Quitter le jeu");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void StartGame(bool tutorialEnabled)
    {
        if (starting || SceneLoader.IsLoading) return;

        starting = true;

        PlayerPrefs.SetInt(tutorialPrefsKey, tutorialEnabled ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[MainMenuController] Lancement de '{gameSceneName}', tutoriel {(tutorialEnabled ? "activé" : "désactivé")}.");
        SceneLoader.Load(gameSceneName);
    }
}