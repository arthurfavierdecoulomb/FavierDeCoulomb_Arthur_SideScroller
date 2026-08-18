using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static UnityEngine.UI.Image;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class LaserBeam : MonoBehaviour

{
    [Header("Laser")]
    [SerializeField] private Transform origin;
    [SerializeField] private Transform destination;



    [Header("Lumière 2D")]
    [Tooltip("Freeform / Sprite / Parametric Light 2D à synchroniser avec le laser")]
    [SerializeField] private Light2D beamLight;
    [Tooltip("Lumières supplémentaires (optionnel)")]
    [SerializeField] private Light2D[] extraLights;
    [Tooltip("Intensité de la lumière quand le laser est allumé")]
    [SerializeField] private float lightIntensity = 1f;
    [Tooltip("Si coché : fondu doux au lieu d'un on/off sec")]
    [SerializeField] private bool smoothFade = false;
    [SerializeField] private float fadeSpeed = 12f;

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

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _collider = GetComponent<EdgeCollider2D>();

        _line.positionCount = 2;
        _collider.isTrigger = true;
        _collider.edgeRadius = 0.2f;

        if (beamLight != null && lightIntensity <= 0)
            lightIntensity = beamLight.intensity;

    }

    void Start()
    {
        StartCoroutine(LaserCycle());
    }

    
    void Update()
    {
        if (_beamActive)
            UpdatePositions();

        if (smoothFade)
            UpdateLightFade();

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
            yield return new WaitForSeconds(onDuration);


        }
    }

    IEnumerator BlinkRoutine()
    { for (int i = 0; i < blinkCount; i++)
        {
            SetBeamActive(false);
            yield return new WaitForSeconds(blinkInterval);
            SetBeamActive(true);
            yield return new WaitForSeconds(blinkInterval);
        }
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

        SetLights(active);
    }

    void SetLights(bool active)
    {
        if (smoothFade)
        {
            // Le fondu est géré dans UpdateLightFade(), on garde les lumières activées
            ForEachLight(l => l.enabled = true);
        }
        else
        {
            ForEachLight(l =>
            {
                l.enabled = active;
                l.intensity = active ? lightIntensity : 0f;
            });
        }
    }

    void UpdateLightFade()
    {
        float target = _beamActive ? lightIntensity : 0f;
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




