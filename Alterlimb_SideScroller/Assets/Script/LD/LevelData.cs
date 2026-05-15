using UnityEngine;

/// <summary>
/// Données d'un niveau du jeu. Asset créé via Create → Alterlimb → Level Data.
/// Référencé par les TransitionZone et le LevelTransitionManager.
/// 
/// Convention de nommage des assets : LD_Usine, LD_Metro, LD_Noyaux
/// </summary>
[CreateAssetMenu(fileName = "LD_NewLevel", menuName = "Alterlimb/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Identité du niveau")]
    [Tooltip("Nom affiché à l'écran pendant la transition (ex: USINE, METRO, NOYAU CENTRAL)")]
    public string levelName = "NIVEAU";

    [Tooltip("Description / objectif affiché sous le titre")]
    [TextArea(2, 4)]
    public string levelDescription = "Description du niveau";

    [Header("Position de spawn")]
    [Tooltip("Position de téléportation du joueur à l'entrée de ce niveau")]
    public Vector2 spawnPosition;

    [Tooltip("Point où la course automatique d'arrivée se termine (le joueur reprend le contrôle ici)")]
    public Vector2 autoRunEndPosition;

    [Header("Direction de la course de sortie")]
    [Tooltip("Direction de la course automatique APRÈS le téléport (+1 = droite, -1 = gauche)")]
    [Range(-1f, 1f)]
    public float exitRunDirection = 1f;

    [Header("Audio (optionnel)")]
    public AudioClip ambientMusic;
}