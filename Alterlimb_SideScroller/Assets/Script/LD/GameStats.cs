using UnityEngine;

public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    float elapsedTime;
    int deathCount;
    float levelStartTime;
    int levelStartDeaths;
    bool frozen;

    public float ElapsedTime => elapsedTime;
    public int DeathCount => deathCount;
    public float LevelTime => elapsedTime - levelStartTime;
    public int LevelDeaths => deathCount - levelStartDeaths;
    public bool IsFrozen => frozen;

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
        if (frozen) return;

        elapsedTime += Time.deltaTime;
    }

    public void AddDeath()
    {
        if (frozen) return;

        deathCount++;
    }

    public void Freeze()
    {
        frozen = true;
    }

    public void Unfreeze()
    {
        frozen = false;
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
        frozen = false;
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