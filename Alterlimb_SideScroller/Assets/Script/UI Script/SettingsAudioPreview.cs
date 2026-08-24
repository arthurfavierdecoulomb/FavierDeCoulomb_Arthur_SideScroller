using UnityEngine;

public class SettingsAudioPreview : MonoBehaviour
{
    [Header("Comportement")]
    [SerializeField] bool unmuffleWhileOpen = true;

    bool unmuffled;

    void OnEnable()
    {
        if (!unmuffleWhileOpen) return;
        if (LevelMusicPlayer.Instance == null) return;

        LevelMusicPlayer.Instance.UnmuffleMusic();
        unmuffled = true;
    }

    void OnDisable()
    {
        if (!unmuffled) return;
        unmuffled = false;

        if (LevelMusicPlayer.Instance == null) return;
        if (Time.timeScale > 0.0001f) return;

        LevelMusicPlayer.Instance.MuffleMusic();
    }
}