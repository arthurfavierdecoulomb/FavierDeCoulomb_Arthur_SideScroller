using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
public class CameraFocus : MonoBehaviour
{
    [System.Serializable]
    public class FocusPoint
    {
        public string id = "oxio";
        public Transform anchor;
        public Vector2 offset;
        public float zoomFactor = 1f;
        public float orthographicSize = 0f;
        public float blendDuration = 0.6f;
    }

    [Header("Caméra")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MonoBehaviour followScript;

    [Header("Points de focus")]
    [SerializeField] private List<FocusPoint> focusPoints = new List<FocusPoint>();

    [Header("Retour au joueur")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float returnBlendDuration = 0.5f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public bool IsFocused { get; private set; }
    public string CurrentFocusId { get; private set; }

    private Transform camTransform;
    private Coroutine blendRoutine;
    private FocusPoint activePoint;
    private float defaultOrthographicSize;
    private bool holdPosition;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogError($"[CameraFocus] '{name}' : aucune caméra trouvée.", this);
            enabled = false;
            return;
        }

        camTransform = targetCamera.transform;
        defaultOrthographicSize = targetCamera.orthographicSize;

        if (followScript == null)
            AutoResolveFollowScript();

        LogSetup();
    }

    private void AutoResolveFollowScript()
    {
        MonoBehaviour[] components = targetCamera.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            if (component == null || component == this)
                continue;

            string typeName = component.GetType().Name;

            if (typeName.Contains("Follow") || typeName.Contains("Suivi"))
            {
                followScript = component;

                if (logDiagnostics)
                    Debug.Log($"[CameraFocus] Script de suivi détecté automatiquement : {typeName}.", this);

                return;
            }
        }
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (followScript == null)
            Debug.LogError($"[CameraFocus] '{name}' : aucun script de suivi trouvé sur la caméra. Assigne-le à la main, sinon le focus luttera contre lui.", this);

        if (focusPoints.Count == 0)
            Debug.LogWarning($"[CameraFocus] '{name}' : aucun point de focus configuré.", this);

        foreach (FocusPoint point in focusPoints)
        {
            if (point == null)
                continue;

            if (point.anchor == null)
                Debug.LogError($"[CameraFocus] '{name}' : le point '{point.id}' n'a pas d'Anchor, il sera ignoré.", this);
        }
    }

    private void LateUpdate()
    {
        if (!holdPosition || activePoint == null || blendRoutine != null)
            return;

        camTransform.position = AnchorPosition(activePoint);

        if (targetCamera.orthographic && HasSizeChange(activePoint))
            targetCamera.orthographicSize = TargetSize(activePoint);
    }

    private bool HasSizeChange(FocusPoint point)
    {
        if (point.orthographicSize > 0f)
            return true;

        return point.zoomFactor > 0f && !Mathf.Approximately(point.zoomFactor, 1f);
    }

    private float TargetSize(FocusPoint point)
    {
        if (point.orthographicSize > 0f)
            return point.orthographicSize;

        float factor = point.zoomFactor > 0f ? point.zoomFactor : 1f;
        return defaultOrthographicSize * factor;
    }

    public void FocusOn(string id)
    {
        FocusPoint point = FindPoint(id);

        if (point == null)
        {
            Debug.LogError($"[CameraFocus] '{name}' : aucun point de focus nommé '{id}'.", this);
            return;
        }

        if (point.anchor == null)
            return;

        if (blendRoutine != null)
            StopCoroutine(blendRoutine);

        if (!IsFocused && targetCamera.orthographic)
            defaultOrthographicSize = targetCamera.orthographicSize;

        IsFocused = true;
        CurrentFocusId = point.id;
        activePoint = point;

        if (followScript != null)
            followScript.enabled = false;

        if (logDiagnostics && targetCamera.orthographic)
        {
            string target = HasSizeChange(point)
                ? TargetSize(point).ToString("0.##")
                : "inchangé";

            Debug.Log($"[CameraFocus] Focus '{point.id}' : Orthographic Size de jeu {defaultOrthographicSize:0.##}, cible {target}. Mets Zoom Factor à 2 pour voir deux fois plus large.", this);
        }

        blendRoutine = StartCoroutine(BlendToPointRoutine(point));
    }

    public void ReleaseFocus()
    {
        if (!IsFocused)
            return;

        if (blendRoutine != null)
            StopCoroutine(blendRoutine);

        blendRoutine = StartCoroutine(ReturnRoutine());
    }

    public void ReleaseFocusInstant()
    {
        if (blendRoutine != null)
        {
            StopCoroutine(blendRoutine);
            blendRoutine = null;
        }

        holdPosition = false;
        IsFocused = false;
        CurrentFocusId = null;
        activePoint = null;

        if (targetCamera.orthographic)
            targetCamera.orthographicSize = defaultOrthographicSize;

        if (followScript != null)
            followScript.enabled = true;
    }

    private IEnumerator BlendToPointRoutine(FocusPoint point)
    {
        holdPosition = false;

        Vector3 startPosition = camTransform.position;
        float startSize = targetCamera.orthographic ? targetCamera.orthographicSize : 0f;
        bool changeSize = targetCamera.orthographic && HasSizeChange(point);
        float endSize = changeSize ? TargetSize(point) : startSize;

        float duration = Mathf.Max(0.01f, point.blendDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            camTransform.position = Vector3.Lerp(startPosition, AnchorPosition(point), eased);

            if (changeSize)
                targetCamera.orthographicSize = Mathf.Lerp(startSize, endSize, eased);

            yield return null;
        }

        camTransform.position = AnchorPosition(point);

        if (changeSize)
            targetCamera.orthographicSize = endSize;

        holdPosition = true;
        blendRoutine = null;
    }

    private IEnumerator ReturnRoutine()
    {
        holdPosition = false;

        Transform target = ResolvePlayer();

        Vector3 startPosition = camTransform.position;
        float startSize = targetCamera.orthographic ? targetCamera.orthographicSize : 0f;
        bool changeSize = targetCamera.orthographic;

        float duration = Mathf.Max(0.01f, returnBlendDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            if (target != null)
                camTransform.position = Vector3.Lerp(startPosition, PlayerPosition(target), eased);

            if (changeSize)
                targetCamera.orthographicSize = Mathf.Lerp(startSize, defaultOrthographicSize, eased);

            yield return null;
        }

        if (changeSize)
            targetCamera.orthographicSize = defaultOrthographicSize;

        IsFocused = false;
        CurrentFocusId = null;
        activePoint = null;
        blendRoutine = null;

        if (followScript != null)
            followScript.enabled = true;
    }

    private Vector3 AnchorPosition(FocusPoint point)
    {
        Vector3 anchor = point.anchor.position;

        return new Vector3(
            anchor.x + point.offset.x,
            anchor.y + point.offset.y,
            camTransform.position.z);
    }

    private Vector3 PlayerPosition(Transform target)
    {
        return new Vector3(target.position.x, target.position.y, camTransform.position.z);
    }

    private Transform ResolvePlayer()
    {
        if (player != null)
            return player;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
            player = found.transform;

        return player;
    }

    private FocusPoint FindPoint(string id)
    {
        foreach (FocusPoint point in focusPoints)
            if (point != null && point.id == id)
                return point;

        return null;
    }
}