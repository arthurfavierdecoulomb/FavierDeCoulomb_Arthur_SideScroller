using UnityEngine;

/// <summary>
/// Tient les statistiques de la partie en cours :
///   - le temps total écoulé depuis le début du jeu
///   - le nombre de morts du joueur
/// 
/// Le chrono tourne en continu (Update). Il se met en pause automatiquement
/// quand le jeu est figé (Time.timeScale = 0), car il utilise Time.deltaTime
/// qui vaut 0 quand le jeu est en pause — donc le temps de pause n'est pas
/// compté. C'est le comportement voulu : on chronomètre le temps de JEU réel.
/// 
/// Accès global via GameStats.Instance.
/// 
/// Setup :
///   - Un GameObject permanent de la scène de jeu (ex: "GameStats").
/// </summary>
public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    float elapsedTime;
    int deathCount;

    /// <summary>Temps total de jeu écoulé, en secondes.</summary>
    public float ElapsedTime => elapsedTime;

    /// <summary>Nombre de morts du joueur depuis le début.</summary>
    public int DeathCount => deathCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        // Time.deltaTime vaut 0 quand le jeu est en pause (timeScale = 0),
        // donc le chrono se fige automatiquement pendant la pause.
        elapsedTime += Time.deltaTime;
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>Ajoute une mort au compteur. À appeler à chaque mort du joueur.</summary>
    public void AddDeath()
    {
        deathCount++;
    }

    /// <summary>Remet les stats à zéro (ex: nouvelle partie).</summary>
    public void ResetStats()
    {
        elapsedTime = 0f;
        deathCount = 0;
    }

    /// <summary>
    /// Formate le temps en texte lisible "MM:SS" (ou "HH:MM:SS" si > 1h).
    /// </summary>
    public string GetFormattedTime()
    {
        int totalSeconds = Mathf.FloorToInt(elapsedTime);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        else
            return $"{minutes:00}:{seconds:00}";
    }
}