using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Smoothing")]
    [SerializeField] float smoothSpeedX = 6f;
    [SerializeField] float smoothSpeedY = 4f;

    [Header("Offset")]
    [SerializeField] Vector3 offset = new Vector3(0f, 1.5f, -10f);

    [Header("Look Ahead")]
    [SerializeField] float lookAheadDistance = 2f;
    [SerializeField] float lookAheadSpeed = 4f;

    [Header("Dead Zone")]
    [SerializeField] float deadZoneX = 0.5f;
    [SerializeField] float deadZoneY = 0.8f;

    [Header("Camera Bounds")]
    [SerializeField] bool useBounds = false;
    [SerializeField] float minX, maxX, minY, maxY;

    [Header("Zoom")]
    [SerializeField] float zoomSpeed = 3f;

    public bool IsSuspended => suspended;
    public float DefaultZoom => defaultZoom;
    public Transform Target => target;

    float currentLookAhead;
    float targetLookAhead;
    float lastTargetX;

    Camera cam;
    float defaultZoom;
    float targetZoom;
    bool suspended;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultZoom = cam.orthographicSize;
        targetZoom = defaultZoom;
    }

    void LateUpdate()
    {
        if (suspended) return;
        if (target == null) return;

        Vector3 targetPos = target.position + offset;

        float moveDirectionX = target.position.x - lastTargetX;
        if (Mathf.Abs(moveDirectionX) > 0.01f)
            targetLookAhead = Mathf.Sign(moveDirectionX) * lookAheadDistance;

        currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead,
                                       lookAheadSpeed * Time.deltaTime);
        lastTargetX = target.position.x;
        targetPos.x += currentLookAhead;

        float diffX = targetPos.x - transform.position.x;
        float diffY = targetPos.y - transform.position.y;

        if (Mathf.Abs(diffX) < deadZoneX) targetPos.x = transform.position.x;
        if (Mathf.Abs(diffY) < deadZoneY) targetPos.y = transform.position.y;

        Vector3 smoothed = new Vector3
        (
            Mathf.Lerp(transform.position.x, targetPos.x, smoothSpeedX * Time.deltaTime),
            Mathf.Lerp(transform.position.y, targetPos.y, smoothSpeedY * Time.deltaTime),
            offset.z
        );

        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minX, maxX);
            smoothed.y = Mathf.Clamp(smoothed.y, minY, maxY);
        }

        transform.position = smoothed;

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom,
                                           zoomSpeed * Time.deltaTime);
    }

    public void Suspend()
    {
        suspended = true;
    }

    public void Resume()
    {
        suspended = false;
        currentLookAhead = 0f;
        targetLookAhead = 0f;

        if (target != null)
            lastTargetX = target.position.x;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        Vector3 snapped = target.position + offset;
        snapped.z = offset.z;

        if (useBounds)
        {
            snapped.x = Mathf.Clamp(snapped.x, minX, maxX);
            snapped.y = Mathf.Clamp(snapped.y, minY, maxY);
        }

        transform.position = snapped;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) lastTargetX = target.position.x;
    }

    public void SetTargetZoom(float newZoom)
    {
        targetZoom = newZoom;
    }

    public void ResetZoom()
    {
        targetZoom = defaultZoom;
    }
}