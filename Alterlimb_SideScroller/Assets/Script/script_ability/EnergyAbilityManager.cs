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
        public float minEffectMultiplier = 0.3f;

        public bool HasEnergy => currentEnergy > 0f;

        public void Init()
        {
            currentEnergy = maxEnergy;
            displayedEnergy = maxEnergy;
        }

        public float GetEffectMultiplier()
        {
            float ratio = currentEnergy / maxEnergy;
            return Mathf.Lerp(minEffectMultiplier, 1f, ratio);
        }

        public void Consume(float amount)
        {
            if (currentEnergy <= 0f) return;

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
    [SerializeField] Image armAttentionIcon;

    [Header("Jambes UI")]
    [SerializeField] Image legEnergyBar;
    [SerializeField] TMP_Text legAbilityText;
    [SerializeField] Image legAttentionIcon;

    [Header("Warning")]
    [SerializeField] float attentionBlinkInterval = 0.3f;

    [Header("Energy Settings - Bras")]
    public AbilityEnergy grapplingEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 3f };
    public AbilityEnergy sawEnergy = new AbilityEnergy { maxEnergy = 100f, rechargeDelay = 2f };

    [Header("Coût en énergie")]
    [SerializeField] float grapplingCost = 20f;
    [SerializeField] float sawCost = 15f;

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
    }

    void Update()
    {
        UpdateGrapplingEnergy();
        UpdateAllBars();
        UpdateUI();
    }

    void UpdateGrapplingEnergy()
    {
        if (grapplingHook == null) return;

        bool isUsingNow = grapplingHook.isUsingGrapple;
        if (wasUsingGrapple && !isUsingNow)
            grapplingEnergy.Consume(grapplingCost);

        wasUsingGrapple = isUsingNow;
    }

    public void OnSawUsed()
    {
        sawEnergy.Consume(sawCost);
    }

    void UpdateAllBars()
    {
        grapplingEnergy.Update(Time.deltaTime);
        sawEnergy.Update(Time.deltaTime);

        grapplingEnergy.SmoothUpdate(Time.deltaTime);
        sawEnergy.SmoothUpdate(Time.deltaTime);
    }

    void UpdateUI()
    {
        if (abilityManager == null) return;

        bool armEmpty = false;

        switch (abilityManager.CurrentArm)
        {
            case ArmAbility.Hand:
                armEnergyBar.fillAmount = 1f;
                armAbilityText.text = "Main droite";
                armEmpty = false;
                break;
            case ArmAbility.Grapple:
                armEnergyBar.fillAmount = grapplingEnergy.displayedEnergy / grapplingEnergy.maxEnergy;
                armAbilityText.text = "Grappin";
                armEmpty = !grapplingEnergy.HasEnergy;
                break;
            case ArmAbility.Saw:
                armEnergyBar.fillAmount = sawEnergy.displayedEnergy / sawEnergy.maxEnergy;
                armAbilityText.text = "Scie";
                armEmpty = !sawEnergy.HasEnergy;
                break;
        }

        bool legEmpty = false;

        switch (abilityManager.CurrentLeg)
        {
            case LegAbility.NormalJump:
                legEnergyBar.fillAmount = 1f;
                legAbilityText.text = "Jambes";
                legEmpty = false;
                break;
        }

        UpdateAttentionIcon(armEnergyBar, armAttentionIcon, armEmpty);
        UpdateAttentionIcon(legEnergyBar, legAttentionIcon, legEmpty);
    }

    void UpdateAttentionIcon(Image energyBar, Image attentionIcon, bool isEmpty)
    {
        if (energyBar == null) return;

        energyBar.enabled = !isEmpty;

        if (attentionIcon == null) return;

        if (!isEmpty)
        {
            attentionIcon.enabled = false;
            return;
        }

        attentionIcon.enabled = true;
        bool blinkOn = Mathf.FloorToInt(Time.time / attentionBlinkInterval) % 2 == 0;
        Color c = attentionIcon.color;
        c.a = blinkOn ? 1f : 0f;
        attentionIcon.color = c;
    }

    public bool CanUseGrapple() => grapplingEnergy.HasEnergy;
    public bool CanUseSaw() => sawEnergy.HasEnergy;

    public float GetGrappleMultiplier() => grapplingEnergy.GetEffectMultiplier();
    public float GetSawMultiplier() => sawEnergy.GetEffectMultiplier();
}