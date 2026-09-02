using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class OxiOCore : MonoBehaviour
{
    public enum CutDetection
    {
        SawCollider,
        PlayerProximity
    }

    [Header("Détection de la scie")]
    [SerializeField] private CutDetection cutDetection = CutDetection.PlayerProximity;
    [SerializeField] private string sawTag = "saw_blade";
    [SerializeField] private float cutRadius = 2f;
    [SerializeField] private bool requireSawEquipped = true;
    [SerializeField] private float contactGraceTime = 0.12f;

    [Header("Découpe")]
    [SerializeField] private float sawDuration = 2.5f;
    [SerializeField] private float progressDecayPerSecond = 0.3f;
    [SerializeField] private bool keepProgressBetweenWindows = true;

    [Header("Joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Capacités")]
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private bool unlockAbilitiesIfMissing = true;
    [SerializeField] private bool equipGrappleOnWindowOpen = true;

    [Header("Ancre de grappin")]
    [SerializeField] private GameObject grappleAnchor;

    [Header("Noyaux de la phase")]
    [SerializeField] private int cutsPerPhase = 2;

    [Header("Animation")]
    [SerializeField] private OxiO_Animation animationDriver;
    [SerializeField] private int slicedPhaseIndex = 1;
    [SerializeField] private bool playSlicedOnEveryCut = true;
    [SerializeField] private bool explosionFromAnimationEvent = true;
    [SerializeField] private float explosionFallbackDelay = 2.5f;

    [Header("Feedback")]
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject vulnerableVisual;
    [SerializeField] private GameObject cuttingVisual;
    [SerializeField] private GameObject removedVisual;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private Collider2D cutTrigger;

    [Header("Explosion du noyau")]
    [SerializeField] private ParticleSystem explosion;
    [SerializeField] private float knockbackSpeed = 9f;
    [SerializeField] private float minUpwardRatio = 0.4f;
    [SerializeField] private float maxKnockbackSpeed = 14f;
    [SerializeField] private float explosionShakeDuration = 0.7f;
    [SerializeField] private float explosionShakeMagnitude = 0.8f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;
    [SerializeField] private float diagnosticInterval = 1f;

    [Header("Événements")]
    public UnityEvent onWindowOpenedEvent;
    public UnityEvent onWindowClosedEvent;
    public UnityEvent onCuttingStartedEvent;
    public UnityEvent onCuttingInterruptedEvent;
    public UnityEvent onCoreRemovedEvent;
    public UnityEvent onCoreExplosionEvent;
    public UnityEvent onPhaseDepletedEvent;

    public event Action OnWindowOpened;
    public event Action OnWindowClosed;
    public event Action OnCuttingStarted;
    public event Action OnCuttingInterrupted;
    public event Action OnCoreRemoved;
    public event Action OnPhaseDepleted;
    public event Action<float> OnProgressChanged;

    public bool IsVulnerable { get; private set; }
    public bool IsRemoved { get; private set; }
    public bool IsCutting => wasCutting;
    public float NormalizedProgress => sawDuration <= 0f ? 0f : Mathf.Clamp01(progress / sawDuration);

    private float progress;
    private float lastContactTime = -999f;
    private bool wasCutting;
    private bool explosionDone;
    private float lastDiagnosticTime;
    private int cutsDoneThisPhase;

    public int CutsDoneThisPhase => cutsDoneThisPhase;
    public int CutsRemainingThisPhase => Mathf.Max(0, cutsPerPhase - cutsDoneThisPhase);
    public bool PhaseDepleted => cutsDoneThisPhase >= cutsPerPhase;

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindAbilityManager();

        if (cutTrigger != null)
            cutTrigger.enabled = false;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(false);

        ShowVisual(lockedVisual);
        StopSparks();

        if (!logDiagnostics)
            return;

        if (abilityManager == null)
            Debug.LogError($"[OxiOCore] '{name}' : aucun AbilityManager trouvé. Assigne-le à la main, sinon la scie ne sera jamais reconnue.", this);

        if (animationDriver == null)
            Debug.LogWarning($"[OxiOCore] '{name}' : aucun Animation Driver assigné, mode_economie et phase_X_sliced ne joueront pas.", this);

        if (ResolvePlayer() == null)
            Debug.LogError($"[OxiOCore] '{name}' : aucun objet trouvé avec le tag '{playerTag}'.", this);
    }

    private AbilityManager FindAbilityManager()
    {
        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
        {
            AbilityManager manager = found.GetComponentInChildren<AbilityManager>();

            if (manager != null)
                return manager;
        }

        return null;
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

        ShowVisual(vulnerableVisual);

        if (animationDriver != null)
            animationDriver.EnterEconomyMode();

        if (logDiagnostics)
            Debug.Log($"[OxiOCore] '{name}' : fenêtre OUVERTE. Driver={(animationDriver != null ? "ok" : "MANQUANT")}, AbilityManager={(abilityManager != null ? "ok" : "MANQUANT")}", this);

        OnWindowOpened?.Invoke();
        onWindowOpenedEvent?.Invoke();
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

        ShowVisual(IsRemoved ? removedVisual : lockedVisual);
        StopSparks();

        if (animationDriver != null && !IsRemoved)
            animationDriver.ExitEconomyMode();

        OnWindowClosed?.Invoke();
        onWindowClosedEvent?.Invoke();
    }

    public void BeginPhase(int phaseIndex, int cuts)
    {
        slicedPhaseIndex = Mathf.Max(1, phaseIndex);
        cutsPerPhase = Mathf.Max(1, cuts);
        cutsDoneThisPhase = 0;

        IsVulnerable = false;
        IsRemoved = false;
        wasCutting = false;
        explosionDone = false;
        lastContactTime = -999f;

        SetProgress(0f);
        ShowVisual(lockedVisual);
        StopSparks();

        if (cutTrigger != null)
            cutTrigger.enabled = false;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(false);
    }

    public void ResetForRetry(bool resetProgress)
    {
        IsVulnerable = false;
        wasCutting = false;
        lastContactTime = -999f;

        if (resetProgress)
            SetProgress(0f);

        if (cutTrigger != null)
            cutTrigger.enabled = false;

        if (grappleAnchor != null)
            grappleAnchor.SetActive(false);

        StopSparks();
        ShowVisual(IsRemoved ? removedVisual : lockedVisual);
    }

    private void Update()
    {
        if (!IsVulnerable || IsRemoved)
            return;

        if (cutDetection == CutDetection.PlayerProximity)
            CheckProximityCut();

        bool inContact = Time.time - lastContactTime <= contactGraceTime;

        if (inContact)
        {
            if (!wasCutting)
            {
                wasCutting = true;
                PlaySparks();
                ShowVisual(cuttingVisual);
                OnCuttingStarted?.Invoke();
                onCuttingStartedEvent?.Invoke();
            }
        }
        else if (wasCutting)
        {
            wasCutting = false;
            StopSparks();
            ShowVisual(vulnerableVisual);
            OnCuttingInterrupted?.Invoke();
            onCuttingInterruptedEvent?.Invoke();
        }

        if (!inContact && progress > 0f)
            SetProgress(Mathf.Max(0f, progress - progressDecayPerSecond * Time.deltaTime));
    }

    private void CheckProximityCut()
    {
        Transform target = ResolvePlayer();
        bool sawReady = IsSawReady();
        float distance = target != null ? Vector2.Distance(transform.position, target.position) : -1f;

        if (!sawReady || target == null || distance > cutRadius)
        {
            LogCutBlocked(sawReady, target, distance);
            return;
        }

        lastContactTime = Time.time;
        SetProgress(progress + Time.deltaTime);

        if (progress >= sawDuration)
            RemoveCore();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (cutDetection != CutDetection.SawCollider)
            return;

        if (!IsVulnerable || IsRemoved)
            return;

        if (!other.CompareTag(sawTag))
            return;

        if (!IsSawReady())
            return;

        lastContactTime = Time.time;
        SetProgress(progress + Time.fixedDeltaTime);

        if (progress >= sawDuration)
            RemoveCore();
    }

    private void LogCutBlocked(bool sawReady, Transform target, float distance)
    {
        if (!logDiagnostics || Time.time - lastDiagnosticTime < diagnosticInterval)
            return;

        lastDiagnosticTime = Time.time;

        if (target == null)
        {
            Debug.LogWarning($"[OxiOCore] '{name}' : joueur introuvable (tag '{playerTag}').", this);
            return;
        }

        if (!sawReady)
        {
            string reason = abilityManager == null
                ? "AbilityManager manquant"
                : abilityManager.CombatLocked
                    ? "capacités verrouillées (SetCombatLock est resté à true)"
                    : $"bras actif = {abilityManager.CurrentArm}, il faut Saw";

            Debug.LogWarning($"[OxiOCore] '{name}' : découpe bloquée — {reason}.", this);
            return;
        }

        Debug.LogWarning($"[OxiOCore] '{name}' : découpe bloquée — joueur à {distance:F2} unités, Cut Radius = {cutRadius}. Rapproche le noyau ou augmente le rayon.", this);
    }

    private bool IsSawReady()
    {
        if (!requireSawEquipped)
            return true;

        return abilityManager != null && abilityManager.IsSawEquipped;
    }

    private Transform ResolvePlayer()
    {
        if (player != null)
            return player;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
            player = found.transform;

        return player;
    }

    private void RemoveCore()
    {
        cutsDoneThisPhase++;
        explosionDone = false;

        bool depleted = PhaseDepleted;
        IsRemoved = depleted;

        SetProgress(0f);
        CloseWindow();

        if (depleted)
            ShowVisual(removedVisual);

        bool playSliced = animationDriver != null && (playSlicedOnEveryCut || depleted);

        if (playSliced)
        {
            animationDriver.PlaySliced(slicedPhaseIndex);

            if (explosionFromAnimationEvent)
            {
                if (logDiagnostics)
                    Debug.Log($"[OxiOCore] '{name}' : explosion en attente d'un Animation Event, secours dans {explosionFallbackDelay}s.", this);

                StartCoroutine(ExplosionFallbackRoutine());
            }
            else
            {
                TriggerCoreExplosion();
            }
        }
        else
        {
            TriggerCoreExplosion();
        }

        if (logDiagnostics)
            Debug.Log($"[OxiOCore] Noyau arraché ({cutsDoneThisPhase}/{cutsPerPhase} pour la phase {slicedPhaseIndex}).", this);

        OnCoreRemoved?.Invoke();
        onCoreRemovedEvent?.Invoke();

        if (depleted)
        {
            OnPhaseDepleted?.Invoke();
            onPhaseDepletedEvent?.Invoke();
        }
    }

    public void TriggerCoreExplosion()
    {
        if (explosionDone)
        {
            if (logDiagnostics)
                Debug.LogWarning($"[OxiOCore] '{name}' : explosion déjà jouée pour cette découpe, appel ignoré.", this);

            return;
        }

        explosionDone = true;

        PlayExplosionParticles();

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(explosionShakeDuration, explosionShakeMagnitude);

        StartCoroutine(KnockbackRoutine());
        onCoreExplosionEvent?.Invoke();
    }

    private void PlayExplosionParticles()
    {
        if (explosion == null)
        {
            Debug.LogError($"[OxiOCore] '{name}' : le champ Explosion est vide, aucune particule ne sera jouée. Assigne ton ParticleSystem.", this);
            return;
        }

        if (!explosion.gameObject.activeInHierarchy)
        {
            if (logDiagnostics)
                Debug.LogWarning($"[OxiOCore] '{name}' : le ParticleSystem '{explosion.name}' était désactivé, il est réactivé.", this);

            explosion.gameObject.SetActive(true);
        }

        explosion.Clear(true);
        explosion.Play(true);

        if (!logDiagnostics)
            return;

        ParticleSystemRenderer renderer = explosion.GetComponent<ParticleSystemRenderer>();
        string layer = renderer != null
            ? $"layer '{renderer.sortingLayerName}' ordre {renderer.sortingOrder}"
            : "aucun ParticleSystemRenderer";

        Debug.Log($"[OxiOCore] Explosion jouée : '{explosion.name}' à {explosion.transform.position}, {explosion.main.duration:0.##}s, {layer}.", explosion);
    }

    private IEnumerator ExplosionFallbackRoutine()
    {
        yield return new WaitForSeconds(explosionFallbackDelay);

        if (!explosionDone)
        {
            Debug.LogWarning($"[OxiOCore] '{name}' : aucun Animation Event reçu, explosion déclenchée par sécurité.", this);
            TriggerCoreExplosion();
        }
    }

    private IEnumerator KnockbackRoutine()
    {
        Transform target = ResolvePlayer();

        if (target == null)
            yield break;

        GrapplingHook hook = target.GetComponentInChildren<GrapplingHook>();

        if (hook != null)
            hook.ReleaseGrapple();

        yield return null;

        Rigidbody2D body = target.GetComponentInChildren<Rigidbody2D>();

        if (body == null)
            yield break;

        Vector2 direction = (Vector2)target.position - (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.up;

        direction.Normalize();
        direction.y = Mathf.Max(direction.y, minUpwardRatio);
        direction.Normalize();

        Vector2 push = direction * knockbackSpeed;

        if (push.magnitude > maxKnockbackSpeed)
            push = push.normalized * maxKnockbackSpeed;

        body.linearVelocity = push;
    }

    private void SetProgress(float value)
    {
        progress = Mathf.Clamp(value, 0f, sawDuration);
        OnProgressChanged?.Invoke(NormalizedProgress);
    }

    private void ShowVisual(GameObject target)
    {
        SetActiveSafe(lockedVisual, target == lockedVisual);
        SetActiveSafe(vulnerableVisual, target == vulnerableVisual);
        SetActiveSafe(cuttingVisual, target == cuttingVisual);
        SetActiveSafe(removedVisual, target == removedVisual);
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
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

    private void OnDrawGizmosSelected()
    {
        if (cutDetection != CutDetection.PlayerProximity)
            return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, cutRadius);
    }
}