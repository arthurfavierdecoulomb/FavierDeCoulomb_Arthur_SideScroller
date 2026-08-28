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
    [SerializeField] private float railsHiddenLocalY = 0f;
    [SerializeField] private float railsDeployedLocalY = -4.5f;
    [SerializeField] private float railsHiddenScaleY = 0.15f;
    [SerializeField] private float railsDeployedScaleY = 1f;
    [SerializeField] private float railsDeploySpeed = 22f;
    [SerializeField] private float railsRetractSpeed = 10f;

    [Header("Écraseur")]
    [SerializeField] private float crusherHiddenLocalY = 0f;
    [SerializeField] private float crusherSlamLocalY = -4.2f;
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

    [Header("Événements")]
    public UnityEvent onWarningStart;
    public UnityEvent onSlamStart;
    public UnityEvent onImpact;

    public bool IsBusy { get; private set; }
    public bool IsDeployed { get; private set; }
    public float CurrentX => transform.position.x;

    private Vector3 restPosition;

    private void Awake()
    {
        restPosition = transform.position;

        ApplyRailsRetracted();
        SetLocalY(crusher, crusherHiddenLocalY);

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = false;

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);
    }

    public float ClampToLimits(float x)
    {
        float min = leftLimit != null ? leftLimit.position.x : x;
        float max = rightLimit != null ? rightLimit.position.x : x;

        if (min > max)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        return Mathf.Clamp(x, min, max);
    }

    public IEnumerator Strike(float targetX, float warnOverride = -1f)
    {
        if (IsBusy)
            yield break;

        IsBusy = true;

        float clampedX = ClampToLimits(targetX);

        if (!IsDeployed)
        {
            yield return MoveHorizontally(clampedX, StomperScreen.ScreenState.DirectionDown);
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
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(transform.position.x, restPosition.y, transform.position.z), riseSpeed * Time.deltaTime);
            yield return null;
        }

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);

        IsDeployed = false;
        IsBusy = false;
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
        if (hoverPoint == null)
            yield break;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.DirectionDown);

        while (Mathf.Abs(transform.position.y - hoverPoint.position.y) > 0.01f)
        {
            Vector3 destination = new Vector3(transform.position.x, hoverPoint.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, descendSpeed * Time.deltaTime);
            yield return null;
        }
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
            yield return ScaleLocalY(rails, railsHiddenScaleY, railsRetractSpeed);
        else
            yield return MoveLocalY(rails, railsHiddenLocalY, railsRetractSpeed);

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;
    }

    private void ApplyRailsRetracted()
    {
        if (railsExtendByScale)
            SetLocalScaleY(rails, railsHiddenScaleY);
        else
            SetLocalY(rails, railsHiddenLocalY);
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

        onImpact?.Invoke();

        yield return new WaitForSeconds(slamHoldDuration);
    }

    private IEnumerator RetractCrusher()
    {
        yield return MoveLocalY(crusher, crusherHiddenLocalY, crusherRetractSpeed);

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

    private void SetLocalScaleY(Transform target, float scaleY)
    {
        if (target == null)
            return;

        Vector3 scale = target.localScale;
        scale.y = scaleY;
        target.localScale = scale;
    }

    private void SetLocalY(Transform target, float localY)
    {
        if (target == null)
            return;

        Vector3 position = target.localPosition;
        position.y = localY;
        target.localPosition = position;
    }

    public void ForceReset()
    {
        StopAllCoroutines();

        transform.position = restPosition;
        ApplyRailsRetracted();
        SetLocalY(crusher, crusherHiddenLocalY);

        if (crusherKillCollider != null)
            crusherKillCollider.enabled = false;

        if (railsKillCollider != null)
            railsKillCollider.enabled = false;

        if (screen != null)
            screen.SetState(StomperScreen.ScreenState.Off);

        IsBusy = false;
        IsDeployed = false;
    }
}