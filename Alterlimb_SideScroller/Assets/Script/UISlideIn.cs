using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Anime l'entrée d'un élément UI : il slide depuis un bord de l'écran
/// jusqu'à sa position finale, avec un rebond "ressort" qui oscille
/// plusieurs fois autour de la cible avant de se stabiliser (boing-oing-oing).
/// 
/// Utilise le même principe de sinus amorti que l'ElevatorPlatform :
/// l'élément atteint vite sa cible, puis oscille autour avec des oscillations
/// de plus en plus petites — formule sin(2π·count·t) · amortissement.
/// 
/// Composant réutilisable : à poser sur chaque élément à animer (titre,
/// boutons...). Le 'startDelay' permet de désynchroniser les apparitions.
/// 
/// La position actuelle de l'élément dans l'éditeur est sa position FINALE.
/// </summary>
public class UISlideIn : MonoBehaviour
{
    public enum SlideDirection { FromTop, FromBottom, FromLeft, FromRight }

    [Header("Direction d'arrivée")]
    [SerializeField] SlideDirection direction = SlideDirection.FromTop;
    [Tooltip("Distance parcourue depuis le point de départ hors écran")]
    [SerializeField] float slideDistance = 600f;

    [Header("Timing")]
    [Tooltip("Délai avant le début de l'animation (sert à désynchroniser les éléments)")]
    [SerializeField] float startDelay = 0f;
    [Tooltip("Durée totale de l'animation (slide + oscillations)")]
    [SerializeField] float duration = 0.7f;

    [Header("Rebond ressort (boing-oing-oing)")]
    [Tooltip("Nombre d'oscillations autour de la cible")]
    [Range(1, 6)]
    [SerializeField] int bounceCount = 3;
    [Tooltip("Amortissement : plus c'est haut, plus les oscillations s'éteignent vite")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.5f;

    [Header("Bouton (optionnel)")]
    [Tooltip("Si coché et qu'un Button est présent, il est non-cliquable pendant l'animation")]
    [SerializeField] bool disableButtonDuringAnim = true;

    RectTransform rect;
    Button button;

    Vector2 finalPosition;   // position d'arrivée (mémorisée au départ)
    Vector2 startPosition;   // position de départ (hors écran)

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

    /// <summary>Retourne le décalage de départ selon la direction choisie.</summary>
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
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Progression "ressort" : la valeur tend vers 1, mais oscille
            // autour avec des oscillations de plus en plus petites.
            float springT = SpringProgress(t);

            // LerpUnclamped car springT dépasse 1 pendant les oscillations
            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, finalPosition, springT);

            yield return null;
        }

        // Position finale exacte
        rect.anchoredPosition = finalPosition;

        if (disableButtonDuringAnim && button != null)
            button.interactable = true;
    }

    /// <summary>
    /// Courbe de progression "ressort". Pour t de 0 à 1, retourne une valeur
    /// qui démarre à 0, atteint vite ~1, puis oscille autour de 1 avec une
    /// amplitude qui décroît — jusqu'à se stabiliser exactement à 1.
    /// 
    /// C'est le même principe de sinus amorti que l'ElevatorPlatform :
    ///   - (1 - t) : l'élément se rapproche de la cible
    ///   - sin(2π·count·t) : les oscillations
    ///   - pow(1-t, ...) : l'amortissement qui éteint les oscillations
    /// </summary>
    float SpringProgress(float t)
    {
        // Approche de base vers la cible (1 - (1-t)) = t, lissé
        float approach = 1f - Mathf.Pow(1f - t, 2f);

        // Oscillation amortie autour de la cible
        float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
        float damping = Mathf.Pow(1f - t, 1f / Mathf.Max(0.01f, 1f - bounceDamping));

        // Le ressort = approche + l'oscillation amortie
        return approach + oscillation * damping;
    }
}