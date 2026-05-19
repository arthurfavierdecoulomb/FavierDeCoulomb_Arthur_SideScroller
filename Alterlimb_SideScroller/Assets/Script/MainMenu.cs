using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu principal du jeu.
/// 
/// Gère les deux boutons :
///   - Jouer  : charge la scène de jeu
///   - Quitter : ferme l'application
/// 
/// La musique de fond est jouée par un AudioSource de la scène Menu.
/// Elle s'arrête naturellement quand la scène Menu est déchargée
/// (au chargement de la scène de jeu).
/// 
/// 
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scène de jeu")]
    [Tooltip("Nom EXACT de la scène de jeu à charger (doit être dans le Build Settings)")]
    [SerializeField] string gameSceneName = "Jeu";

    /// <summary>
    /// Appelé par le bouton "Jouer". Charge la scène de jeu.
    /// </summary>
    public void Jouer()
    {
        Debug.Log($"[MainMenu] Chargement de la scène : {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Appelé par le bouton "Quitter". Ferme le jeu.
    /// Note : Application.Quit() n'a aucun effet dans l'éditeur Unity,
    /// il ne fonctionne que dans un build exporté. Le log permet de vérifier
    /// que le bouton marche pendant les tests dans l'éditeur.
    /// </summary>
    public void Quitter()
    {
        Debug.Log("[MainMenu] Quitter le jeu");
        Application.Quit();
    }
}
