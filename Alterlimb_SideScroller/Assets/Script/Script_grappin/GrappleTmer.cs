using UnityEngine;
using UnityEngine.UI;

public class GrappleEnergySystem : MonoBehaviour
{
    [Header("Energy Settings")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float energyCostPerUse = 20f;
    [SerializeField] float rechargeDelay = 60f;

    [Header("References")]
    [SerializeField] Image energyBar;
    [SerializeField] GrapplingHook grapplingHook;

    float currentEnergy;
    bool isRecharging = false;
    float rechargeTimer;

    void Start()
    {
        currentEnergy = maxEnergy;
        UpdateBar();
    }

    void Update()
    {
        // Si clic gauche + assez d'énergie
        if (Input.GetMouseButtonDown(0) && !isRecharging)
        {
            TryConsumeEnergy();
        }

        // Gestion recharge
        if (isRecharging)
        {
            rechargeTimer -= Time.deltaTime;

            if (rechargeTimer <= 0f)
            {
                FullRecharge();
            }
        }
    }

    void TryConsumeEnergy()
    {
        if (currentEnergy >= energyCostPerUse)
        {
            currentEnergy -= energyCostPerUse;
            UpdateBar();

            if (currentEnergy <= 0f)
            {
                StartRecharge();
            }
        }
        else
        {
            StartRecharge();
        }
    }

    void StartRecharge()
    {
        isRecharging = true;
        rechargeTimer = rechargeDelay;

        grapplingHook.canUseGrapple = false;
        grapplingHook.ReleaseGrapple();
    }

    void FullRecharge()
    {
        isRecharging = false;
        currentEnergy = maxEnergy;
        grapplingHook.canUseGrapple = true;
        UpdateBar();
    }

    void UpdateBar()
    {
        energyBar.fillAmount = currentEnergy / maxEnergy;
    }
}
