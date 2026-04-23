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

    // ─── États ────────────────────────────────────────────────
    public enum LaserState { On, Off, Blinking }

    [Header("Comportement")]
    [SerializeField] private LaserState state = LaserState.On;
    [SerializeField] private float onDuration = 2f;
    [SerializeField] private float offDuration = 1f;
    [SerializeField] private float blinkInterval = 0.15f;

    // ─── Privés ───────────────────────────────────────────────
    private LineRenderer _line;
    private EdgeCollider2D _collider;
    private float _timer;
    private bool _beamVisible = true;

    // ──────────────────────────────────────────────────────────
    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _collider = GetComponent<EdgeCollider2D>();

        _line.positionCount = 2;
        _collider.isTrigger = true;
    }

    void Start()
    {
        // Lance le cycle automatique si Blinking
        if (state == LaserState.Blinking)
            StartCoroutine(BlinkLoop());
        else if (state == LaserState.On)
            SetBeamActive(true);
        else
            SetBeamActive(false);
    }

    void Update()
    {
        if (_beamVisible)
            UpdatePositions();
    }

    // ─── Mise à jour visuel + collider ────────────────────────
    void UpdatePositions()
    {
        Vector3 start = origin.position;
        Vector3 end = destination.position;

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);

        _collider.SetPoints(new List<Vector2>
        {
            new Vector2(start.x, start.y),
            new Vector2(end.x,   end.y)
        });
    }

    // ─── Active / désactive visuellement + hitbox ─────────────
    void SetBeamActive(bool active)
    {
        _beamVisible = active;
        _line.enabled = active;
        _collider.enabled = active;
    }

    // ─── Coroutine de clignotement ────────────────────────────
    IEnumerator BlinkLoop()
    {
        while (true)
        {
            SetBeamActive(true);
            yield return new WaitForSeconds(onDuration);

            SetBeamActive(false);
            yield return new WaitForSeconds(offDuration);
        }
    }

    // ─── API publique (appelable depuis d'autres scripts) ─────
    public void TurnOn()
    {
        StopAllCoroutines();
        state = LaserState.On;
        SetBeamActive(true);
    }

    public void TurnOff()
    {
        StopAllCoroutines();
        state = LaserState.Off;
        SetBeamActive(false);
    }

    public void StartBlinking()
    {
        StopAllCoroutines();
        state = LaserState.Blinking;
        StartCoroutine(BlinkLoop());
    }
}