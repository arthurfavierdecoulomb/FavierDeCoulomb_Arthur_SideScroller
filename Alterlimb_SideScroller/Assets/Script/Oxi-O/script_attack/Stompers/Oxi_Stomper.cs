using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Stomper : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform rails;
    [SerializeField] private Transform crusher;
    [SerializeField] private Collider2D crusherKillCollider;
    [SerializeField] private Collider2D railsKillCollider;
    [SerializeField] private StomperScreen screen;

    [Header("Repères")]
    [SerializeField] private Transform hoverPoint;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;

    [Header("Rails")]
    [SerializeField] private bool railsExtendByScale = false;
    [SerializeField] private float railsDeployDistance = 4.5f;
    [SerializeField] private float railsDeployScaleMultiplier = 6f;
    [SerializeField] private float railsDeploySpeed = 22f;
    [SerializeField] private float railsRetractSpeed = 10f;

    [Header("Écraseur")]
    [SerializeField] private float crusherSlamDistance = 4.2f;
    [SerializeField] private float slamSpeed = 70f;
    [SerializeField] private float crusherRetractSpeed = 9f;

    [Header("Déplacement")]
    [SerializeField] private float horizontalSpeed = 16f;
    [SerializeField] private float descendSpeed = 12f;
    [SerializeField] private float riseSpeed = 7f;

    [Header("Timing")]
    [SerializeField] private float warningDuration = 0.9f;
    [SerializeField] private float slamHoldDuration = 0.35f;
    [SerializeField] private float pauseBeforeCrusherRetract = 0.15f;
    [SerializeField] private float pauseAfterSlam = 0.25f;

    [Header("Apparition")]
    [SerializeField] private bool hideWhenIdle = true;
    [SerializeField] private float appearFlickerDuration = 0.45f;
    [SerializeField] private float disappearFlickerDuration = 0.3f;
    [SerializeField] private float flickerIntervalMin = 0.025f;
    [SerializeField] private float flickerIntervalMax = 0.09f;

    [Header("Impact")]
    [SerializeField] private bool shakeOnImpact = true;
    [SerializeField] private float impactShakeDuration = 0.4f;
    [SerializeField] private float impactShakeMagnitude = 0.45f;
    [SerializeField] private float impactPunchMagnitude = 0.6f;
    [SerializeField] private float impactHitStop = 0.06f;

    [Header("Diagnostic")]
    [SerializeField] private bool logSetupWarnings = true;

    [Header("Événements")]
    public UnityEvent onWarningStart;
    public UnityEvent onSlamStart;
    public UnityEvent onImpact;

    public bool IsBusy { get; private set; }
    public bool IsDeployed { get; private set; }
    public float CurrentX => transform.position.x;

    private Vector3 restPosition;
    private float railsRestLocalY;
    private float railsDeployedLocalY;
    private float railsRestScaleY;
    private float railsDeployedScaleY;
    private float crusherRestLocalY;
    private float crusherSlamLocalY;
    private SpriteRenderer[] visuals;
    private bool isVisible = true;
    private float hoverY;
    private bool hasHoverPoint;
    private float limitMinX;
    private float limitMaxX;
    private bool hasLimits;

    private void Awake()
    {
        restPosition = transform.position;
        visuals = GetComponentsInChildren<SpriteRenderer>(true);

        CacheRails();
        CacheCrusher();
        CacheMarkers();

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = false;

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);

        if (hideWhenIdle)
            SetVisualsVisible(false);

        LogSetup();
    }

    private void CacheRails()
    {
        if (rails == null)
            return;

        railsRestLocalY = rails.localPosition.y;
        railsDeployedLocalY = railsRestLocalY - railsDeployDistance;

        railsRestScaleY = rails.localScale.y;
        railsDeployedScaleY = railsRestScaleY * railsDeployScaleMultiplier;
    }

    private void CacheCrusher()
    {
        if (crusher == null)
            return;

        crusherRestLocalY = crusher.localPosition.y;
        crusherSlamLocalY = crusherRestLocalY - crusherSlamDistance;
    }

    private void CacheMarkers()
    {
        hasHoverPoint = hoverPoint != null;

        if (hasHoverPoint)
            hoverY = hoverPoint.position.y;

        hasLimits = leftLimit != null && rightLimit != null;

        if (hasLimits)
        {
            limitMinX = Mathf.Min(leftLimit.position.x, rightLimit.position.x);
            limitMaxX = Mathf.Max(leftLimit.position.x, rightLimit.position.x);
        }
    }

    private void LogSetup()
    {
        if (!logSetupWarnings)
            return;

        if (rails == null)
            Debug.LogWarning($"[Stomper] '{name}' : le champ Rails est vide, les rails ne se déploieront pas.", this);

        if (crusher == null)
            Debug.LogWarning($"[Stomper] '{name}' : le champ Crusher est vide, rien n'écrasera le joueur.", this);

        if (crusherKillCollider == null)
            Debug.LogWarning($"[Stomper] '{name}' : aucun Crusher Kill Collider assigné, l'écrasement ne tuera pas.", this);

        if (hoverPoint == null)
            Debug.LogWarning($"[Stomper] '{name}' : aucun Hover Point assigné, le stomper ne descendra pas.", this);
        else if (hoverPoint.IsChildOf(transform))
            Debug.LogWarning($"[Stomper] '{name}' : le Hover Point est un enfant du stomper. Sors-le de la hiérarchie, sinon il se déplace avec la machine.", this);

        if (leftLimit != null && leftLimit.IsChildOf(transform))
            Debug.LogWarning($"[Stomper] '{name}' : le Left Limit est un enfant du stomper, le bornage sera faux.", this);

        if (rightLimit != null && rightLimit.IsChildOf(transform))
            Debug.LogWarning($"[Stomper] '{name}' : le Right Limit est un enfant du stomper, le bornage sera faux.", this);

        if (leftLimit == null || rightLimit == null)
            Debug.LogWarning($"[Stomper] '{name}' : limites gauche/droite incomplètes, le déplacement ne sera pas borné.", this);

        if (screen == null)
            Debug.LogWarning($"[Stomper] '{name}' : aucun StomperScreen assigné, pas d'avertissement à l'écran.", this);
    }

    public float ClampToLimits(float x)
    {
        if (!hasLimits)
            return x;

        return Mathf.Clamp(x, limitMinX, limitMaxX);
    }

    public IEnumerator Strike(float targetX, float warnOverride = -1f)
    {
        if (IsBusy)
            yield break;

        IsBusy = true;

        float clampedX = ClampToLimits(targetX);

        if (!IsDeployed)
        {
            if (hideWhenIdle && !isVisible)
            {
                transform.position = new Vector3(clampedX, restPosition.y, transform.position.z);
                yield return Appear();
            }
            else
            {
                yield return MoveHorizontally(clampedX, StomperScreen.ScreenState.DirectionDown);
            }

            yield return Descend();
            yield return DeployRails();
        }
        else
        {
            yield return SlideDeployed(clampedX);
        }

        yield return WarningPhase(warnOverride < 0f ? warningDuration : warnOverride);
        yield return Slam();

        yield return new WaitForSeconds(pauseBeforeCrusherRetract);
        yield return RetractCrusher();
        yield return new WaitForSeconds(pauseAfterSlam);

        IsBusy = false;
    }

    public IEnumerator ReturnHome()
    {
        if (IsBusy)
            yield break;

        IsBusy = true;

        yield return RetractRails();

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.DirectionUp);

        while (Mathf.Abs(transform.position.y - restPosition.y) > 0.01f)
        {
            Vector3 destination = new Vector3(transform.position.x, restPosition.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, riseSpeed * Time.deltaTime);
            yield return null;
        }

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);

        if (hideWhenIdle)
            yield return Disappear();

        IsDeployed = false;
        IsBusy = false;
    }

    private IEnumerator Appear()
    {
        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.DirectionDown);

        yield return FlickerVisuals(appearFlickerDuration);
        SetVisualsVisible(true);
    }

    private IEnumerator Disappear()
    {
        yield return FlickerVisuals(disappearFlickerDuration);
        SetVisualsVisible(false);
    }

    private IEnumerator FlickerVisuals(float duration)
    {
        float elapsed = 0f;
        bool visible = isVisible;

        while (elapsed < duration)
        {
            visible = !visible;
            SetVisualsVisible(visible);

            float wait = Random.Range(flickerIntervalMin, flickerIntervalMax);
            wait = Mathf.Min(wait, duration - elapsed);

            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }
    }

    private void SetVisualsVisible(bool visible)
    {
        isVisible = visible;

        if (visuals == null)
            return;

        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null)
                visuals[i].enabled = visible;
    }

    private IEnumerator MoveHorizontally(float targetX, StomperScreen.ScreenState state)
    {
        if (screen != null)
            screen.SetState(state);

        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            Vector3 destination = new Vector3(targetX, transform.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, horizontalSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator SlideDeployed(float targetX)
    {
        if (screen != null)
            screen.ShowHorizontalDirection(targetX - transform.position.x);

        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            Vector3 destination = new Vector3(targetX, transform.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, horizontalSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator Descend()
    {
        if (!hasHoverPoint)
            yield break;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.DirectionDown);

        float elapsed = 0f;
        float timeout = Mathf.Abs(restPosition.y - hoverY) / Mathf.Max(0.1f, descendSpeed) + 2f;

        while (Mathf.Abs(transform.position.y - hoverY) > 0.01f && elapsed < timeout)
        {
            Vector3 destination = new Vector3(transform.position.x, hoverY, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, descendSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, hoverY, transform.position.z);
    }

    private IEnumerator DeployRails()
    {
        if (railsKillCollider != null)
            railsKillCollider.enabled = true;

        if (railsExtendByScale)
            yield return ScaleLocalY(rails, railsDeployedScaleY, railsDeploySpeed);
        else
            yield return MoveLocalY(rails, railsDeployedLocalY, railsDeploySpeed);

        IsDeployed = true;
    }

    private IEnumerator RetractRails()
    {
        if (railsExtendByScale)
            yield return ScaleLocalY(rails, railsRestScaleY, railsRetractSpeed);
        else
            yield return MoveLocalY(rails, railsRestLocalY, railsRetractSpeed);

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;
    }

    private IEnumerator WarningPhase(float duration)
    {
        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Warning);

        onWarningStart?.Invoke();

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator Slam()
    {
        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Stomp);

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = true;

        onSlamStart?.Invoke();

        yield return MoveLocalY(crusher, crusherSlamLocalY, slamSpeed);

        if (shakeOnImpact && CameraShake.Instance != null)
        {
            CameraShake.Instance.Punch(Vector2.down, impactPunchMagnitude, impactShakeDuration, impactShakeMagnitude);
            CameraShake.HitStop(impactHitStop);
        }

        onImpact?.Invoke();

        yield return new WaitForSeconds(slamHoldDuration);
    }

    private IEnumerator RetractCrusher()
    {
        yield return MoveLocalY(crusher, crusherRestLocalY, crusherRetractSpeed);

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = false;
    }

    private IEnumerator MoveLocalY(Transform target, float localY, float speed)
    {
        if (target == null)
            yield break;

        while (Mathf.Abs(target.localPosition.y - localY) > 0.001f)
        {
            float next = Mathf.MoveTowards(target.localPosition.y, localY, speed * Time.deltaTime);
            SetLocalY(target, next);
            yield return null;
        }

        SetLocalY(target, localY);
    }

    private IEnumerator ScaleLocalY(Transform target, float scaleY, float speed)
    {
        if (target == null)
            yield break;

        while (Mathf.Abs(target.localScale.y - scaleY) > 0.001f)
        {
            float next = Mathf.MoveTowards(target.localScale.y, scaleY, speed * Time.deltaTime);
            SetLocalScaleY(target, next);
            yield return null;
        }

        SetLocalScaleY(target, scaleY);
    }

    private void SetLocalY(Transform target, float localY)
    {
        if (target == null)
            return;

        Vector3 position = target.localPosition;
        position.y = localY;
        target.localPosition = position;
    }

    private void SetLocalScaleY(Transform target, float scaleY)
    {
        if (target == null)
            return;

        Vector3 scale = target.localScale;
        scale.y = scaleY;
        target.localScale = scale;
    }

    public void ForceReset()
    {
        StopAllCoroutines();

        transform.position = restPosition;

        if (railsExtendByScale)
            SetLocalScaleY(rails, railsRestScaleY);
        else
            SetLocalY(rails, railsRestLocalY);

        SetLocalY(crusher, crusherRestLocalY);

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = false;

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);

        SetVisualsVisible(!hideWhenIdle);

        IsBusy = false;
        IsDeployed = false;
    }
}