using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// GUI du compteur de fusibles (HUD du niveau Metro) avec animation de récolte.
/// 
/// Au ramassage d'un fusible :
///   - Une icône de fusible apparaît au CENTRE de l'écran et bounce.
///   - Courte pause.
///   - L'icône vole vers le compteur (en haut à gauche).
///   - À l'impact, le compteur affiché s'incrémente.
/// 
/// Les récoltes rapprochées sont mises en FILE D'ATTENTE : les animations
/// se jouent l'une après l'autre, le compteur ne saute jamais.
/// 
/// Le GUI apparaît en flicker au premier fusible, disparaît en flicker
/// quand tous les fusibles sont installés.
/// 
/// 
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FuseGUI : MonoBehaviour
{
    [Header("Références texte")]
    [SerializeField] TextMeshProUGUI fuseCountText;
    [SerializeField] TextMeshProUGUI onFuseCountText;

    [Header("Icône volante")]
    [Tooltip("L'icône de fusible qui vole du centre vers le compteur (Image UI, désactivée au départ)")]
    [SerializeField] RectTransform flyingIcon;
    [Tooltip("Point cible de l'icône : le RectTransform du compteur (ex: FuseCount lui-même)")]
    [SerializeField] RectTransform iconTarget;

    [Header("Animation — Bounce au centre")]
    [Tooltip("Échelle maximale de l'icône pendant le bounce")]
    [SerializeField] float bounceScale = 1.4f;
    [Tooltip("Durée du bounce au centre")]
    [SerializeField] float bounceDuration = 0.4f;
    [Tooltip("Pause après le bounce, avant l'envol")]
    [SerializeField] float pauseDuration = 0.25f;

    [Header("Animation — Vol vers le compteur")]
    [Tooltip("Durée du vol du centre vers le compteur")]
    [SerializeField] float flyDuration = 0.5f;
    [Tooltip("Hauteur de l'arc du vol (0 = ligne droite)")]
    [SerializeField] float flyArcHeight = 80f;

    [Header("Flicker du GUI")]
    [SerializeField] float flickerDuration = 0.5f;
    [SerializeField] float flickerMinInterval = 0.04f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    CanvasGroup canvasGroup;
    bool isVisible;
    Coroutine flickerCoroutine;

    // Compteur AFFICHÉ (différent de la donnée réelle : il rattrape au rythme des anims)
    int displayedCount;

    // File d'attente des animations de récolte
    readonly Queue<int> pickupQueue = new Queue<int>();
    bool isPlayingPickup;

    // Position centrale de l'écran (en coordonnées du Canvas)
    Vector2 screenCenter;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        isVisible = false;

        if (flyingIcon != null)
            flyingIcon.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        FuseManager.OnAllFusesInstalledStatic += HandleAllFusesInstalled;

        if (FuseManager.Instance != null)
            SubscribeToManager();
        else
            StartCoroutine(WaitForManager());
    }

    void OnDisable()
    {
        FuseManager.OnAllFusesInstalledStatic -= HandleAllFusesInstalled;

        if (FuseManager.Instance != null)
        {
            FuseManager.Instance.OnFuseCollected -= HandleFuseCollected;
            FuseManager.Instance.OnFuseInstalled -= HandleFuseInstalled;
        }
    }

    IEnumerator WaitForManager()
    {
        while (FuseManager.Instance == null)
            yield return null;
        SubscribeToManager();
    }

    void SubscribeToManager()
    {
        FuseManager.Instance.OnFuseCollected += HandleFuseCollected;
        FuseManager.Instance.OnFuseInstalled += HandleFuseInstalled;

        if (onFuseCountText != null)
            onFuseCountText.text = $"/{FuseManager.Instance.TotalFuses}";

        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();
    }

    // ════════════════════════════════════════════════════════════
    //  Réactions aux événements
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Ramassage d'un fusible : on met une animation de récolte en file d'attente.
    /// Le compteur affiché ne bougera qu'à l'impact de l'icône.
    /// </summary>
    void HandleFuseCollected()
    {
        pickupQueue.Enqueue(1);

        if (!isPlayingPickup)
            StartCoroutine(ProcessPickupQueue());
    }

    /// <summary>
    /// Insertion d'un fusible au panneau : le compteur "en main" descend.
    /// Pas d'animation volante ici, c'est une décrémentation directe.
    /// </summary>
    void HandleFuseInstalled()
    {
        displayedCount = Mathf.Max(0, displayedCount - 1);
        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();
    }

    void HandleAllFusesInstalled()
    {
        StartFlicker(appearing: false);
    }

    // ════════════════════════════════════════════════════════════
    //  File d'attente des animations de récolte
    // ════════════════════════════════════════════════════════════

    IEnumerator ProcessPickupQueue()
    {
        isPlayingPickup = true;

        while (pickupQueue.Count > 0)
        {
            pickupQueue.Dequeue();
            yield return StartCoroutine(PlayPickupAnimation());
        }

        isPlayingPickup = false;
    }

    // ════════════════════════════════════════════════════════════
    //  L'animation de récolte : bounce → pause → vol → impact
    // ════════════════════════════════════════════════════════════

    IEnumerator PlayPickupAnimation()
    {
        // Si le GUI n'est pas encore visible, on le fait apparaître d'abord.
        // On yield SUR la coroutine de flicker pour garantir que l'alpha
        // est bien posé à 1 avant que l'animation ne démarre.
        if (!isVisible)
        {
            isVisible = true; // set tout de suite pour éviter une 2e entrée
            if (flickerCoroutine != null) StopCoroutine(flickerCoroutine);
            yield return StartCoroutine(FlickerRoutine(appearing: true));
        }

        // Sécurité : on force l'alpha à 1 au cas où.
        canvasGroup.alpha = 1f;

        if (flyingIcon == null || iconTarget == null)
        {
            // Sécurité : pas d'icône configurée → on incrémente direct
            IncrementDisplayedCount();
            yield break;
        }

        // Calcul de la position centrale de l'écran
        screenCenter = Vector2.zero; // l'icône doit avoir ses anchors au centre (0.5, 0.5)

        // ── Préparation : icône au centre, échelle 0 ──
        flyingIcon.gameObject.SetActive(true);
        flyingIcon.anchoredPosition = screenCenter;
        flyingIcon.localScale = Vector3.zero;

        // ── Phase 1 : Bounce d'apparition au centre ──
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;
            // Overshoot : grossit au-delà de 1 puis revient à 1
            float scale = BounceScale(t);
            flyingIcon.localScale = Vector3.one * scale;

            elapsed += Time.deltaTime;
            yield return null;
        }
        flyingIcon.localScale = Vector3.one;

        // ── Phase 2 : Courte pause ──
        yield return new WaitForSeconds(pauseDuration);

        // ── Phase 3 : Vol vers le compteur (avec arc) ──
        Vector2 startPos = flyingIcon.anchoredPosition;
        Vector2 endPos = GetTargetAnchoredPosition();

        elapsed = 0f;
        while (elapsed < flyDuration)
        {
            float t = elapsed / flyDuration;
            // Ease-in : démarre doucement, accélère
            float easedT = t * t;

            // Interpolation linéaire + arc vertical (parabole)
            Vector2 linearPos = Vector2.Lerp(startPos, endPos, easedT);
            float arc = Mathf.Sin(easedT * Mathf.PI) * flyArcHeight;
            flyingIcon.anchoredPosition = linearPos + Vector2.up * arc;

            // L'icône rétrécit légèrement en approchant
            flyingIcon.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, easedT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Phase 4 : Impact → incrément du compteur ──
        flyingIcon.gameObject.SetActive(false);
        IncrementDisplayedCount();

        // (Optionnel : petit punch d'échelle sur le texte du compteur ici)
    }

    /// <summary>Courbe de bounce : overshoot puis retour à 1.</summary>
    float BounceScale(float t)
    {
        // Monte vite jusqu'à bounceScale, puis redescend à 1 avec un léger rebond
        float overshoot = Mathf.Sin(t * Mathf.PI) * (bounceScale - 1f);
        return 1f + overshoot;
    }

    /// <summary>
    /// Convertit la position du compteur cible en coordonnées d'ancrage
    /// relatives au parent de l'icône volante.
    /// </summary>
    Vector2 GetTargetAnchoredPosition()
    {
        // On convertit la position monde du compteur en position locale
        // dans le repère du parent de l'icône volante.
        RectTransform iconParent = flyingIcon.parent as RectTransform;
        if (iconParent == null) return Vector2.zero;

        Vector2 worldPos = iconTarget.position;
        Vector2 localPos = iconParent.InverseTransformPoint(worldPos);
        return localPos;
    }

    void IncrementDisplayedCount()
    {
        displayedCount++;
        if (fuseCountText != null)
            fuseCountText.text = displayedCount.ToString();
    }

    // ════════════════════════════════════════════════════════════
    //  Flicker du GUI
    // ════════════════════════════════════════════════════════════

    void StartFlicker(bool appearing)
    {
        if (flickerCoroutine != null) StopCoroutine(flickerCoroutine);
        flickerCoroutine = StartCoroutine(FlickerRoutine(appearing));
    }

    IEnumerator FlickerRoutine(bool appearing)
    {
        isVisible = appearing;

        float elapsed = 0f;
        bool on = false;

        while (elapsed < flickerDuration)
        {
            on = !on;
            canvasGroup.alpha = on ? 1f : 0f;

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        canvasGroup.alpha = appearing ? 1f : 0f;
        flickerCoroutine = null;
    }
}