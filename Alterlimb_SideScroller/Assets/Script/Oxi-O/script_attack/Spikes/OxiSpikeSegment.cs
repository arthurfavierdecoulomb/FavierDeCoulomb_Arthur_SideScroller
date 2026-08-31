using System.Collections;
using UnityEngine;

public class OxiSpikeSegment : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform spikes;
    [SerializeField] private Collider2D killCollider;
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private SpriteRenderer warningRenderer;

    [Header("Zone sûre")]
    [SerializeField] private GameObject safeRoot;
    [SerializeField] private float safeBlinkInterval = 0.22f;

    [Header("Masquage")]
    [SerializeField] private float hideDistance = 2f;

    [Header("Vitesses")]
    [SerializeField] private float riseSpeed = 40f;
    [SerializeField] private float retractSpeed = 7f;

    [Header("Warning")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private float warningBlinkInterval = 0.08f;
    [SerializeField] private float warningBlinkAcceleration = 0.85f;
    [SerializeField] private float warningMinInterval = 0.03f;

    [Header("Diagnostic")]
    [SerializeField] private bool logSetupWarnings = true;

    public bool IsBusy { get; private set; }
    public float WorldX => transform.position.x;

    private float raisedLocalY;
    private float hiddenLocalY;

    private void Awake()
    {
        if (spikes == null)
        {
            spikes = transform;

            if (logSetupWarnings)
                Debug.LogWarning($"[OxiSpikeSegment] '{name}' : le champ Spikes est vide, le segment entier va bouger.", this);
        }

        raisedLocalY = spikes.localPosition.y;
        hiddenLocalY = raisedLocalY - hideDistance;

        SetSpikeHeight(hiddenLocalY);

        if (killCollider == null && logSetupWarnings)
            Debug.LogWarning($"[OxiSpikeSegment] '{name}' : aucun Kill Collider assigné, ce segment ne tuera pas.", this);

        if (killCollider != null)
            killCollider.enabled = false;

        if (warningRenderer != null)
            warningRenderer.color = warningColor;

        if (!HasWarningVisual() && logSetupWarnings)
            Debug.LogWarning($"[OxiSpikeSegment] '{name}' : ni Warning Root ni Warning Renderer assigné, l'avertissement restera visible en permanence.", this);

        SetWarningVisible(false);
        SetSafeVisible(false);
    }

    public void ShowSafe(float duration)
    {
        if (safeRoot == null || IsBusy)
            return;

        StartCoroutine(SafeRoutine(duration));
    }

    private IEnumerator SafeRoutine(float duration)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            SetSafeVisible(visible);
            visible = !visible;

            float wait = Mathf.Min(safeBlinkInterval, duration - elapsed);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        SetSafeVisible(false);
    }

    private void SetSafeVisible(bool visible)
    {
        if (safeRoot != null)
            safeRoot.SetActive(visible);
    }

    public void StrikeNow(float warnDuration, float stayDuration)
    {
        if (IsBusy)
            return;

        StartCoroutine(Strike(warnDuration, stayDuration));
    }

    private IEnumerator Strike(float warnDuration, float stayDuration)
    {
        IsBusy = true;

        yield return Warn(warnDuration);
        yield return Rise();

        yield return new WaitForSeconds(stayDuration);

        yield return Retract();

        IsBusy = false;
    }

    private IEnumerator Warn(float warnDuration)
    {
        if (!HasWarningVisual())
        {
            yield return new WaitForSeconds(warnDuration);
            yield break;
        }

        float elapsed = 0f;
        float interval = warningBlinkInterval;
        bool visible = true;

        while (elapsed < warnDuration)
        {
            SetWarningVisible(visible);
            visible = !visible;

            float wait = Mathf.Min(interval, warnDuration - elapsed);
            yield return new WaitForSeconds(wait);

            elapsed += wait;
            interval = Mathf.Max(warningMinInterval, interval * warningBlinkAcceleration);
        }

        SetWarningVisible(false);
    }

    private IEnumerator Rise()
    {
        if (killCollider != null)
            killCollider.enabled = true;

        while (spikes.localPosition.y < raisedLocalY - 0.001f)
        {
            float next = Mathf.MoveTowards(spikes.localPosition.y, raisedLocalY, riseSpeed * Time.deltaTime);
            SetSpikeHeight(next);
            yield return null;
        }

        SetSpikeHeight(raisedLocalY);
    }

    private IEnumerator Retract()
    {
        while (spikes.localPosition.y > hiddenLocalY + 0.001f)
        {
            float next = Mathf.MoveTowards(spikes.localPosition.y, hiddenLocalY, retractSpeed * Time.deltaTime);
            SetSpikeHeight(next);
            yield return null;
        }

        SetSpikeHeight(hiddenLocalY);

        if (killCollider != null)
            killCollider.enabled = false;
    }

    public void ForceReset()
    {
        StopAllCoroutines();
        IsBusy = false;
        SetSpikeHeight(hiddenLocalY);

        if (killCollider != null)
            killCollider.enabled = false;

        SetWarningVisible(false);
        SetSafeVisible(false);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningRoot != null)
        {
            warningRoot.SetActive(visible);
            return;
        }

        if (warningRenderer != null)
            warningRenderer.enabled = visible;
    }

    private bool HasWarningVisual()
    {
        return warningRoot != null || warningRenderer != null;
    }

    private void SetSpikeHeight(float localY)
    {
        Vector3 position = spikes.localPosition;
        position.y = localY;
        spikes.localPosition = position;
    }
}