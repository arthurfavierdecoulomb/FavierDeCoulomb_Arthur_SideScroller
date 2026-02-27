using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Smoothing")]
    [SerializeField] float smoothSpeedX = 6f;
    [SerializeField] float smoothSpeedY = 4f;

    [Header("Offset")]
    [SerializeField] Vector3 offset = new Vector3(0f, 1.5f, -10f);

    [Header("Look Ahead (anticipation)")]
    [SerializeField] float lookAheadDistance = 2f;
    [SerializeField] float lookAheadSpeed = 4f;

    [Header("Dead Zone (zone sans mouvement cam)")]
    [SerializeField] float deadZoneX = 0.5f;
    [SerializeField] float deadZoneY = 0.8f;

    [Header("Camera Bounds (optionnel)")]
    [SerializeField] bool useBounds = false;
    [SerializeField] float minX, maxX, minY, maxY;

    float currentLookAhead;
    float targetLookAhead;
    float lastTargetX;
    Vector3 currentVelocity; // pour SmoothDamp

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position + offset;

        // Look ahead : anticipe la direction du perso
        float moveDirectionX = target.position.x - lastTargetX;
        if (Mathf.Abs(moveDirectionX) > 0.01f)
            targetLookAhead = Mathf.Sign(moveDirectionX) * lookAheadDistance;

        currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead, lookAheadSpeed * Time.deltaTime);
        lastTargetX = target.position.x;

        targetPos.x += currentLookAhead;

        // Dead zone : la cam ne bouge que si le perso sort de la zone
        float diffX = targetPos.x - transform.position.x;
        float diffY = targetPos.y - transform.position.y;

        if (Mathf.Abs(diffX) < deadZoneX) targetPos.x = transform.position.x;
        if (Mathf.Abs(diffY) < deadZoneY) targetPos.y = transform.position.y;

        // Smooth
        Vector3 smoothed = new Vector3
        (
            Mathf.Lerp(transform.position.x, targetPos.x, smoothSpeedX * Time.deltaTime),
            Mathf.Lerp(transform.position.y, targetPos.y, smoothSpeedY * Time.deltaTime),
            offset.z
        );

        // Clamp dans les bounds si activé
        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minX, maxX);
            smoothed.y = Mathf.Clamp(smoothed.y, minY, maxY);
        }

        transform.position = smoothed;
    }
}