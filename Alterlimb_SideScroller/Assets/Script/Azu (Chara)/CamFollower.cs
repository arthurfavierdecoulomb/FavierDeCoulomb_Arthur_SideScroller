using System.Collections.Generic;
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

    [Header("Diagnostic")]
    [SerializeField] bool logHolders = false;

    public bool IsSuspended => holders.Count > 0;
    public float DefaultZoom => defaultZoom;
    public Transform Target => target;

    readonly HashSet<Object> holders = new HashSet<Object>();

    float currentLookAhead;
    float targetLookAhead;
    float lastTargetX;

    Camera cam;
    float defaultZoom;
    float targetZoom;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultZoom = cam.orthographicSize;
        targetZoom = defaultZoom;
    }

    void LateUpdate()
    {
        if (IsSuspended) return;
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

        transform.position = ClampToBounds(smoothed);

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom,
                                           zoomSpeed * Time.deltaTime);
    }

    public void Suspend()
    {
        Suspend(this);
    }

    public void Resume()
    {
        Resume(this);
    }

    public void Suspend(Object holder)
    {
        if (holder == null)
            holder = this;

        if (!holders.Add(holder))
            return;

        if (logHolders)
            Debug.Log($"[CameraFollow] Suivi suspendu par '{holder.name}' ({holders.Count} en cours).", this);
    }

    public void Resume(Object holder)
    {
        if (holder == null)
            holder = this;

        if (!holders.Remove(holder))
            return;

        if (logHolders)
            Debug.Log($"[CameraFollow] '{holder.name}' rend la caméra ({holders.Count} restant).", this);

        if (holders.Count > 0)
            return;

        currentLookAhead = 0f;
        targetLookAhead = 0f;

        if (target != null)
            lastTargetX = target.position.x;
    }

    public Vector3 DesiredPosition()
    {
        if (target == null)
            return transform.position;

        Vector3 desired = target.position + offset;
        desired.z = offset.z;

        return ClampToBounds(desired);
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        transform.position = DesiredPosition();
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

    Vector3 ClampToBounds(Vector3 position)
    {
        if (!useBounds)
            return position;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }
}