using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class LaserBeam : MonoBehaviour
{
    // ─── Références ───────────────────────────────────────────
    [Header("Points du laser")]
    [SerializeField] private Transform origin;
    [SerializeField] private Transform destination;

    // ─── Timing ───────────────────────────────────────────────
    [Header("Timing")]
    [SerializeField] private float onDurationMin = 1f;
    [SerializeField] private float onDurationMax = 2f;
    [SerializeField] private float offDurationMin = 1f;
    [SerializeField] private float offDurationMax = 2f;

    [Header("Blink")]
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private int blinkCount = 5;

    // ─── Privés ───────────────────────────────────────────────
    private LineRenderer _line;
    private EdgeCollider2D _collider;
    private bool _beamActive = true;

    // ──────────────────────────────────────────────────────────
    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _collider = GetComponent<EdgeCollider2D>();

        _line.positionCount = 2;
        _collider.isTrigger = true;
        _collider.edgeRadius = 0.2f;
    }

    void Start()
    {
        StartCoroutine(LaserCycle());
    }

    void Update()
    {
        if (_beamActive)
            UpdatePositions();
    }

    // ─── Cycle principal ──────────────────────────────────────
    IEnumerator LaserCycle()
    {
        while (true)
        {
            // 1. Allumé pendant X secondes (aléatoire)
            SetBeamActive(true);
            float onDuration = Random.Range(onDurationMin, onDurationMax);
            yield return new WaitForSeconds(onDuration);

            // 2. Blink avant extinction
            yield return StartCoroutine(BlinkRoutine());

            // 3. Éteint pendant X secondes (aléatoire)
            SetBeamActive(false);
            float offDuration = Random.Range(offDurationMin, offDurationMax);
            yield return new WaitForSeconds(offDuration);
        }
    }

    // ─── Blink ────────────────────────────────────────────────
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

    // ─── Mise à jour visuel + collider ────────────────────────
    void UpdatePositions()
    {
        Vector3 start = origin.position;
        Vector3 end = destination.position;

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);

        // Convertit en local space pour le collider
        Vector2 localStart = transform.InverseTransformPoint(start);
        Vector2 localEnd = transform.InverseTransformPoint(end);

        _collider.SetPoints(new List<Vector2> { localStart, localEnd });
    }

    // ─── Active / désactive visuellement + hitbox ─────────────
    void SetBeamActive(bool active)
    {
        _beamActive = active;
        _line.enabled = active;
        _collider.enabled = active;
    }
}