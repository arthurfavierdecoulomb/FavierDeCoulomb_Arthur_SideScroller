using System.Collections;
using UnityEngine;

public class StomperScreen : MonoBehaviour
{
    public enum ScreenState
    {
        Off,
        DirectionDown,
        DirectionUp,
        DirectionLeft,
        DirectionRight,
        Warning,
        Stomp
    }

    [Header("Bloc DIR")]
    [SerializeField] private GameObject dirRoot;
    [SerializeField] private GameObject arrowDown;
    [SerializeField] private GameObject arrowUp;
    [SerializeField] private GameObject arrowLeft;
    [SerializeField] private GameObject arrowRight;

    [Header("Bloc ATTENTION")]
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private float warningBlinkInterval = 0.18f;
    [SerializeField] private float warningBlinkMinInterval = 0.05f;
    [SerializeField] private float warningBlinkAcceleration = 0.82f;

    [Header("Bloc STOMP")]
    [SerializeField] private GameObject stompRoot;
    [SerializeField] private float stompFlickerInterval = 0.04f;

    public ScreenState State { get; private set; }

    private Coroutine blinkRoutine;

    private void Awake()
    {
        SetState(ScreenState.Off);
    }

    public void SetState(ScreenState state)
    {
        StopBlink();
        State = state;

        bool showDir = state == ScreenState.DirectionDown
            || state == ScreenState.DirectionUp
            || state == ScreenState.DirectionLeft
            || state == ScreenState.DirectionRight;

        SetActive(dirRoot, showDir);
        SetActive(warningRoot, state == ScreenState.Warning);
        SetActive(stompRoot, state == ScreenState.Stomp);

        SetActive(arrowDown, state == ScreenState.DirectionDown);
        SetActive(arrowUp, state == ScreenState.DirectionUp);
        SetActive(arrowLeft, state == ScreenState.DirectionLeft);
        SetActive(arrowRight, state == ScreenState.DirectionRight);

        if (state == ScreenState.DirectionLeft && arrowLeft == null)
            SetActive(arrowDown, true);

        if (state == ScreenState.DirectionRight && arrowRight == null)
            SetActive(arrowDown, true);

        if (state == ScreenState.Warning && warningRoot != null)
            blinkRoutine = StartCoroutine(WarningBlinkRoutine());

        if (state == ScreenState.Stomp && stompRoot != null)
            blinkRoutine = StartCoroutine(StompFlickerRoutine());
    }

    public void ShowHorizontalDirection(float deltaX)
    {
        SetState(deltaX < 0f ? ScreenState.DirectionLeft : ScreenState.DirectionRight);
    }

    private IEnumerator WarningBlinkRoutine()
    {
        float interval = warningBlinkInterval;
        bool visible = true;

        while (true)
        {
            warningRoot.SetActive(visible);
            visible = !visible;

            yield return new WaitForSeconds(interval);
            interval = Mathf.Max(warningBlinkMinInterval, interval * warningBlinkAcceleration);
        }
    }

    private IEnumerator StompFlickerRoutine()
    {
        bool visible = true;

        for (int i = 0; i < 6; i++)
        {
            stompRoot.SetActive(visible);
            visible = !visible;
            yield return new WaitForSeconds(stompFlickerInterval);
        }

        stompRoot.SetActive(true);
        blinkRoutine = null;
    }

    private void StopBlink()
    {
        if (blinkRoutine == null)
            return;

        StopCoroutine(blinkRoutine);
        blinkRoutine = null;
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}