using UnityEngine;

[CreateAssetMenu(fileName = "LD_NewLevel", menuName = "Alterlimb/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Identité du niveau")]
    public string levelName = "NIVEAU";

    [TextArea(2, 4)]
    public string levelDescription = "Description du niveau";

    [Header("Position de spawn")]
    public Vector2 spawnPosition;
    public Vector2 autoRunEndPosition;

    [Header("Position de spawn (sans tutoriel)")]
    public bool hasNoTutorialSpawn;
    public Vector2 noTutorialSpawnPosition;

    [Header("Direction de la course de sortie")]
    [Range(-1f, 1f)]
    public float exitRunDirection = 1f;

    [Header("Audio (optionnel)")]
    public AudioClip ambientMusic;
}