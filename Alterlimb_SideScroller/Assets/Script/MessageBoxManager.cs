using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class TutorialManager : MonoBehaviour
{
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

    [Header("Machine à écrire")]
    [Tooltip("Délai en secondes entre chaque caractère affiché")]
    [SerializeField] float typewriterDelay = 0.04f;

    // CanvasGroup sur le bubblePanel — contrôle la visibilité sans SetActive
    CanvasGroup canvasGroup;

    TutorialZone currentZone = null;
    float displayTimer = 0f;
    Coroutine typewriterCoroutine = null;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogWarning("[TutorialManager] Joueur non trouvé !");
        }

        // Récupère ou crée le CanvasGroup sur le panel racine
        canvasGroup = bubblePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = bubblePanel.AddComponent<CanvasGroup>();

        // Le panel reste TOUJOURS actif dans la hiérarchie —
        // c'est le CanvasGroup qui le rend invisible et non interactif
        bubblePanel.SetActive(true);
        SetVisible(false);
    }

    void Update()
    {
        if (player == null) return;

        CheckZones();
        HandleDismiss();
    }

    // ── Visibilité instantanée via CanvasGroup ────────────────────────────
    void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
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
            // Premier appui pendant l'animation = complète le texte immédiatement
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
                messageText.text = currentZone.message;
                return; // deuxième appui fermera
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

        // ── Remplit tous les éléments AVANT de rendre visible ────────────
        // Ainsi le layout est calculé avec le bon contenu avant l'apparition

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

        // Vide le message — sera rempli lettre par lettre
        if (messageText != null)
            messageText.text = "";

        // ── Rend le panel visible instantanément ─────────────────────────
        SetVisible(true);

        // ── Lance la machine à écrire ────────────────────────────────────
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        if (messageText != null)
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(zone.message));
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

            // ── Cache tous les enfants puis rend le panel invisible ───────
            // On remet les enfants à leur état neutre pour le prochain affichage
            if (titleText != null) { titleText.text = ""; titleText.gameObject.SetActive(false); }
            if (messageText != null) { messageText.text = ""; }
            if (iconImage != null) { iconImage.sprite = null; iconImage.gameObject.SetActive(false); }
            if (dismissHint != null) { dismissHint.text = ""; dismissHint.gameObject.SetActive(false); }

            SetVisible(false);
        }
    }

    // ── Machine à écrire ─────────────────────────────────────────────────
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