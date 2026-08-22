using UnityEngine;

public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    float elapsedTime;
    int deathCount;
    float levelStartTime;
    int levelStartDeaths;

    public float ElapsedTime => elapsedTime;
    public int DeathCount => deathCount;
    public float LevelTime => elapsedTime - levelStartTime;
    public int LevelDeaths => deathCount - levelStartDeaths;

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
        elapsedTime += Time.deltaTime;
    }

    public void AddDeath()
    {
        deathCount++;
    }

    public void MarkLevelStart()
    {
        levelStartTime = elapsedTime;
        levelStartDeaths = deathCount;
    }

    public void ResetStats()
    {
        elapsedTime = 0f;
        deathCount = 0;
        levelStartTime = 0f;
        levelStartDeaths = 0;
    }

    public string GetFormattedTime()
    {
        int totalSeconds = Mathf.FloorToInt(elapsedTime);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours:00}:{minutes:00}:{seconds:00}";

        return $"{minutes:00}:{seconds:00}";
    }
}