using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialZone
    {
        [Header("Zone de déclenchement")]
        public GameObject triggerObject;        // l'objet avec le collider trigger
        public float triggerRadius = 3f;        // OU rayon si pas de collider

        [Header("Bulle de texte")]
        public string title = "";               // titre optionnel
        [TextArea(2, 6)]
        public string message = "";             // message principal
        public Sprite icon;                     // icône optionnelle

        [Header("Comportement")]
        public float displayDuration = 0f;      // 0 = reste jusqu'à ce qu'on parte
        public bool showOnce = true;            // ne s'affiche qu'une seule fois
        public bool requireKeyPress = false;    // attendre appui touche pour fermer
        public KeyCode dismissKey = KeyCode.E;

        [HideInInspector] public bool hasBeenShown = false;
        [HideInInspector] public bool isActive = false;
    }

    [Header("UI Refs")]
    [SerializeField] GameObject bubblePanel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI dismissHint;   // ex: "Appuie sur E pour continuer"

    [Header("Joueur")]
    [SerializeField] Transform player;

    [Header("Zones tutoriel")]
    [SerializeField] List<TutorialZone> zones = new List<TutorialZone>();

    [Header("Animation")]
    [SerializeField] float fadeSpeed = 5f;

    CanvasGroup canvasGroup;
    TutorialZone currentZone = null;
    float displayTimer = 0f;

    void Awake()
    {
        // Auto-find joueur si non assigné
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogWarning("[TutorialManager] Joueur non trouvé !");
        }

        canvasGroup = bubblePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = bubblePanel.AddComponent<CanvasGroup>();

        HideBubble(instant: true);
    }

    void Update()
    {
        if (player == null) return;

        CheckZones();
        HandleDismiss();
        UpdateFade();
    }

    void CheckZones()
    {
        foreach (TutorialZone zone in zones)
        {
            if (zone.showOnce && zone.hasBeenShown) continue;
            if (zone.triggerObject == null) continue;

            float dist = Vector2.Distance(player.position, zone.triggerObject.transform.position);
            bool inRange = dist <= zone.triggerRadius;

            if (inRange && !zone.isActive)
            {
                ShowZone(zone);
            }
            else if (!inRange && zone.isActive && !zone.requireKeyPress)
            {
                HideZone(zone);
            }
        }

        // Timer d'affichage automatique
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
            HideZone(currentZone);
    }

    void ShowZone(TutorialZone zone)
    {
        // Cache la zone précédente proprement
        if (currentZone != null && currentZone != zone)
            currentZone.isActive = false;

        zone.isActive = true;
        zone.hasBeenShown = true;
        currentZone = zone;
        displayTimer = zone.displayDuration;

        // Remplit la bulle
        if (titleText != null)
        {
            titleText.text = zone.title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(zone.title));
        }

        if (messageText != null)
            messageText.text = zone.message;

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

        bubblePanel.SetActive(true);
    }

    void HideZone(TutorialZone zone)
    {
        zone.isActive = false;
        if (currentZone == zone)
        {
            currentZone = null;
            HideBubble();
        }
    }

    void HideBubble(bool instant = false)
    {
        if (instant)
        {
            canvasGroup.alpha = 0f;
            bubblePanel.SetActive(false);
        }
        else
        {
            // Le fade out est géré dans UpdateFade()
        }
    }

    void UpdateFade()
    {
        float targetAlpha = currentZone != null ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (canvasGroup.alpha < 0.01f && currentZone == null)
            bubblePanel.SetActive(false);
        else if (currentZone != null)
            bubblePanel.SetActive(true);
    }

    void OnDrawGizmosSelected()
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