using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Bruit")]
    [SerializeField] private float noiseFrequency = 26f;
    [SerializeField] private float directionalDecayPower = 2.5f;
    [SerializeField] private float noiseDecayPower = 1.8f;

    [Header("Rotation")]
    [SerializeField] private bool shakeRotation = false;
    [SerializeField] private float maxRotationAngle = 1.2f;

    [Header("Pixel perfect")]
    [SerializeField] private bool snapToPixel = false;
    [SerializeField] private float pixelsPerUnit = 100f;

    [Header("Limites")]
    [SerializeField] private float maxMagnitude = 2.5f;

    private float duration;
    private float elapsed;
    private float magnitude;
    private Vector2 kickDirection;
    private float kickMagnitude;
    private float seedX;
    private float seedY;

    private Vector3 appliedOffset;
    private float appliedRotation;
    private bool offsetApplied;

    private void Awake()
    {
        Instance = this;
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        RemoveOffset();
    }

    private void LateUpdate()
    {
        if (duration <= 0f)
            return;

        elapsed += Time.unscaledDeltaTime;

        if (elapsed >= duration)
        {
            duration = 0f;
            return;
        }

        float t = elapsed / duration;

        float noiseFalloff = Mathf.Pow(1f - t, noiseDecayPower);
        float kickFalloff = Mathf.Pow(1f - t, directionalDecayPower);

        float time = elapsed * noiseFrequency;
        float noiseX = (Mathf.PerlinNoise(seedX, time) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(seedY, time) - 0.5f) * 2f;

        Vector3 offset = new Vector3(noiseX, noiseY, 0f) * magnitude * noiseFalloff;
        offset += (Vector3)(kickDirection * kickMagnitude * kickFalloff);

        offset = Vector3.ClampMagnitude(offset, maxMagnitude);

        if (snapToPixel && pixelsPerUnit > 0f)
        {
            offset.x = Mathf.Round(offset.x * pixelsPerUnit) / pixelsPerUnit;
            offset.y = Mathf.Round(offset.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position += offset;
        appliedOffset = offset;
        offsetApplied = true;

        if (shakeRotation)
        {
            appliedRotation = noiseX * maxRotationAngle * noiseFalloff;
            transform.Rotate(0f, 0f, appliedRotation);
        }
    }

    private void RemoveOffset()
    {
        if (!offsetApplied)
            return;

        transform.position -= appliedOffset;

        if (shakeRotation)
            transform.Rotate(0f, 0f, -appliedRotation);

        appliedOffset = Vector3.zero;
        appliedRotation = 0f;
        offsetApplied = false;
    }

    public void Shake(float shakeDuration, float shakeMagnitude)
    {
        Punch(Vector2.zero, 0f, shakeDuration, shakeMagnitude);
    }

    public void Punch(Vector2 direction, float punchMagnitude, float shakeDuration, float shakeMagnitude)
    {
        if (shakeDuration <= 0f)
            return;

        if (duration > 0f)
        {
            float remaining = duration - elapsed;

            if (remaining > shakeDuration && shakeMagnitude <= magnitude)
                return;
        }

        duration = shakeDuration;
        elapsed = 0f;
        magnitude = shakeMagnitude;
        kickDirection = direction.normalized;
        kickMagnitude = punchMagnitude;

        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    public void StopShake()
    {
        RemoveOffset();
        duration = 0f;
    }

    public static void HitStop(float seconds)
    {
        if (Instance == null || seconds <= 0f)
            return;

        Instance.StartCoroutine(Instance.HitStopRoutine(seconds));
    }

    private IEnumerator HitStopRoutine(float seconds)
    {
        if (Time.timeScale == 0f)
            yield break;

        float previous = Time.timeScale;
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(seconds);

        Time.timeScale = previous;
    }
}