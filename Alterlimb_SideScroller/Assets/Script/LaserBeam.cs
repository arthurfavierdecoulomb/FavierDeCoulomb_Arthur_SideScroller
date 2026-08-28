using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum LaserMode
{
    Autonomous,
    Controlled
}

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class LaserBeam : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private LaserMode mode = LaserMode.Autonomous;
    [SerializeField] private bool startActiveWhenControlled = false;

    [Header("Laser")]
    [SerializeField] private Transform origin;
    [SerializeField] private Transform destination;

    [Header("Lumière 2D")]
    [SerializeField] private Light2D beamLight;
    [SerializeField] private Light2D[] extraLights;
    [SerializeField] private float lightIntensity = 1f;
    [SerializeField] private bool smoothFade = false;
    [SerializeField] private float fadeSpeed = 12f;

    [Header("Atténuation")]
    [SerializeField] private bool dimLineWidth = true;
    [SerializeField] private float intensityLerpSpeed = 6f;

    [Header("Timing")]
    [SerializeField] private float onDurationMin = 1f;
    [SerializeField] private float onDurationMax = 2f;
    [SerializeField] private float offDurationMin = 1f;
    [SerializeField] private float offDurationMax = 2f;

    [Header("Blink")]
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private int blinkCount = 5;

    private LineRenderer _line;
    private EdgeCollider2D _collider;
    private bool _beamActive = true;
    private float _baseLineWidth;
    private float _intensityMultiplier = 1f;
    private float _targetIntensityMultiplier = 1f;
    private Coroutine _controlledRoutine;

    public bool IsActive => _beamActive;
    public LaserMode Mode => mode;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _collider = GetComponent<EdgeCollider2D>();
        _line.positionCount = 2;
        _collider.isTrigger = true;
        _collider.edgeRadius = 0.2f;
        _baseLineWidth = _line.widthMultiplier;

        if (beamLight != null && lightIntensity <= 0)
            lightIntensity = beamLight.intensity;
    }

    void Start()
    {
        if (mode == LaserMode.Autonomous)
        {
            StartCoroutine(LaserCycle());
            return;
        }

        SetBeamActive(startActiveWhenControlled);
    }

    void Update()
    {
        if (_beamActive)
            UpdatePositions();

        _intensityMultiplier = Mathf.MoveTowards(_intensityMultiplier, _targetIntensityMultiplier, intensityLerpSpeed * Time.deltaTime);

        if (dimLineWidth)
            _line.widthMultiplier = _baseLineWidth * Mathf.Max(0.05f, _intensityMultiplier);

        if (smoothFade)
            UpdateLightFade();
        else if (_beamActive)
            ForEachLight(l => l.intensity = lightIntensity * _intensityMultiplier);
    }

    IEnumerator LaserCycle()
    {
        while (true)
        {
            SetBeamActive(true);

            float onDuration = Random.Range(onDurationMin, onDurationMax);
            yield return new WaitForSeconds(onDuration);

            yield return StartCoroutine(BlinkRoutine());

            SetBeamActive(false);

            float offDuration = Random.Range(offDurationMin, offDurationMax);
            yield return new WaitForSeconds(offDuration);
        }
    }

    IEnumerator BlinkRoutine()
    {
        for (int i = 0; i < blinkCount; i++)
        {
            SetBeamActive(false);
            yield return new WaitForSeconds(blinkInterval);
            SetBeamActive(true);
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    public void TurnOn()
    {
        StopControlledRoutine();
        SetBeamActive(true);
    }

    public void TurnOff()
    {
        StopControlledRoutine();
        SetBeamActive(false);
    }

    public void PowerUpWithFlicker(int flickers = -1)
    {
        StopControlledRoutine();
        _controlledRoutine = StartCoroutine(PowerUpRoutine(flickers < 0 ? blinkCount : flickers));
    }

    public void FlickerWhileOn(int flickers = -1)
    {
        if (!_beamActive)
            return;

        StopControlledRoutine();
        _controlledRoutine = StartCoroutine(FlickerRoutine(flickers < 0 ? blinkCount : flickers));
    }

    public void SetIntensityMultiplier(float multiplier)
    {
        _targetIntensityMultiplier = Mathf.Clamp01(multiplier);
    }

    public void SetIntensityMultiplierInstant(float multiplier)
    {
        _targetIntensityMultiplier = Mathf.Clamp01(multiplier);
        _intensityMultiplier = _targetIntensityMultiplier;
    }

    private IEnumerator PowerUpRoutine(int flickers)
    {
        for (int i = 0; i < flickers; i++)
        {
            SetBeamActive(true);
            yield return new WaitForSeconds(blinkInterval);
            SetBeamActive(false);
            yield return new WaitForSeconds(blinkInterval);
        }

        SetBeamActive(true);
        _controlledRoutine = null;
    }

    private IEnumerator FlickerRoutine(int flickers)
    {
        for (int i = 0; i < flickers; i++)
        {
            SetBeamActive(false);
            yield return new WaitForSeconds(blinkInterval);
            SetBeamActive(true);
            yield return new WaitForSeconds(blinkInterval);
        }

        _controlledRoutine = null;
    }

    private void StopControlledRoutine()
    {
        if (_controlledRoutine == null)
            return;

        StopCoroutine(_controlledRoutine);
        _controlledRoutine = null;
    }

    void UpdatePositions()
    {
        Vector3 start = origin.position;
        Vector3 end = destination.position;

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);

        Vector2 localStart = transform.InverseTransformPoint(start);
        Vector2 localEnd = transform.InverseTransformPoint(end);
        _collider.SetPoints(new List<Vector2> { localStart, localEnd });
    }

    void SetBeamActive(bool active)
    {
        _beamActive = active;
        _line.enabled = active;
        _collider.enabled = active;

        if (active)
            UpdatePositions();

        SetLights(active);
    }

    void SetLights(bool active)
    {
        if (smoothFade)
        {
            ForEachLight(l => l.enabled = true);
            return;
        }

        ForEachLight(l =>
        {
            l.enabled = active;
            l.intensity = active ? lightIntensity * _intensityMultiplier : 0f;
        });
    }

    void UpdateLightFade()
    {
        float target = _beamActive ? lightIntensity * _intensityMultiplier : 0f;
        ForEachLight(l =>
            l.intensity = Mathf.MoveTowards(l.intensity, target, fadeSpeed * Time.deltaTime));
    }

    void ForEachLight(System.Action<Light2D> action)
    {
        if (beamLight != null) action(beamLight);
        if (extraLights == null) return;

        for (int i = 0; i < extraLights.Length; i++)
            if (extraLights[i] != null) action(extraLights[i]);
    }
}