using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OxiOHack : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private ArtefactRoulette roulette;
    [SerializeField] private OxiO_Animation oxiAnimation;

    [Header("Couleur du piratage")]
    [SerializeField] private Color hackColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Vector2 glitchInterval = new Vector2(0.04f, 0.12f);
    [Range(0f, 1f)]
    [SerializeField] private float glitchColorChance = 0.6f;

    [Header("Inventaire verrouillé")]
    [SerializeField] private TextMeshProUGUI inventoryTitle;
    [SerializeField] private float inventoryTitleExtraTime = 0.2f;

    [Header("Barres piratées en phase 2")]
    [SerializeField] private TextMeshProUGUI barsTitle;
    [SerializeField] private Image healthBar;
    [SerializeField] private Image armStaminaBar;
    [SerializeField] private Image legStaminaBar;
    [SerializeField] private TMP_Text armAbilityText;
    [SerializeField] private TMP_Text legAbilityText;
    [SerializeField] private List<Graphic> extraHackedGraphics = new List<Graphic>();
    [SerializeField] private string[] glitchWords = { "[HACKED]", "[0X1-0]", "[NULL]", "[???]", "[N/A]", "[OXI-O]", "[0x10]" };

    [Header("Titre qui tremble")]
    [SerializeField] private float titleShakeMagnitude = 8f;
    [SerializeField] private float titleShakeSpeed = 40f;
    [Range(0f, 1f)]
    [SerializeField] private float titleJumpChance = 0.08f;
    [SerializeField] private float titleJumpMagnitude = 24f;
    [Range(0f, 1f)]
    [SerializeField] private float titleFlickerChance = 0.15f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();

    private Vector2 inventoryTitleBase;
    private Vector2 barsTitleBase;
    private float inventoryTitleUntil;
    private bool barsHacked;
    private float nextBarsGlitch;
    private bool barsGlitchColor;
    private float fakeHealth = 1f;
    private float fakeArm = 1f;
    private float fakeLeg = 1f;
    private string fakeArmWord = "";
    private string fakeLegWord = "";

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindAnyObjectByType<AbilityManager>();

        if (roulette == null)
            roulette = FindAnyObjectByType<ArtefactRoulette>();

        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

        if (inventoryTitle != null)
        {
            inventoryTitleBase = inventoryTitle.rectTransform.anchoredPosition;
            inventoryTitle.gameObject.SetActive(false);
        }

        if (barsTitle != null)
        {
            barsTitleBase = barsTitle.rectTransform.anchoredPosition;
            barsTitle.gameObject.SetActive(false);
        }

        CacheColor(healthBar);
        CacheColor(armStaminaBar);
        CacheColor(legStaminaBar);
        CacheColor(armAbilityText);
        CacheColor(legAbilityText);

        foreach (Graphic graphic in extraHackedGraphics)
            CacheColor(graphic);

        LogSetup();
    }

    private void OnEnable()
    {
        if (abilityManager != null)
            abilityManager.OnArmSwitchDenied += HandleArmSwitchDenied;

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted += HandleTransformationStarted;
            oxiAnimation.OnFinalFall += HandleFinalFall;
        }
    }

    private void OnDisable()
    {
        if (abilityManager != null)
            abilityManager.OnArmSwitchDenied -= HandleArmSwitchDenied;

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted -= HandleTransformationStarted;
            oxiAnimation.OnFinalFall -= HandleFinalFall;
        }
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (abilityManager == null)
            Debug.LogError($"[OxiOHack] '{name}' : aucun AbilityManager trouvé, l'inventaire ne réagira pas quand on appuie sur A.", this);

        if (roulette == null)
            Debug.LogWarning($"[OxiOHack] '{name}' : aucune ArtefactRoulette trouvée, la roue ne glitchera pas.", this);

        if (oxiAnimation == null)
            Debug.LogError($"[OxiOHack] '{name}' : aucun OxiO_Animation trouvé, les barres ne seront jamais piratées.", this);

        if (inventoryTitle == null)
            Debug.LogWarning($"[OxiOHack] '{name}' : Inventory Title vide, pas de [HACKED] sur l'inventaire.", this);

        if (barsTitle == null)
            Debug.LogWarning($"[OxiOHack] '{name}' : Bars Title vide, pas de [HACKED] sur les barres.", this);

        if (healthBar == null)
            Debug.LogWarning($"[OxiOHack] '{name}' : Health Bar vide, la barre de vie ne sera pas piratée.", this);
    }

    private void CacheColor(Graphic graphic)
    {
        if (graphic != null && !baseColors.ContainsKey(graphic))
            baseColors[graphic] = graphic.color;
    }

    private void HandleArmSwitchDenied()
    {
        if (roulette != null)
            roulette.PlayHacked();

        float duration = roulette != null ? roulette.HackedTotalDuration : 1.2f;
        inventoryTitleUntil = Time.unscaledTime + duration + inventoryTitleExtraTime;

        if (logDiagnostics)
            Debug.Log("[OxiOHack] A refusé : l'inventaire est sous le contrôle d'Oxi-O.", this);
    }

    private void HandleTransformationStarted()
    {
        barsHacked = true;
        nextBarsGlitch = 0f;

        if (logDiagnostics)
            Debug.Log("[OxiOHack] Phase 2 : les barres de vie et de stamina sont piratées.", this);
    }

    private void HandleFinalFall()
    {
        barsHacked = false;
        inventoryTitleUntil = 0f;

        RestoreGraphic(healthBar);
        RestoreGraphic(armStaminaBar);
        RestoreGraphic(legStaminaBar);
        RestoreGraphic(armAbilityText);
        RestoreGraphic(legAbilityText);

        foreach (Graphic graphic in extraHackedGraphics)
            RestoreGraphic(graphic);

        if (logDiagnostics)
            Debug.Log("[OxiOHack] Oxi-O est tombé : l'interface redevient normale.", this);
    }

    private void RestoreGraphic(Graphic graphic)
    {
        if (graphic != null && baseColors.TryGetValue(graphic, out Color color))
            graphic.color = color;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;

        UpdateTitle(inventoryTitle, inventoryTitleBase, now < inventoryTitleUntil, now, 0f);
        UpdateTitle(barsTitle, barsTitleBase, barsHacked, now, 31.7f);

        if (barsHacked)
            UpdateBars(now);
    }

    private void UpdateTitle(TextMeshProUGUI title, Vector2 basePosition, bool active, float now, float seed)
    {
        if (title == null)
            return;

        if (title.gameObject.activeSelf != active)
            title.gameObject.SetActive(active);

        if (!active)
        {
            title.rectTransform.anchoredPosition = basePosition;
            return;
        }

        float t = now * titleShakeSpeed;
        Vector2 offset = new Vector2(
            Mathf.PerlinNoise(seed, t) - 0.5f,
            Mathf.PerlinNoise(seed + 7.3f, t) - 0.5f) * 2f * titleShakeMagnitude;

        if (Random.value < titleJumpChance)
            offset.x += Random.Range(-titleJumpMagnitude, titleJumpMagnitude);

        title.rectTransform.anchoredPosition = basePosition + offset;

        Color color = hackColor;
        color.a = Random.value < titleFlickerChance ? 0.25f : 1f;
        title.color = color;
    }

    private void UpdateBars(float now)
    {
        if (now >= nextBarsGlitch)
        {
            nextBarsGlitch = now + Random.Range(glitchInterval.x, glitchInterval.y);

            barsGlitchColor = Random.value < glitchColorChance;
            fakeHealth = Random.value;
            fakeArm = Random.value;
            fakeLeg = Random.value;
            fakeArmWord = RandomWord();
            fakeLegWord = RandomWord();
        }

        ApplyBar(healthBar, fakeHealth);
        ApplyBar(armStaminaBar, fakeArm);
        ApplyBar(legStaminaBar, fakeLeg);

        ApplyText(armAbilityText, fakeArmWord);
        ApplyText(legAbilityText, fakeLegWord);

        foreach (Graphic graphic in extraHackedGraphics)
            ApplyTint(graphic);
    }

    private void ApplyBar(Image bar, float fill)
    {
        if (bar == null)
            return;

        bar.enabled = true;
        bar.fillAmount = fill;
        ApplyTint(bar);
    }

    private void ApplyText(TMP_Text text, string word)
    {
        if (text == null)
            return;

        text.text = word;
        ApplyTint(text);
    }

    private void ApplyTint(Graphic graphic)
    {
        if (graphic == null || !baseColors.TryGetValue(graphic, out Color baseColor))
            return;

        if (!barsGlitchColor)
        {
            graphic.color = baseColor;
            return;
        }

        Color tinted = hackColor;
        tinted.a = baseColor.a;
        graphic.color = tinted;
    }

    private string RandomWord()
    {
        if (glitchWords == null || glitchWords.Length == 0)
            return "";

        return glitchWords[Random.Range(0, glitchWords.Length)];
    }
}