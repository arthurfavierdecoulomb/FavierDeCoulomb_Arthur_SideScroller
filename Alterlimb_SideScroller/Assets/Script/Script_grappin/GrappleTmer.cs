using UnityEngine;
using UnityEngine.UI;

public class GrappleEnergySystem : MonoBehaviour
{
    [Header("Energy Settings")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float energyCostPerUse = 20f;
    [SerializeField] float rechargeDelay = 60f;
    [SerializeField] float barSmoothSpeed = 5f;     // fluidité de la barre visuelle

    [Header("References")]
    [SerializeField] Image energyBar;
    [SerializeField] GrapplingHook grapplingHook;

    float currentEnergy;
    float displayedEnergy;  // valeur affichée (lissée)
    bool isRecharging = false;
    float rechargeTimer;
    bool wasUsingGrapple = false;   // pour détecter le moment où le grappin est lâché

    void Start()
    {
        currentEnergy = maxEnergy;
        displayedEnergy = maxEnergy;
    }

    void Update()
    {
        HandleEnergyConsumption();
        HandleRecharge();
        SmoothBar();
    }

    void HandleEnergyConsumption()
    {
        if (isRecharging) return;

        bool isUsingNow = grapplingHook.isUsingGrapple;

        // Le joueur vient de lâcher le grappin → on consomme
        if (wasUsingGrapple && !isUsingNow)
        {
            ConsumeEnergy();
        }

        wasUsingGrapple = isUsingNow;
    }

    void ConsumeEnergy()
    {
        currentEnergy -= energyCostPerUse;
        currentEnergy = Mathf.Max(currentEnergy, 0f);

        if (currentEnergy <= 0f)
            StartRecharge();
    }

    void HandleRecharge()
    {
        if (!isRecharging) return;

        rechargeTimer -= Time.deltaTime;

        // Recharge progressive de la barre pendant les 60s
        displayedEnergy = Mathf.Lerp(0f, maxEnergy, 1f - (rechargeTimer / rechargeDelay));

        if (rechargeTimer <= 0f)
            FullRecharge();

        energyBar.fillAmount = displayedEnergy / maxEnergy;
    }

    void SmoothBar()
    {
        if (isRecharging) return;

        // Descente fluide de la barre
        displayedEnergy = Mathf.Lerp(displayedEnergy, currentEnergy, barSmoothSpeed * Time.deltaTime);
        energyBar.fillAmount = displayedEnergy / maxEnergy;
    }

    void StartRecharge()
    {
        isRecharging = true;
        rechargeTimer = rechargeDelay;
        currentEnergy = 0f;
        displayedEnergy = 0f;
        grapplingHook.canUseGrapple = false;
        grapplingHook.ReleaseGrapple();
    }

    void FullRecharge()
    {
        isRecharging = false;
        currentEnergy = maxEnergy;
        displayedEnergy = maxEnergy;
        grapplingHook.canUseGrapple = true;
        energyBar.fillAmount = 1f;
    }
}