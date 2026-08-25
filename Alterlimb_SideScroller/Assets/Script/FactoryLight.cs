using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum FactoryLightMode { Auto, Steady, Flicker, Dead }

public class FactoryLight : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] Light2D targetLight;
    [SerializeField] SpriteRenderer bulbRenderer;

    [Header("Mode")]
    [SerializeField] FactoryLightMode mode = FactoryLightMode.Auto;
    [SerializeField, Range(0f, 1f)] float deadChance = 0.12f;
    [SerializeField, Range(0f, 1f)] float flickerChance = 0.25f;

    [Header("Couleur")]
    [SerializeField] bool applyColor = true;
    [SerializeField] Color lightColor = new Color(1f, 0.62f, 0.28f);

    [Header("Respiration")]
    [SerializeField] float minIntensity = 0.55f;
    [SerializeField] float maxIntensity = 1f;
    [SerializeField] float breathSpeed = 0.35f;

    [Header("Clignotement")]
    [SerializeField] Vector2 onDurationRange = new Vector2(0.8f, 4f);
    [SerializeField] Vector2 offDurationRange = new Vector2(0.04f, 0.3f);
    [SerializeField] float flickerFade = 0.05f;
    [SerializeField, Range(0f, 0.5f)] float flickerFloor = 0.08f;

    [Header("Ampoule")]
    [SerializeField, Range(0f, 1f)] float bulbMinBrightness = 0.2f;

    FactoryLightMode resolvedMode;
    Color bulbBaseColor;
    float noiseSeed;
    float flickerTimer;
    float flickerTarget = 1f;
    float flickerValue = 1f;
    bool flickerOn = true;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponent<Light2D>();
        if (targetLight == null) targetLight = GetComponentInChildren<Light2D>();
        if (bulbRenderer != null) bulbBaseColor = bulbRenderer.color;

        float roll = HashFromPosition(0.37f);
        noiseSeed = HashFromPosition(7.13f) * 100f;

        resolvedMode = mode;
        if (mode == FactoryLightMode.Auto)
        {
            if (roll < deadChance) resolvedMode = FactoryLightMode.Dead;
            else if (roll < deadChance + flickerChance) resolvedMode = FactoryLightMode.Flicker;
            else resolvedMode = FactoryLightMode.Steady;
        }

        if (targetLight != null && applyColor) targetLight.color = lightColor;

        flickerTimer = Random.Range(0f, onDurationRange.y);
    }

    void Update()
    {
        float intensity;

        switch (resolvedMode)
        {
            case FactoryLightMode.Dead:
                intensity = 0f;
                break;
            case FactoryLightMode.Flicker:
                intensity = Breathe() * UpdateFlicker();
                break;
            default:
                intensity = Breathe();
                break;
        }

        Apply(intensity);
    }

    float Breathe()
    {
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * breathSpeed);
        float shaped = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0.75f, noise));
        return Mathf.Lerp(minIntensity, maxIntensity, shaped);
    }

    float UpdateFlicker()
    {
        flickerTimer -= Time.deltaTime;

        if (flickerTimer <= 0f)
        {
            flickerOn = !flickerOn;
            flickerTimer = flickerOn
                ? Random.Range(onDurationRange.x, onDurationRange.y)
                : Random.Range(offDurationRange.x, offDurationRange.y);
            flickerTarget = flickerOn ? 1f : Random.Range(0f, flickerFloor);
        }

        flickerValue = flickerFade <= 0f
            ? flickerTarget
            : Mathf.MoveTowards(flickerValue, flickerTarget, Time.deltaTime / flickerFade);

        return flickerValue;
    }

    void Apply(float intensity)
    {
        if (targetLight != null)
            targetLight.intensity = intensity;

        if (bulbRenderer == null) return;

        float t = Mathf.Clamp01(intensity / Mathf.Max(maxIntensity, 0.0001f));
        Color tinted = bulbBaseColor * Mathf.Lerp(bulbMinBrightness, 1f, t);
        tinted.a = bulbBaseColor.a;
        bulbRenderer.color = tinted;
    }

    float HashFromPosition(float offset)
    {
        Vector3 p = transform.position;
        float value = Mathf.Abs(Mathf.Sin((p.x + offset) * 12.9898f + (p.y - offset) * 78.233f) * 43758.5453f);
        return value - Mathf.Floor(value);
    }
}