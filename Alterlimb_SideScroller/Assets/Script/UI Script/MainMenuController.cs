using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Animations")]
    [SerializeField] PanelAnimator mainMenuAnimator;
    [SerializeField] PanelAnimator difficultyPopupAnimator;

    [Header("Lancement de la partie")]
    [SerializeField] string gameSceneName = "Usine";
    [SerializeField] string tutorialPrefsKey = "TutorialEnabled";

    void Awake()
    {
        if (mainMenuAnimator == null)
            Debug.LogError($"{name}: aucune animations de present");

        if (difficultyPopupAnimator == null)
            Debug.LogError($"{name}: aucune animation de difficulté");
    }

    public void OnPlayClicked()
    {
        mainMenuAnimator.Close();
        difficultyPopupAnimator.Open();
    }

    public void OnClosePopupClicked()
    {
        difficultyPopupAnimator.Close();
        mainMenuAnimator.Open();
    }

    public void OnBeginnerSelected()
    {
        StartGame(false);
    }

    public void OnQualifiedSelected()
    {
        StartGame(true);
    }

    void StartGame(bool tutorialEnabled)
    {
        PlayerPrefs.SetInt(tutorialPrefsKey, tutorialEnabled ? 1 : 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }
}