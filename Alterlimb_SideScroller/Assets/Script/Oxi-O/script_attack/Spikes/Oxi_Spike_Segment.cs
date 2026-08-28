using System.Collections;
using UnityEngine;

public class OxiSpikeSegment : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform spikes;
    [SerializeField] private Collider2D killCollider;
    [SerializeField] private SpriteRenderer warningRenderer;

    [Header("Positions locales")]
    [SerializeField] private float hiddenLocalY = -1.5f;
    [SerializeField] private float raisedLocalY = 0f;

    [Header("Vitesses")]
    [SerializeField] private float riseSpeed = 40f;
    [SerializeField] private float retractSpeed = 7f;

    [Header("Warning")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private float warningBlinkInterval = 0.08f;
    [SerializeField] private float warningBlinkAcceleration = 0.85f;
    [SerializeField] private float warningMinInterval = 0.03f;

    public bool IsBusy { get; private set; }
    public float WorldX => transform.position.x;

    private void Awake()
    {
        if (spikes == null)
            spikes = transform;

        SetSpikeHeight(hiddenLocalY);

        if (killCollider != null)
            killCollider.enabled = false;

        if (warningRenderer != null)
        {
            warningRenderer.color = warningColor;
            warningRenderer.enabled = false;
        }
    }

    public IEnumerator Strike(float warnDuration, float stayDuration)
    {
        if (IsBusy)
            yield break;

        IsBusy = true;

        yield return Warn(warnDuration);
        yield return Rise();

        yield return new WaitForSeconds(stayDuration);

        yield return Retract();

        IsBusy = false;
    }

    private IEnumerator Warn(float warnDuration)
    {
        if (warningRenderer == null)
        {
            yield return new WaitForSeconds(warnDuration);
            yield break;
        }

        float elapsed = 0f;
        float interval = warningBlinkInterval;
        bool visible = true;

        while (elapsed < warnDuration)
        {
            warningRenderer.enabled = visible;
            visible = !visible;

            float wait = Mathf.Min(interval, warnDuration - elapsed);
            yield return new WaitForSeconds(wait);

            elapsed += wait;
            interval = Mathf.Max(warningMinInterval, interval * warningBlinkAcceleration);
        }

        warningRenderer.enabled = false;
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

        if (warningRenderer != null)
            warningRenderer.enabled = false;
    }

    private void SetSpikeHeight(float localY)
    {
        Vector3 position = spikes.localPosition;
        position.y = localY;
        spikes.localPosition = position;
    }
}