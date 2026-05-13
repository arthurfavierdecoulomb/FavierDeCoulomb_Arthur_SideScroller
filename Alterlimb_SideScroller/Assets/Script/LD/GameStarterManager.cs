using UnityEngine;

/// <summary>
/// Déclenche l'intro de démarrage du jeu sur le premier niveau.
/// À placer sur un GameObject dans la scène (ex: "GameStarter").
/// 
/// Le LevelData référencé est celui du premier niveau (LD_Usine).
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Header("Niveau de démarrage")]
    [Tooltip("LevelData du premier niveau (généralement LD_Usine)")]
    [SerializeField] LevelData firstLevel;

    void Start()
    {
        if (firstLevel == null)
        {
            Debug.LogError("[GameStarter] Aucun LevelData assigné !");
            return;
        }
        if (LevelTransitionManager.Instance == null)
        {
            Debug.LogError("[GameStarter] LevelTransitionManager introuvable dans la scène !");
            return;
        }

        LevelTransitionManager.Instance.StartIntro(firstLevel);
    }
}