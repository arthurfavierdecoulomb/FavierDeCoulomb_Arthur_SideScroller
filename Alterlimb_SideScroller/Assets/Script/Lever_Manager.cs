using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Levier interactif avec poignée rotative.
/// 
/// Comportement intelligent :
///   - Si le levier est lié à une Door → la poignée descend de -49° à +49° et reste verrouillée.
///     Notifie la porte via l'événement OnLeverActivated.
///   - Si le levier n'est lié à rien (faux levier) → la poignée descend à 0° puis remonte à -49°
///     avec un effet de bounce, sans verrouillage.
/// 
/// L'effet bouncing utilise un easing "EaseOutBack" pour un feedback satisfaisant.
/// </summary>
public class Lever : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Référence")]
    [Tooltip("Transform de la poignée à faire tourner (pas le levier entier)")]
    [SerializeField] Transform handle;

    [Header("Rotation")]
    [Tooltip("Angle de départ de la poignée (état non activé)")]
    [SerializeField] float restAngle = -49f;
    [Tooltip("Angle d'arrivée de la poignée (état activé)")]
    [SerializeField] float activeAngle = 49f;
    [Tooltip("Angle intermédiaire pour les faux leviers (descend puis remonte)")]
    [SerializeField] float fakeAngle = 0f;

    [Header("Durées d'animation")]
    [Tooltip("Durée d'activation (vrai levier)")]
    [SerializeField] float activationDuration = 0.6f;
    [Tooltip("Durée de la descente d'un faux levier")]
    [SerializeField] float fakeDownDuration = 0.3f;
    [Tooltip("Durée du retour à la position de repos d'un faux levier")]
    [SerializeField] float fakeUpDuration = 0.5f;

    [Header("Bounce")]
    [Tooltip("Force du rebond (0 = pas de bounce, 1.7 = rebond classique, plus = exagéré)")]
    [Range(0f, 3f)]
    [SerializeField] float bounceStrength = 1.7f;

    [Header("Interaction joueur")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1; // clic droit
    [SerializeField] string playerTag = "Player";


    [Header("Liaison porte (laisser vide = faux levier)")]
    [Tooltip("Porte que ce levier doit aider à ouvrir. Laisser vide = faux levier.")]
    [SerializeField] Door connectedDoor;

    [Header("Message si faux levier")]
    [Tooltip("Id du message TutorialManager affiché quand on actionne un faux levier. Vide = aucun message.")]
    [SerializeField] string brokenMessageId = "";

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Transform playerTransform;
    bool isAnimating;
    bool isActivated;

    /// <summary>True si le levier est en position activée (verrouillée)</summary>
    public bool IsActivated => isActivated;

    /// <summary>Événement déclenché quand le levier passe en état activé</summary>
    public event Action OnLeverActivated;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (handle == null) handle = transform;

        // Position de départ
        SetHandleAngle(restAngle);

        // Cache le joueur
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) playerTransform = p.transform;
    }

    // ════════════════════════════════════════════════════════════
    //  Update
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (isActivated || isAnimating || playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            TryActivate();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Activation
    // ════════════════════════════════════════════════════════════

    void TryActivate()
    {
        bool isRealLever = connectedDoor != null && connectedDoor.IsLeverRequired(this);

        if (isRealLever)
            StartCoroutine(ActivateRoutine());
        else
            StartCoroutine(FakeActivateRoutine());
    }

    /// <summary>Levier connecté à une porte : descend et reste verrouillé</summary>
    IEnumerator ActivateRoutine()
    {
        isAnimating = true;

        yield return RotateHandle(restAngle, activeAngle, activationDuration, useBounce: true);

        isActivated = true;
        isAnimating = false;

        // Notifie la porte
        OnLeverActivated?.Invoke();
    }

    /// <summary>Faux levier : descend partiellement puis remonte</summary>
    /// <summary>Faux levier : descend partiellement puis remonte. Affiche un message si configuré.</summary>
    IEnumerator FakeActivateRoutine()
    {
        isAnimating = true;

        // Affiche le message "levier cassé" si un id est configuré
        if (!string.IsNullOrEmpty(brokenMessageId) && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ShowMessageById(brokenMessageId);
        }

        // Descente sans bounce (rapide, sec)
        yield return RotateHandle(restAngle, fakeAngle, fakeDownDuration, useBounce: false);

        // Petit délai pour laisser au joueur le temps de "voir" l'échec
        yield return new WaitForSeconds(0.1f);

        // Remontée avec bounce (effet "boing")
        yield return RotateHandle(fakeAngle, restAngle, fakeUpDuration, useBounce: true);

        isAnimating = false;
    }

    // ════════════════════════════════════════════════════════════
    //  Animation de rotation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Anime la rotation de la poignée d'un angle à l'autre, avec ou sans rebond final.
    /// </summary>
    IEnumerator RotateHandle(float fromAngle, float toAngle, float duration, bool useBounce)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Easing : avec ou sans bounce
            float easedT = useBounce ? EaseOutBack(t, bounceStrength) : EaseOutQuad(t);
            float angle = Mathf.LerpUnclamped(fromAngle, toAngle, easedT);

            SetHandleAngle(angle);
            yield return null;
        }

        // Garantit la position finale exacte
        SetHandleAngle(toAngle);
    }

    void SetHandleAngle(float angle)
    {
        handle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ════════════════════════════════════════════════════════════
    //  Easing functions
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Easing avec dépassement (overshoot) puis retour vers la cible.
    /// Donne un effet de rebond satisfaisant en fin d'animation.
    /// </summary>
    static float EaseOutBack(float t, float strength)
    {
        float c1 = strength;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>Easing simple, rapide au début, ralentit en fin.</summary>
    static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Cercle de portée d'interaction
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Ligne vers la porte connectée (vert = vrai levier, rouge = faux)
        if (connectedDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, connectedDoor.transform.position);
        }
        else
        {
            // Indication faux levier
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
        }
    }
}