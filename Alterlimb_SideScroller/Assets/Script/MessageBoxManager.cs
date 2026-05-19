using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gère les bulles de tutoriel et de message.
/// 
/// Deux sources de messages :
///   - ZONES de proximité (TutorialZone) : le joueur entre dans un rayon.
///   - MESSAGES À LA DEMANDE : d'autres scripts (Door, Lever...) appellent
///     ShowMessageById() avec l'id d'un message configuré dans 'messages'.
/// 
/// Animation : le bubblePanel slide depuis le haut avec un bounce d'overshoot.
/// La machine à écrire démarre une fois le slide d'entrée terminé.
/// 
/// Accès global via TutorialManager.Instance (singleton léger).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Types
    // ════════════════════════════════════════════════════════════

    [System.Serializable]
    public class TutorialZone
    {
        [Header("Zone de déclenchement")]
        public GameObject triggerObject;
        public float triggerRadius = 3f;

        [Header("Bulle de texte")]
        public string title = "";
        [TextArea(2, 6)]
        public string message = "";
        public Sprite icon;

        [Header("Comportement")]
        public float displayDuration = 0f;      // 0 = reste jusqu'à ce qu'on parte
        public bool showOnce = true;
        public bool requireKeyPress = false;
        public KeyCode dismissKey = KeyCode.E;

        [HideInInspector] public bool hasBeenShown = false;
        [HideInInspector] public bool isActive = false;
    }

    /// <summary>
    /// Un message réutilisable, identifié par un id texte.
    /// Les portes, leviers, etc. demandent l'affichage via cet id.
    /// </summary>
    [System.Serializable]
    public class TutorialMessage
    {
        [Tooltip("Identifiant unique (ex: 'porte_fusibles', 'levier_casse')")]
        public string id = "";
        public string title = "";
        [TextArea(2, 6)]
        public string message = "";
        public Sprite icon;
        [Tooltip("Durée d'affichage (0 = jusqu'à un nouveau message ou sortie de zone)")]
        public float displayDuration = 3f;
    }

    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("UI Refs")]
    [SerializeField] GameObject bubblePanel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI dismissHint;

    [Header("Joueur")]
    [SerializeField] Transform player;

    [Header("Zones tutoriel")]
    [SerializeField] List<TutorialZone> zones = new List<TutorialZone>();

    [Header("Messages à la demande (portes, leviers...)")]
    [Tooltip("Messages réutilisables, appelés par leur id via ShowMessageById()")]
    [SerializeField] List<TutorialMessage> messages = new List<TutorialMessage>();

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.04f;

    [Header("Animation slide + bounce")]
    [SerializeField] float hideOffsetY = 400f;
    [SerializeField] float slideInDuration = 0.45f;
    [SerializeField] float slideOutDuration = 0.25f;
    [SerializeField] float bounceAmplitude = 40f;
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.45f;

    // ════════════════════════════════════════════════════════════
    //  Runtime
    // ════════════════════════════════════════════════════════════

    CanvasGroup canvasGroup;
    RectTransform panelRect;

    Vector2 shownPosition;
    Vector2 hiddenPosition;

    TutorialZone currentZone = null;
    float displayTimer = 0f;
    Coroutine typewriterCoroutine = null;
    Coroutine slideCoroutine = null;

    bool isShowing;

    // Vrai si le message courant vient d'un appel à la demande (pas d'une zone)
    bool currentIsOnDemand = false;
    float onDemandTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogWarning("[TutorialManager] Joueur non trouvé !");
        }

        canvasGroup = bubblePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = bubblePanel.AddComponent<CanvasGroup>();

        panelRect = bubblePanel.GetComponent<RectTransform>();

        shownPosition = panelRect.anchoredPosition;
        hiddenPosition = shownPosition + Vector2.up * hideOffsetY;

        bubblePanel.SetActive(true);
        panelRect.anchoredPosition = hiddenPosition;
        SetInteractable(false);
        isShowing = false;
    }

    void Update()
    {
        if (player == null) return;

        CheckZones();
        HandleDismiss();
        HandleOnDemandTimer();
    }

    void SetInteractable(bool interactable)
    {
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    // ════════════════════════════════════════════════════════════
    //  API PUBLIQUE — messages à la demande
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Affiche un message depuis la liste 'messages', identifié par son id.
    /// Appelé par les portes verrouillées, leviers cassés, etc.
    /// </summary>
    public void ShowMessageById(string id)
    {
        TutorialMessage msg = messages.Find(m => m.id == id);

        if (msg == null)
        {
            Debug.LogWarning($"[TutorialManager] Aucun message avec l'id '{id}'.");
            return;
        }

        ShowOnDemandMessage(msg);
    }

    void ShowOnDemandMessage(TutorialMessage msg)
    {
        // Un message à la demande prend le dessus sur une zone éventuelle
        if (currentZone != null)
        {
            currentZone.isActive = false;
            currentZone = null;
        }

        currentIsOnDemand = true;
        onDemandTimer = msg.displayDuration;

        // Remplit les éléments
        if (titleText != null)
        {
            titleText.text = msg.title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(msg.title));
        }
        if (iconImage != null)
        {
            iconImage.sprite = msg.icon;
            iconImage.gameObject.SetActive(msg.icon != null);
        }
        if (dismissHint != null)
        {
            dismissHint.gameObject.SetActive(false);
        }
        if (messageText != null)
            messageText.text = "";

        // Lance le slide d'entrée
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideInRoutine(msg.message));
    }

    /// <summary>
    /// Gère la fermeture automatique d'un message à la demande après sa durée.
    /// </summary>
    void HandleOnDemandTimer()
    {
        if (!currentIsOnDemand) return;
        if (onDemandTimer <= 0f) return;  // 0 = pas de fermeture auto

        onDemandTimer -= Time.deltaTime;
        if (onDemandTimer <= 0f)
            HideCurrentMessage();
    }

    /// <summary>Ferme le message à la demande en cours.</summary>
    void HideCurrentMessage()
    {
        currentIsOnDemand = false;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideOutRoutine());
    }

    // ════════════════════════════════════════════════════════════
    //  Zones de proximité
    // ════════════════════════════════════════════════════════════

    void CheckZones()
    {
        // Si un message à la demande est affiché, les zones attendent leur tour
        if (currentIsOnDemand) return;

        foreach (TutorialZone zone in zones)
        {
            if (zone.showOnce && zone.hasBeenShown) continue;
            if (zone.triggerObject == null) continue;

            float dist = Vector2.Distance(player.position, zone.triggerObject.transform.position);
            bool inRange = dist <= zone.triggerRadius;

            if (inRange && !zone.isActive)
                ShowZone(zone);
            else if (!inRange && zone.isActive && !zone.requireKeyPress)
                HideZone(zone);
        }

        if (currentZone != null && currentZone.displayDuration > 0f)
        {
            displayTimer -= Time.deltaTime;
            if (displayTimer <= 0f)
                HideZone(currentZone);
        }
    }

    void HandleDismiss()
    {
        if (currentZone == null) return;
        if (!currentZone.requireKeyPress) return;

        if (Input.GetKeyDown(currentZone.dismissKey))
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
                messageText.text = currentZone.message;
                return;
            }

            HideZone(currentZone);
        }
    }

    void ShowZone(TutorialZone zone)
    {
        if (currentZone != null && currentZone != zone)
            currentZone.isActive = false;

        zone.isActive = true;
        zone.hasBeenShown = true;
        currentZone = zone;
        displayTimer = zone.displayDuration;

        if (titleText != null)
        {
            titleText.text = zone.title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(zone.title));
        }
        if (iconImage != null)
        {
            iconImage.sprite = zone.icon;
            iconImage.gameObject.SetActive(zone.icon != null);
        }
        if (dismissHint != null)
        {
            dismissHint.gameObject.SetActive(zone.requireKeyPress);
            if (zone.requireKeyPress)
                dismissHint.text = $"[ {zone.dismissKey} ] pour continuer";
        }
        if (messageText != null)
            messageText.text = "";

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideInRoutine(zone.message));
    }

    void HideZone(TutorialZone zone)
    {
        zone.isActive = false;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (currentZone == zone)
        {
            currentZone = null;

            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideOutRoutine());
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Animation slide + bounce
    // ════════════════════════════════════════════════════════════

    IEnumerator SlideInRoutine(string message)
    {
        isShowing = true;
        SetInteractable(true);

        float elapsed = 0f;
        Vector2 startPos = panelRect.anchoredPosition;

        while (elapsed < slideInDuration)
        {
            float t = elapsed / slideInDuration;

            Vector2 basePos = Vector2.Lerp(startPos, shownPosition, t);

            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float bounceOffset = oscillation * dampingCurve * bounceAmplitude;

            panelRect.anchoredPosition = basePos + Vector2.down * bounceOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = shownPosition;

        if (messageText != null)
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(message));
        }

        slideCoroutine = null;
    }

    IEnumerator SlideOutRoutine()
    {
        SetInteractable(false);

        float elapsed = 0f;
        Vector2 startPos = panelRect.anchoredPosition;

        while (elapsed < slideOutDuration)
        {
            float t = elapsed / slideOutDuration;
            float easedT = t * t;
            panelRect.anchoredPosition = Vector2.Lerp(startPos, hiddenPosition, easedT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = hiddenPosition;
        isShowing = false;

        if (titleText != null) { titleText.text = ""; titleText.gameObject.SetActive(false); }
        if (messageText != null) { messageText.text = ""; }
        if (iconImage != null) { iconImage.sprite = null; iconImage.gameObject.SetActive(false); }
        if (dismissHint != null) { dismissHint.text = ""; dismissHint.gameObject.SetActive(false); }

        slideCoroutine = null;
    }

    IEnumerator TypewriterRoutine(string fullText)
    {
        messageText.text = "";
        foreach (char c in fullText)
        {
            messageText.text += c;
            yield return new WaitForSeconds(typewriterDelay);
        }
        typewriterCoroutine = null;
    }

    void OnDrawGizmos()
    {
        foreach (TutorialZone zone in zones)
        {
            if (zone.triggerObject == null) continue;
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(zone.triggerObject.transform.position, zone.triggerRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(zone.triggerObject.transform.position, 0.15f);
        }
    }
}