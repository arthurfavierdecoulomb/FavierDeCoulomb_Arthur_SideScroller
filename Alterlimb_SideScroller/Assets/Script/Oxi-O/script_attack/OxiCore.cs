using System;
using UnityEngine;

public class OxiOCore : MonoBehaviour
{
    [Header("Découpe")]
    [SerializeField] private string sawTag = "saw_blade";
    [SerializeField] private float sawDuration = 2.5f;
    [SerializeField] private float progressDecayPerSecond = 0.3f;
    [SerializeField] private bool requireSawEquipped = true;
    [SerializeField] private float contactGraceTime = 0.12f;

    [Header("Fenêtre")]
    [SerializeField] private bool keepProgressBetweenWindows = true;

    [Header("Capacités")]
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private bool unlockAbilitiesIfMissing = true;
    [SerializeField] private bool equipGrappleOnWindowOpen = true;

    [Header("Ancre de grappin")]
    [SerializeField] private GameObject grappleAnchor;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color vulnerableColor = new Color(1f, 0.35f, 0.1f, 1f);
    [SerializeField] private Color cuttingColor = new Color(1f, 0.95f, 0.4f, 1f);
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private Collider2D cutTrigger;

    public event Action OnWindowOpened;
    public event Action OnWindowClosed;
    public event Action OnCuttingStarted;
    public event Action OnCuttingInterrupted;
    public event Action OnCoreRemoved;
    public event Action<float> OnProgressChanged;

    public bool IsVulnerable { get; private set; }
    public bool IsRemoved { get; private set; }
    public bool IsCutting => wasCutting;
    public float NormalizedProgress => sawDuration <= 0f ? 0f : Mathf.Clamp01(progress / sawDuration);

    private float progress;
    private float lastContactTime = -999f;
    private bool wasCutting;

    [Obsolete]
    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindObjectOfType<AbilityManager>();

        if (cutTrigger != null)
            cutTrigger.enabled = false;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(false);

        ApplyColor(lockedColor);
        StopSparks();
    }

    public void OpenWindow()
    {
        if (IsRemoved)
            return;

        IsVulnerable = true;

        if (!keepProgressBetweenWindows)
            SetProgress(0f);

        if (cutTrigger != null)
            cutTrigger.enabled = true;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(true);

        if (abilityManager != null)
        {
            if (unlockAbilitiesIfMissing)
            {
                abilityManager.UnlockArm(ArmAbility.Grapple);
                abilityManager.UnlockArm(ArmAbility.Saw);
            }

            abilityManager.SetCombatLock(false);

            if (equipGrappleOnWindowOpen)
                abilityManager.EquipArm(ArmAbility.Grapple);
        }

        ApplyColor(vulnerableColor);
        OnWindowOpened?.Invoke();
    }

    public void CloseWindow()
    {
        if (!IsVulnerable)
            return;

        IsVulnerable = false;
        wasCutting = false;

        if (cutTrigger != null)
            cutTrigger.enabled = false;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(false);

        if (abilityManager != null)
            abilityManager.SetCombatLock(true);

        ApplyColor(lockedColor);
        StopSparks();

        OnWindowClosed?.Invoke();
    }

    private void Update()
    {
        if (!IsVulnerable || IsRemoved)
            return;

        bool inContact = Time.time - lastContactTime <= contactGraceTime;

        if (inContact)
        {
            if (!wasCutting)
            {
                wasCutting = true;
                PlaySparks();
                ApplyColor(cuttingColor);
                OnCuttingStarted?.Invoke();
            }
        }
        else if (wasCutting)
        {
            wasCutting = false;
            StopSparks();
            ApplyColor(vulnerableColor);
            OnCuttingInterrupted?.Invoke();
        }

        if (!inContact && progress > 0f)
            SetProgress(Mathf.Max(0f, progress - progressDecayPerSecond * Time.deltaTime));
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsVulnerable || IsRemoved)
            return;

        if (!other.CompareTag(sawTag))
            return;

        if (requireSawEquipped && (abilityManager == null || !abilityManager.IsSawEquipped))
            return;

        lastContactTime = Time.time;
        SetProgress(progress + Time.fixedDeltaTime);

        if (progress >= sawDuration)
            RemoveCore();
    }

    private void RemoveCore()
    {
        IsRemoved = true;
        SetProgress(sawDuration);
        CloseWindow();
        OnCoreRemoved?.Invoke();
    }

    private void SetProgress(float value)
    {
        progress = Mathf.Clamp(value, 0f, sawDuration);
        OnProgressChanged?.Invoke(NormalizedProgress);
    }

    private void ApplyColor(Color color)
    {
        if (coreRenderer != null)
            coreRenderer.color = color;
    }

    private void PlaySparks()
    {
        if (sparks != null && !sparks.isPlaying)
            sparks.Play();
    }

    private void StopSparks()
    {
        if (sparks != null && sparks.isPlaying)
            sparks.Stop();
    }
}