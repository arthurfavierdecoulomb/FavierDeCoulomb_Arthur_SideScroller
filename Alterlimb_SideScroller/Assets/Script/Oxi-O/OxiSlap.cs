using System.Collections;
using UnityEngine;

public class OxiSlap : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private OxiO_Animation oxiAnimation;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Mains (gauche et droite à l'écran)")]
    [SerializeField] private Transform leftHandPoint;
    [SerializeField] private Transform rightHandPoint;
    [SerializeField] private float hitRadius = 4f;

    [Header("Dégâts")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private bool damageAsPercentOfMax = true;

    [Header("Recul vers le sol")]
    [SerializeField] private float knockbackSpeed = 16f;
    [Range(0f, 1f)]
    [SerializeField] private float horizontalRatio = 0.4f;

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

    private void Awake()
    {
        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

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
            Debug.LogError($"[OxiSlap] '{name}' : aucun OxiO_Animation trouvé, Oxi-O ne jouera pas son coup de main.", this);

        if (leftHandPoint == null || rightHandPoint == null)
            Debug.LogError($"[OxiSlap] '{name}' : Left Hand Point ou Right Hand Point manquant. Crée deux repères sur les mains d'Oxi-O.", this);

        if (ResolvePlayer() == null)
            Debug.LogError($"[OxiSlap] '{name}' : aucun objet trouvé avec le tag '{playerTag}'.", this);
    }

    public IEnumerator Perform()
    {
        Transform target = ResolvePlayer();

        currentLeft = ChooseLeftHand(target);
        awaitingImpact = true;
        IsPerforming = true;

        Log($"compte à rebours raté : Oxi-O frappe de la main {(currentLeft ? "gauche" : "droite")}.");

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
            Debug.LogWarning($"[OxiSlap] '{name}' : l'Animation Event 'SlapImpact' n'a pas été reçu après {impactFallbackDelay}s, impact déclenché par sécurité.", this);
            ApplyImpact();
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

        if (hand != null && Vector2.Distance(hand.position, target.position) > hitRadius)
        {
            Log("Azu est hors de portée de la main, coup esquivé.");
            return;
        }

        StartCoroutine(HitRoutine(target, hand));
    }

    private IEnumerator HitRoutine(Transform target, Transform hand)
    {
        GrapplingHook hook = target.GetComponentInChildren<GrapplingHook>();

        if (hook != null)
            hook.ReleaseGrapple();

        yield return null;

        Rigidbody2D body = target.GetComponentInChildren<Rigidbody2D>();

        if (body != null)
        {
            float side = hand != null ? Mathf.Sign(target.position.x - hand.position.x) : 0f;
            Vector2 direction = new Vector2(side * horizontalRatio, -1f).normalized;
            body.linearVelocity = direction * knockbackSpeed;
        }

        DamagePlayer(target);
    }

    private void DamagePlayer(Transform target)
    {
        PlayerHealth health = target.GetComponentInParent<PlayerHealth>();

        if (health == null)
            health = target.GetComponentInChildren<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[OxiSlap] '{name}' : aucun PlayerHealth trouvé sur Azu, pas de dégâts.", this);
            return;
        }

        float amount = damageAsPercentOfMax ? health.MaxHealth * damage * 0.01f : damage;

        if (amount <= 0f)
            return;

        health.TakeDamage(amount);

        Log($"Azu encaisse le coup de main : {amount:0.#} pv.");
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
            Debug.Log($"[OxiSlap] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
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
