using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UISlideIn : MonoBehaviour
{
    public enum SlideDirection { FromTop, FromBottom, FromLeft, FromRight }

    [Header("Direction d'arrivée")]
    [SerializeField] SlideDirection direction = SlideDirection.FromTop;
    [SerializeField] float slideDistance = 600f;

    [Header("Timing")]
    [SerializeField] float startDelay = 0f;
    [SerializeField] float duration = 0.6f;

    [Header("Courbe")]
    [Range(1f, 6f)]
    [SerializeField] float easeStrength = 3f;

    [Header("Bouton (optionnel)")]
    [SerializeField] bool disableButtonDuringAnim = true;

    RectTransform rect;
    Button button;
    Vector2 finalPosition;
    Vector2 startPosition;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();

        finalPosition = rect.anchoredPosition;
        startPosition = finalPosition + GetOffset();
        rect.anchoredPosition = startPosition;

        if (disableButtonDuringAnim && button != null)
            button.interactable = false;
    }

    void OnEnable()
    {
        StartCoroutine(SlideRoutine());
    }

    Vector2 GetOffset()
    {
        switch (direction)
        {
            case SlideDirection.FromTop: return Vector2.up * slideDistance;
            case SlideDirection.FromBottom: return Vector2.down * slideDistance;
            case SlideDirection.FromLeft: return Vector2.left * slideDistance;
            case SlideDirection.FromRight: return Vector2.right * slideDistance;
            default: return Vector2.zero;
        }
    }

    IEnumerator SlideRoutine()
    {
        rect.anchoredPosition = startPosition;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(startPosition, finalPosition, EaseOut(t));
            yield return null;
        }

        rect.anchoredPosition = finalPosition;

        if (disableButtonDuringAnim && button != null)
            button.interactable = true;
    }

    float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, easeStrength);
    }
}