using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Écran de chargement")]
    [SerializeField] private CanvasGroup loadingScreen;
    [SerializeField] private Image progressBar;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float minimumDisplayTime = 0.5f;

    [Header("Texte d'importation")]
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private string statusFormat = "importation {0}";
    [SerializeField] private string finishedText = "importation terminée";
    [SerializeField]
    private List<string> importNames = new List<string>
    {
        "des textures",
        "des script",
        "d'Azu",
        "des item pour Azu",
        "des niveaux, il y en a trois c'est beaucoup ",
        "des pièges",
        "des plateformes et ascenseurs",
        "des wagons",
        "des portes et des leviers",
        "de l'apparition d'Oxi-O",
        "des dialogues à Oxi-O, il a une mauvaise connexion internet",
        "des dialogues",
        "des cinématiques",
        "des ennemis",
        "des structures",
        "des sons ambiants",
        "des SFX",
        "d'un virus (je rigole...)",
        "des animations des differentes sprites",
        "des interfaces",
        "des musiques du jeu",
        "des boutons",
        "des dernières finalisations",
        "...",

    };

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public static bool IsLoading => Instance != null && Instance.loading;

    private bool loading;
    private int shownStatusIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetScreen(0f, false);

        if (loadingScreen == null)
            Debug.LogWarning($"[SceneLoader] '{name}' : Loading Screen vide, le chargement se fera sans écran.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] Impossible de charger '{sceneName}'. Vérifie le nom et ajoute la scène aux Build Settings.");
            return;
        }

        if (Instance == null)
        {
            Debug.LogWarning($"[SceneLoader] Aucun SceneLoader dans la scène, '{sceneName}' est chargée en asynchrone sans écran de chargement.");
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(sceneName);
            return;
        }

        if (Instance.loading)
            return;

        Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        loading = true;
        Time.timeScale = 1f;

        float started = Time.unscaledTime;
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        shownStatusIndex = -1;
        SetProgress(0f);
        SetScreen(0f, true);

        yield return Fade(0f, 1f);

        if (logDiagnostics)
            Debug.Log($"[SceneLoader] Chargement de '{sceneName}'.", this);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            SetProgress(operation.progress / 0.9f);
            yield return null;
        }

        SetProgress(1f);

        float remaining = minimumDisplayTime - (Time.unscaledTime - started);

        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        Application.backgroundLoadingPriority = previousPriority;

        if (logDiagnostics)
            Debug.Log($"[SceneLoader] '{sceneName}' chargée en {Time.unscaledTime - started:0.0}s.", this);

        yield return Fade(1f, 0f);

        SetScreen(0f, false);
        loading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (loadingScreen == null)
            yield break;

        if (fadeDuration <= 0f)
        {
            loadingScreen.alpha = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingScreen.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        loadingScreen.alpha = to;
    }

    private void SetScreen(float alpha, bool blocking)
    {
        if (loadingScreen == null)
            return;

        loadingScreen.alpha = alpha;
        loadingScreen.interactable = false;
        loadingScreen.blocksRaycasts = blocking;
    }

    private void SetProgress(float value)
    {
        float progress = Mathf.Clamp01(value);

        if (progressBar != null)
            progressBar.fillAmount = progress;

        UpdateStatus(progress);
    }

    private void UpdateStatus(float progress)
    {
        if (statusLabel == null)
            return;

        if (progress >= 1f || importNames == null || importNames.Count == 0)
        {
            if (shownStatusIndex != int.MaxValue)
            {
                shownStatusIndex = int.MaxValue;
                statusLabel.text = progress >= 1f ? finishedText : "";
            }

            return;
        }

        int index = Mathf.Clamp(Mathf.FloorToInt(progress * importNames.Count), 0, importNames.Count - 1);

        if (index == shownStatusIndex)
            return;

        shownStatusIndex = index;
        statusLabel.text = string.Format(statusFormat, importNames[index]);
    }
}