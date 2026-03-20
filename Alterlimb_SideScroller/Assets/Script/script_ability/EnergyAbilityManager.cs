using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityEnergySystem : MonoBehaviour
{
    [System.Serializable]
    public class AbilityEnergy
    {
        public float maxEnergy = 100f;
        public float currentEnergy;
        public float displayedEnergy;
        public float rechargeDelay = 3f;
        public float rechargeTimer;
        public float barSmoothSpeed = 5f;
        public bool isRecharging = false;

        // Multiplicateur d'effet quand barre basse (0.3 = 30% d'effet à vide)
        public float minEffectMultiplier = 0.3f;

        public void Init()
        {
            currentEnergy = maxEnergy;
            displayedEnergy = maxEnergy;
        }

        // Retourne un multiplicateur entre minEffectMultiplier et 1 selon l'énergie restante
        public float GetEffectMultiplier()
        {
            float ratio = currentEnergy / maxEnergy;
            return Mathf.Lerp(minEffectMultiplier, 1f, ratio);
        }

        public void Consume(float amount)
        {
            currentEnergy = Mathf.Max(currentEnergy - amount, 0f);
            isRecharging = true;
            rechargeTimer = rechargeDelay;
        }

        public void Update(float deltaTime)
        {
            if (!isRecharging) return;

            rechargeTimer -= deltaTime;
            if (rechargeTimer <= 0f)
            {
                currentEnergy = maxEnergy;
                isRecharging = false;
            }

            // Lissage visuel
            displayedEnergy = Mathf.Lerp(displayedEnergy, currentEnergy, barSmoothSpeed * deltaTime);
        }

        public void SmoothUpdate(float deltaTime)
        {
            if (isRecharging) return;
            displayedEnergy = Mathf.Lerp(displayedEnergy, currentEnergy, barSmoothSpeed * deltaTime);
        }
    }

    [Header("Bras UI")]
    [SerializeField] Image armEnergyBar;
    [SerializeField] TMP_Text armAbilityText;

    [Header("Jambes UI")]
    [SerializeField] Image legEnergyBar;
    [SerializeField] TMP_Text legAbilityText;

    [Header("Energy Settings - Bras")]
    public AbilityEnergy grapplingEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 3f };
    public AbilityEnergy sawEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 2f };

    [Header("Energy Settings - Jambes")]
    public AbilityEnergy dashEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 1.5f };
    public AbilityEnergy jumpEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 1f };

    [Header("Coût en énergie")]
    [SerializeField] float grapplingCost = 20f;
    [SerializeField] float sawCost = 15f;
    [SerializeField] float dashCost = 25f;
    [SerializeField] float jumpBoostCost = 10f;

    AbilityManager abilityManager;
    GrapplingHook grapplingHook;
    SawAbility sawAbility;
    CharaController charaController;

    bool wasUsingGrapple = false;

    void Awake()
    {
        abilityManager = GetComponent<AbilityManager>();
        grapplingHook = GetComponent<GrapplingHook>();
        sawAbility = GetComponent<SawAbility>();
        charaController = GetComponent<CharaController>();

        grapplingEnergy.Init();
        sawEnergy.Init();
        dashEnergy.Init();
        jumpEnergy.Init();
    }

    void Update()
    {
        UpdateGrapplingEnergy();
        UpdateAllBars();
        UpdateUI();
    }

    // ── Grappin : consomme quand on lâche ──────────────────────
    void UpdateGrapplingEnergy()
    {
        if (grapplingHook == null) return;

        bool isUsingNow = grapplingHook.isUsingGrapple;
        if (wasUsingGrapple && !isUsingNow)
            grapplingEnergy.Consume(grapplingCost);

        wasUsingGrapple = isUsingNow;
    }

    // ── Appelé par SawAbility quand elle attaque ───────────────
    public void OnSawUsed()
    {
        sawEnergy.Consume(sawCost);
    }

    // ── Appelé par CharaController quand dash ─────────────────
    public void OnDashUsed()
    {
        dashEnergy.Consume(dashCost);
    }

    // ── Appelé par CharaController quand jump boost ────────────
    public void OnJumpBoostUsed()
    {
        jumpEnergy.Consume(jumpBoostCost);
    }

    // ── Mise à jour de toutes les barres ──────────────────────
    void UpdateAllBars()
    {
        grapplingEnergy.Update(Time.deltaTime);
        sawEnergy.Update(Time.deltaTime);
        dashEnergy.Update(Time.deltaTime);
        jumpEnergy.Update(Time.deltaTime);

        grapplingEnergy.SmoothUpdate(Time.deltaTime);
        sawEnergy.SmoothUpdate(Time.deltaTime);
        dashEnergy.SmoothUpdate(Time.deltaTime);
        jumpEnergy.SmoothUpdate(Time.deltaTime);
    }

    // ── UI : barre + texte selon capacité active ──────────────
    void UpdateUI()
    {
        if (abilityManager == null) return;

        // Bras
        switch (abilityManager.CurrentArm)
        {
            case ArmAbility.Hand:
                armEnergyBar.fillAmount = 1f;
                armAbilityText.text = "Main droite";
                break;
            case ArmAbility.Grapple:
                armEnergyBar.fillAmount = grapplingEnergy.displayedEnergy / grapplingEnergy.maxEnergy;
                armAbilityText.text = "Grappin";
                break;
            case ArmAbility.Saw:
                armEnergyBar.fillAmount = sawEnergy.displayedEnergy / sawEnergy.maxEnergy;
                armAbilityText.text = "Scie";
                break;
        }

        // Jambes
        switch (abilityManager.CurrentLeg)
        {
            case LegAbility.NormalJump:
                legEnergyBar.fillAmount = 1f;
                legAbilityText.text = "Jambes";
                break;
            case LegAbility.HighJump:
                legEnergyBar.fillAmount = jumpEnergy.displayedEnergy / jumpEnergy.maxEnergy;
                legAbilityText.text = "Grand Saut";
                break;
            case LegAbility.Dash:
                legEnergyBar.fillAmount = dashEnergy.displayedEnergy / dashEnergy.maxEnergy;
                legAbilityText.text = "Dash";
                break;
        }
    }

    // ── Getters multiplicateurs pour les scripts ──────────────
    public float GetGrappleMultiplier() => grapplingEnergy.GetEffectMultiplier();
    public float GetSawMultiplier() => sawEnergy.GetEffectMultiplier();
    public float GetDashMultiplier() => dashEnergy.GetEffectMultiplier();
    public float GetJumpMultiplier() => jumpEnergy.GetEffectMultiplier();
}