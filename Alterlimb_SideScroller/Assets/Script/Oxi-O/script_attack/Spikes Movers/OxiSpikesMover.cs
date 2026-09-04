using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class OxiSpikeMover : MonoBehaviour
{
    [Header("Trajet")]
    [SerializeField] private Transform movingRoot;
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform rightPoint;
    [SerializeField] private float travelSpeed = 9f;
    [SerializeField] private float overshoot = 2f;
    [SerializeField] private bool lockVertical = true;

    [Header("Visuel")]
    [SerializeField] private bool hideWhenIdle = true;
    [SerializeField] private bool flipOnDirection = false;

    [Header("Collision")]
    [SerializeField] private Collider2D killCollider;
    [SerializeField] private bool autoCollectColliders = true;

    [Header("Avertissement")]
    [SerializeField] private SpikeWarningUI warningUI;
    [SerializeField] private float warnDuration = 1.1f;

    [Header("Événements")]
    public UnityEvent onWarningStart;
    public UnityEvent onTravelStart;
    public UnityEvent onTravelEnd;

    [Header("Diagnostic")]
    [SerializeField] private bool logSetupWarnings = true;

    public bool IsBusy { get; private set; }

    private SpriteRenderer[] visuals;
    private Collider2D[] colliders;
    private Vector3 baseLocalScale = Vector3.one;
    private float travelY;

    private void Awake()
    {
        if (movingRoot == null)
            movingRoot = transform;

        baseLocalScale = movingRoot.localScale;
        travelY = movingRoot.position.y;

        visuals = movingRoot.GetComponentsInChildren<SpriteRenderer>(true);

        colliders = autoCollectColliders
            ? movingRoot.GetComponentsInChildren<Collider2D>(true)
            : (killCollider != null ? new Collider2D[] { killCollider } : new Collider2D[0]);

        SetVisible(false);
        SetCollidersEnabled(false);

        LogSetup();
    }

    private void LogSetup()
    {
        if (!logSetupWarnings)
            return;

        if (leftPoint == null || rightPoint == null)
        {
            Debug.LogError($"[OxiSpikeMover] '{name}' : Left Point ou Right Point manquant, les piques ne se déplaceront pas.", this);
            return;
        }

        if (leftPoint.position.x > rightPoint.position.x)
            Debug.LogError($"[OxiSpikeMover] '{name}' : Left Point est à droite de Right Point. Inverse-les, sinon les directions seront à l'envers.", this);

        if (leftPoint.IsChildOf(transform) || rightPoint.IsChildOf(transform))
            Debug.LogError($"[OxiSpikeMover] '{name}' : un des repères est enfant de l'objet qui se déplace, il fuira devant les piques.", this);

        if (colliders == null || colliders.Length == 0)
            Debug.LogWarning($"[OxiSpikeMover] '{name}' : aucun collider trouvé sous '{movingRoot.name}', les piques ne tueront personne.", this);

        if (visuals == null || visuals.Length == 0)
            Debug.LogWarning($"[OxiSpikeMover] '{name}' : aucun SpriteRenderer sous '{movingRoot.name}', rien ne sera visible.", this);

        if (warningUI == null)
            Debug.LogWarning($"[OxiSpikeMover] '{name}' : aucun Warning UI assigné, le joueur ne verra pas d'où ça vient.", this);
    }

    public IEnumerator Travel(bool fromLeft, float warnOverride = -1f)
    {
        if (IsBusy || leftPoint == null || rightPoint == null)
            yield break;

        IsBusy = true;

        float direction = fromLeft ? 1f : -1f;

        Vector3 start = fromLeft ? leftPoint.position : rightPoint.position;
        Vector3 end = fromLeft ? rightPoint.position : leftPoint.position;
        end += Vector3.right * (overshoot * direction);

        if (lockVertical)
        {
            start.y = travelY;
            end.y = travelY;
        }

        movingRoot.position = start;
        SetVisible(false);
        SetCollidersEnabled(false);

        if (flipOnDirection)
            movingRoot.localScale = new Vector3(baseLocalScale.x * direction, baseLocalScale.y, baseLocalScale.z);

        onWarningStart?.Invoke();

        float warn = warnOverride < 0f ? warnDuration : warnOverride;

        if (warningUI != null)
            yield return warningUI.Warn(fromLeft, warn);
        else
            yield return new WaitForSeconds(warn);

        SetVisible(true);
        SetCollidersEnabled(true);
        onTravelStart?.Invoke();

        while (Mathf.Abs(movingRoot.position.x - end.x) > 0.05f)
        {
            movingRoot.position = Vector3.MoveTowards(movingRoot.position, end, travelSpeed * Time.deltaTime);
            yield return null;
        }

        SetVisible(false);
        SetCollidersEnabled(false);
        onTravelEnd?.Invoke();

        IsBusy = false;
    }

    public void ForceReset()
    {
        StopAllCoroutines();

        SetVisible(false);
        SetCollidersEnabled(false);

        if (warningUI != null)
            warningUI.HideAll();

        if (leftPoint != null)
        {
            Vector3 home = leftPoint.position;
            if (lockVertical) home.y = travelY;
            movingRoot.position = home;
        }

        movingRoot.localScale = baseLocalScale;
        IsBusy = false;
    }

    private void SetVisible(bool visible)
    {
        if (!hideWhenIdle && !visible)
            return;

        if (visuals == null)
            return;

        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null)
                visuals[i].enabled = visible;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
    }

    private void OnDrawGizmosSelected()
    {
        if (leftPoint == null || rightPoint == null)
            return;

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawLine(leftPoint.position, rightPoint.position);
        Gizmos.DrawWireSphere(leftPoint.position, 0.4f);
        Gizmos.DrawWireSphere(rightPoint.position, 0.4f);
    }
}