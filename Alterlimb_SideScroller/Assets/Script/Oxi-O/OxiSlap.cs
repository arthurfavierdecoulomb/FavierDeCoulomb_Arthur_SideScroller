using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OxiOSlap : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private OxiO_Animation oxiAnimation;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Zones de gifle")]
    [SerializeField] private List<Collider2D> slapZones = new List<Collider2D>();

    [Header("Mains (gauche et droite à l'écran)")]
    [SerializeField] private Transform leftHandPoint;
    [SerializeField] private Transform rightHandPoint;
    [SerializeField] private bool requireHandRange = false;
    [SerializeField] private float hitRadius = 4f;

    [Header("Dégâts")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private bool damageAsPercentOfMax = true;

    [Header("Éjection")]
    [SerializeField] private float knockbackSpeed = 14f;
    [Range(0f, 1f)]
    [SerializeField] private float upwardRatio = 0.45f;
    [SerializeField] private float stunDuration = 0.6f;

    [Header("Caméra")]
    [SerializeField] private CameraFocus cameraFocus;
    [SerializeField] private string focusId = "focus_oxi";
    [SerializeField] private float focusHoldAfterImpact = 0.5f;

    [Header("Timing")]
    [SerializeField] private float impactFallbackDelay = 1.5f;
    [SerializeField] private float slapTimeout = 4f;

    [Header("Impact")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeMagnitude = 0.6f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public bool IsPerforming { get; private set; }

    private bool awaitingImpact;
    private bool currentLeft;
    private bool ownsFocus;

    private void Awake()
    {
        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

        if (cameraFocus == null)
            cameraFocus = FindAnyObjectByType<CameraFocus>();

        LogSetup();
    }

    private void OnEnable()
    {
        if (oxiAnimation != null)
            oxiAnimation.OnSlapImpact += HandleSlapImpact;
    }

    private void OnDisable()
    {
        if (oxiAnimation != null)
            oxiAnimation.OnSlapImpact -= HandleSlapImpact;
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (oxiAnimation == null)
            Debug.LogError($"[OxiOSlap] '{name}' : aucun OxiO_Animation trouvé, Oxi-O ne jouera pas son coup de main.", this);

        if (leftHandPoint == null || rightHandPoint == null)
            Debug.LogError($"[OxiOSlap] '{name}' : Left Hand Point ou Right Hand Point manquant. Crée deux repères sur les mains d'Oxi-O.", this);

        if (ResolvePlayer() == null)
            Debug.LogError($"[OxiOSlap] '{name}' : aucun objet trouvé avec le tag '{playerTag}'.", this);

        if (slapZones.Count == 0)
            Debug.Log($"[OxiOSlap] '{name}' : aucune zone de gifle, ce sont les cercles des mains (Hit Radius) qui décident si Oxi-O frappe.", this);

        foreach (Collider2D zone in slapZones)
            if (zone != null && !zone.isTrigger)
                Debug.LogWarning($"[OxiOSlap] '{name}' : la zone '{zone.name}' n'est pas en Is Trigger, elle va bloquer Azu physiquement.", zone);

        if (cameraFocus == null && !string.IsNullOrEmpty(focusId))
            Debug.LogWarning($"[OxiOSlap] '{name}' : aucun CameraFocus trouvé, la caméra restera sur Azu pendant la gifle.", this);
    }

    public IEnumerator Perform()
    {
        Transform target = ResolvePlayer();

        if (!IsInSlapZone(target))
        {
            Log("compte à rebours raté, mais Azu n'est pas sur une plateforme : pas de gifle.");
            yield break;
        }

        currentLeft = ChooseLeftHand(target);
        awaitingImpact = true;
        IsPerforming = true;

        Log($"compte à rebours raté : Oxi-O frappe de la main {(currentLeft ? "gauche" : "droite")}.");

        if (cameraFocus != null && !string.IsNullOrEmpty(focusId))
        {
            cameraFocus.FocusOn(focusId);
            ownsFocus = true;
        }

        if (oxiAnimation != null)
            oxiAnimation.PlaySlap(currentLeft);

        float elapsed = 0f;

        while (awaitingImpact && elapsed < impactFallbackDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (awaitingImpact)
        {
            Debug.LogWarning($"[OxiOSlap] '{name}' : l'Animation Event 'SlapImpact' n'a pas été reçu après {impactFallbackDelay}s, impact déclenché par sécurité.", this);
            ApplyImpact();
        }

        if (ownsFocus)
        {
            if (focusHoldAfterImpact > 0f)
                yield return new WaitForSeconds(focusHoldAfterImpact);

            ReturnFocus();
        }

        elapsed = 0f;

        while (oxiAnimation != null && oxiAnimation.IsSlapping && elapsed < slapTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        IsPerforming = false;
    }

    public void Cancel()
    {
        awaitingImpact = false;
        IsPerforming = false;

        StopAllCoroutines();
        ReleaseStun();

        if (ownsFocus && cameraFocus != null)
            cameraFocus.ReleaseFocusInstant();

        ownsFocus = false;
    }

    private void ReturnFocus()
    {
        if (ownsFocus && cameraFocus != null)
            cameraFocus.ReleaseFocus();

        ownsFocus = false;
    }

    private void ReleaseStun()
    {
        Transform target = ResolvePlayer();

        if (target == null)
            return;

        CharaController controller = target.GetComponentInChildren<CharaController>();

        if (controller != null && !controller.enabled)
            controller.enabled = true;
    }

    private void HandleSlapImpact()
    {
        if (awaitingImpact)
            ApplyImpact();
    }

    private void ApplyImpact()
    {
        awaitingImpact = false;

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);

        Transform target = ResolvePlayer();

        if (target == null)
            return;

        Transform hand = currentLeft ? leftHandPoint : rightHandPoint;

        if (requireHandRange && hand != null && Vector2.Distance(hand.position, target.position) > hitRadius)
        {
            Log("Azu est hors de portée de la main, coup esquivé.");
            return;
        }

        StartCoroutine(HitRoutine(target, hand));
    }

    private IEnumerator HitRoutine(Transform target, Transform hand)
    {
        GrapplingHook hook = target.GetComponentInChildren<GrapplingHook>();
        CharaController controller = target.GetComponentInChildren<CharaController>();

        if (hook != null)
            hook.ReleaseGrapple();

        bool stunned = controller != null && stunDuration > 0f && controller.enabled;

        if (stunned)
            controller.enabled = false;

        yield return null;

        Rigidbody2D body = target.GetComponentInChildren<Rigidbody2D>();

        if (body != null)
        {
            Vector2 direction = new Vector2(EjectionSide(target), upwardRatio).normalized;
            body.linearVelocity = direction * knockbackSpeed;
        }

        DamagePlayer(target);

        if (!stunned)
            yield break;

        yield return new WaitForSeconds(stunDuration);

        if (controller != null)
            controller.enabled = true;
    }

    private float EjectionSide(Transform target)
    {
        return currentLeft ? 1f : -1f;
    }

    private void DamagePlayer(Transform target)
    {
        PlayerHealth health = target.GetComponentInParent<PlayerHealth>();

        if (health == null)
            health = target.GetComponentInChildren<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[OxiOSlap] '{name}' : aucun PlayerHealth trouvé sur Azu, pas de dégâts.", this);
            return;
        }

        float amount = damageAsPercentOfMax ? health.MaxHealth * damage * 0.01f : damage;

        if (amount <= 0f)
            return;

        health.TakeDamage(amount);

        Log($"Azu encaisse le coup de main : {amount:0.#} pv.");
    }

    private bool IsInSlapZone(Transform target)
    {
        if (target == null)
            return false;

        if (slapZones.Count == 0)
            return IsInHandRange(target);

        foreach (Collider2D zone in slapZones)
            if (zone != null && zone.OverlapPoint(target.position))
                return true;

        return false;
    }

    private bool IsInHandRange(Transform target)
    {
        if (leftHandPoint == null && rightHandPoint == null)
            return true;

        if (leftHandPoint != null && Vector2.Distance(leftHandPoint.position, target.position) <= hitRadius)
            return true;

        return rightHandPoint != null && Vector2.Distance(rightHandPoint.position, target.position) <= hitRadius;
    }

    private bool ChooseLeftHand(Transform target)
    {
        if (target == null)
            return Random.value < 0.5f;

        if (leftHandPoint == null || rightHandPoint == null)
            return target.position.x < transform.position.x;

        float toLeft = Vector2.Distance(target.position, leftHandPoint.position);
        float toRight = Vector2.Distance(target.position, rightHandPoint.position);

        return toLeft <= toRight;
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

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[OxiOSlap] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);

        foreach (Collider2D zone in slapZones)
        {
            if (zone == null)
                continue;

            Bounds bounds = zone.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        if (leftHandPoint != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(leftHandPoint.position, hitRadius);
        }

        if (rightHandPoint != null)
        {
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(rightHandPoint.position, hitRadius);
        }
    }
}