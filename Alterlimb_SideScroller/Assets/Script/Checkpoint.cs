using UnityEngine;
using TMPro;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] int spawnIndex;
    [SerializeField] bool rechargeDoubleJump = false;

    [Header("Feedback visuel")]
    [SerializeField] TextMeshProUGUI feedbackText;
    [SerializeField] string message = "Spawnpoint enregistré";
    [SerializeField] float floatAmplitude = 8f;      // en pixels (UI)
    [SerializeField] float floatSpeed = 1.2f;        // vitesse du flottement
    [SerializeField] float fadeInDuration = 0.6f;
    [SerializeField] float displayDuration = 2.5f;
    [SerializeField] float fadeOutDuration = 1f;

    bool activated = false;
    bool showText = false;
    float showTimer = 0f;
    Vector3 textBasePosition;

    void Start()
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            textBasePosition = feedbackText.rectTransform.anchoredPosition;
            SetTextAlpha(0f);
        }
    }

    void Update()
    {
        if (feedbackText == null || !showText) return;

        showTimer += Time.deltaTime;

        // Flottement vertical smooth (sinus)
        float offsetY = Mathf.Sin(showTimer * floatSpeed * Mathf.PI) * floatAmplitude;
        feedbackText.rectTransform.anchoredPosition =
            (Vector2)textBasePosition + new Vector2(0f, offsetY);

        // Fade in / hold / fade out
        float alpha;
        if (showTimer < fadeInDuration)
        {
            alpha = Mathf.SmoothStep(0f, 1f, showTimer / fadeInDuration);
        }
        else if (showTimer < fadeInDuration + displayDuration)
        {
            alpha = 1f;
        }
        else if (showTimer < fadeInDuration + displayDuration + fadeOutDuration)
        {
            float t = (showTimer - fadeInDuration - displayDuration) / fadeOutDuration;
            alpha = Mathf.SmoothStep(1f, 0f, t);
        }
        else
        {
            alpha = 0f;
            showText = false;
        }

        SetTextAlpha(alpha);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        activated = true;
        SpawnManager.Instance.SetSpawnPoint(spawnIndex);

        if (rechargeDoubleJump)
        {
            CharaController chara = other.GetComponent<CharaController>();
            if (chara != null) chara.ResetJumps();
        }

        // Déclenche le feedback texte
        if (feedbackText != null)
        {
            showText = true;
            showTimer = 0f;
        }
    }

    void SetTextAlpha(float a)
    {
        Color c = feedbackText.color;
        c.a = a;
        feedbackText.color = c;
    }
}