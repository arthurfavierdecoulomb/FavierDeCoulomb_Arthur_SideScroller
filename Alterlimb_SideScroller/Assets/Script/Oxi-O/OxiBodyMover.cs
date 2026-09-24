using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OxiBodyMover : MonoBehaviour
{
    [System.Serializable]
    public class DescentLevel
    {
        public string label = "Pression";
        public float depth = 3f;
        public float duration = 1.1f;
        public float bounceAmplitude = 0.45f;
        public int bounceCount = 3;
        [Range(0.05f, 0.95f)]
        public float bounceDamping = 0.4f;
        public bool useCustomTint = false;
        public Color customTint = new Color(0.55f, 0.55f, 0.65f, 1f);
    }

    [Header("Corps à déplacer")]
    [SerializeField] private Transform body;

    [Header("Paliers de descente")]
    [SerializeField] private List<DescentLevel> levels = new List<DescentLevel>();

    [Header("Remontée")]
    [SerializeField] private float riseDuration = 1.4f;
    [SerializeField] private float riseBounceAmplitude = 0.2f;
    [SerializeField] private int riseBounceCount = 2;
    [Range(0.05f, 0.95f)]
    [SerializeField] private float riseBounceDamping = 0.55f;

    [Header("Chute finale")]
    [SerializeField] private float fallDepth = 15f;
    [SerializeField] private float fallDuration = 1.2f;
    [SerializeField] private float fallTiltAngle = 6f;
    [SerializeField] private bool shakeOnLanding = true;
    [SerializeField] private float landingShakeDuration = 0.6f;
    [SerializeField] private float landingShakeMagnitude = 0.7f;
    [SerializeField] private bool hideAfterFall = false;

    [Header("Assombrissement quand il descend")]
    [SerializeField] private bool tintWhenLowered = true;
    [SerializeField] private Color loweredTint = new Color(0.55f, 0.55f, 0.65f, 1f);
    [SerializeField] private bool tintAffectsAlpha = false;
    [SerializeField] private List<SpriteRenderer> excludedRenderers = new List<SpriteRenderer>();
    [SerializeField] private bool includeInactiveRenderers = true;

    [Header("Balancement")]
    [SerializeField] private bool swayWhenLowered = true;
    [SerializeField] private float swayAmplitude = 0.12f;
    [SerializeField] private float swaySpeed = 0.9f;
    [SerializeField] private float swayTilt = 1.4f;
    [SerializeField] private float swayFadeDuration = 0.6f;

    [Header("Impact")]
    [SerializeField] private bool shakeOnArrival = true;
    [SerializeField] private float arrivalShakeDuration = 0.35f;
    [SerializeField] private float arrivalShakeMagnitude = 0.25f;

    [Header("Événements")]
    public UnityEvent onDescendStart;
    public UnityEvent onDescendEnd;
    public UnityEvent onRiseStart;
    public UnityEvent onRiseEnd;
    public UnityEvent onFallStart;
    public UnityEvent onFallEnd;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    public bool IsMoving { get; private set; }
    public bool IsLowered { get; private set; }
    public bool IsFalling { get; private set; }
    public bool HasFallen { get; private set; }
    public int CurrentLevel { get; private set; } = -1;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 anchorPosition;
    private Coroutine motionRoutine;
    private float swayTime;
    private float swayWeight;

    private readonly List<SpriteRenderer> tintedRenderers = new List<SpriteRenderer>();
    private readonly List<Color> baseColors = new List<Color>();
    private float tintWeight;
    private Color activeTint;

    private void Awake()
    {
        if (body == null)
            body = transform;

        restPosition = body.position;
        restRotation = body.rotation;
        anchorPosition = restPosition;

        activeTint = loweredTint;

        RefreshRenderers();
        LogSetup();
    }

    public void RefreshRenderers()
    {
        tintedRenderers.Clear();
        baseColors.Clear();

        if (!tintWhenLowered || body == null)
            return;

        SpriteRenderer[] found = body.GetComponentsInChildren<SpriteRenderer>(includeInactiveRenderers);

        foreach (SpriteRenderer renderer in found)
        {
            if (renderer == null || excludedRenderers.Contains(renderer))
                continue;

            tintedRenderers.Add(renderer);
            baseColors.Add(renderer.color);
        }
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (levels.Count == 0)
            Debug.LogError($"[OxiOBodyMotion] '{name}' : aucun palier configuré, Oxi-O ne descendra jamais.", this);

        foreach (DescentLevel level in levels)
            if (level != null && level.depth <= 0f)
                Debug.LogWarning($"[OxiOBodyMotion] '{name}' : le palier '{level.label}' a une profondeur de {level.depth}. Une valeur positive fait descendre.", this);

        if (fallDepth <= 0f)
            Debug.LogWarning($"[OxiOBodyMotion] '{name}' : Fall Depth vaut {fallDepth}. Une valeur positive fait tomber Oxi-O.", this);

        if (body == transform && transform.childCount == 0)
            Debug.LogWarning($"[OxiOBodyMotion] '{name}' : le champ Body est vide et cet objet n'a pas d'enfants. Assigne le parent qui contient Oxi-O ET ses tuyaux.", this);

        if (tintWhenLowered && tintedRenderers.Count == 0)
            Debug.LogWarning($"[OxiOBodyMotion] '{name}' : aucun SpriteRenderer trouvé sous Body, l'assombrissement ne se verra pas.", this);
        else if (tintWhenLowered)
            Debug.Log($"[OxiOBodyMotion] '{name}' : {tintedRenderers.Count} SpriteRenderer(s) seront assombris pendant la descente.", this);
    }

    private void LateUpdate()
    {
        if (!swayWhenLowered || IsFalling || HasFallen)
            return;

        float target = IsLowered && !IsMoving ? 1f : 0f;
        float step = swayFadeDuration <= 0f ? 1f : Time.deltaTime / swayFadeDuration;
        swayWeight = Mathf.MoveTowards(swayWeight, target, step);

        if (swayWeight <= 0.001f)
            return;

        swayTime += Time.deltaTime * swaySpeed;

        float offset = Mathf.Sin(swayTime) * swayAmplitude * swayWeight;
        float tilt = Mathf.Cos(swayTime) * swayTilt * swayWeight;

        body.position = anchorPosition + Vector3.right * offset;
        body.rotation = restRotation * Quaternion.Euler(0f, 0f, tilt);
    }

    private void ApplyTint(float weight)
    {
        tintWeight = Mathf.Clamp01(weight);

        if (!tintWhenLowered || tintedRenderers.Count == 0)
            return;

        for (int i = 0; i < tintedRenderers.Count; i++)
        {
            SpriteRenderer renderer = tintedRenderers[i];

            if (renderer == null)
                continue;

            Color baseColor = baseColors[i];

            Color darkened = new Color(
                baseColor.r * activeTint.r,
                baseColor.g * activeTint.g,
                baseColor.b * activeTint.b,
                tintAffectsAlpha ? baseColor.a * activeTint.a : baseColor.a);

            renderer.color = Color.Lerp(baseColor, darkened, tintWeight);
        }
    }

    public void DescendTo(int levelIndex)
    {
        if (levels.Count == 0 || IsFalling || HasFallen)
            return;

        int index = Mathf.Clamp(levelIndex, 0, levels.Count - 1);

        if (IsLowered && CurrentLevel == index && !IsMoving)
            return;

        if (motionRoutine != null)
            StopCoroutine(motionRoutine);

        motionRoutine = StartCoroutine(DescendRoutine(index));
    }

    public void Descend()
    {
        DescendTo(0);
    }

    public void DescendDeep()
    {
        DescendTo(levels.Count - 1);
    }

    public void RiseToRest()
    {
        if (IsFalling || HasFallen)
            return;

        if (!IsLowered && !IsMoving)
            return;

        if (motionRoutine != null)
            StopCoroutine(motionRoutine);

        motionRoutine = StartCoroutine(RiseRoutine());
    }

    public void FallAway()
    {
        if (IsFalling || HasFallen)
            return;

        if (motionRoutine != null)
            StopCoroutine(motionRoutine);

        motionRoutine = StartCoroutine(FallRoutine());
    }

    public void SnapToRest()
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }

        swayWeight = 0f;
        IsMoving = false;
        IsLowered = false;
        IsFalling = false;
        HasFallen = false;
        CurrentLevel = -1;

        anchorPosition = restPosition;
        body.position = restPosition;
        body.rotation = restRotation;

        SetRenderersVisible(true);
        ApplyTint(0f);
    }

    private Vector3 FallTarget()
    {
        return restPosition + Vector3.down * fallDepth;
    }

    private IEnumerator FallRoutine()
    {
        IsFalling = true;
        IsMoving = true;
        IsLowered = false;
        swayWeight = 0f;
        onFallStart?.Invoke();

        if (logDiagnostics)
            Debug.Log($"[OxiOBodyMotion] Oxi-O tombe de {fallDepth} unités en {fallDuration}s.", this);

        Vector3 start = body.position;
        Vector3 target = new Vector3(start.x, FallTarget().y, start.z);
        Quaternion startRotation = body.rotation;
        Quaternion endRotation = restRotation * Quaternion.Euler(0f, 0f, fallTiltAngle);

        float duration = Mathf.Max(0.05f, fallDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float gravity = t * t;

            body.position = Vector3.LerpUnclamped(start, target, gravity);
            body.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        body.position = target;
        body.rotation = endRotation;
        anchorPosition = target;

        IsFalling = false;
        IsMoving = false;
        HasFallen = true;
        motionRoutine = null;

        if (shakeOnLanding && CameraShake.Instance != null)
            CameraShake.Instance.Shake(landingShakeDuration, landingShakeMagnitude);

        if (hideAfterFall)
            SetRenderersVisible(false);

        if (logDiagnostics)
            Debug.Log("[OxiOBodyMotion] Oxi-O a touché le sol.", this);

        onFallEnd?.Invoke();
    }

    private void SetRenderersVisible(bool visible)
    {
        if (body == null)
            return;

        foreach (SpriteRenderer renderer in body.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer != null)
                renderer.enabled = visible;
    }

    private IEnumerator DescendRoutine(int index)
    {
        DescentLevel level = levels[index];

        IsMoving = true;
        swayWeight = 0f;
        onDescendStart?.Invoke();

        activeTint = level.useCustomTint ? level.customTint : loweredTint;

        Vector3 start = body.position;
        Vector3 target = restPosition + Vector3.down * level.depth;

        float startTint = tintWeight;
        float duration = Mathf.Max(0.05f, level.duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased01 = EaseOutCubic(t);
            Vector3 eased = Vector3.Lerp(start, target, eased01);

            float damping = Mathf.Pow(1f - t, 1f - level.bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, level.bounceCount));

            body.position = eased + Vector3.down * (oscillation * damping * level.bounceAmplitude);
            body.rotation = restRotation * Quaternion.Euler(0f, 0f, oscillation * damping * swayTilt * 2f);

            ApplyTint(Mathf.Lerp(startTint, 1f, eased01));

            elapsed += Time.deltaTime;
            yield return null;
        }

        anchorPosition = target;
        body.position = target;
        body.rotation = restRotation;

        ApplyTint(1f);

        swayTime = 0f;
        IsLowered = true;
        CurrentLevel = index;
        IsMoving = false;
        motionRoutine = null;

        if (shakeOnArrival && CameraShake.Instance != null)
            CameraShake.Instance.Shake(arrivalShakeDuration, arrivalShakeMagnitude);

        if (logDiagnostics)
            Debug.Log($"[OxiOBodyMotion] Oxi-O descend au palier '{level.label}' ({level.depth} unités).", this);

        onDescendEnd?.Invoke();
    }

    private IEnumerator RiseRoutine()
    {
        IsMoving = true;
        swayWeight = 0f;
        onRiseStart?.Invoke();

        Vector3 start = body.position;
        float startTint = tintWeight;
        float duration = Mathf.Max(0.05f, riseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased01 = EaseOutCubic(t);
            Vector3 eased = Vector3.Lerp(start, restPosition, eased01);

            float damping = Mathf.Pow(1f - t, 1f - riseBounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, riseBounceCount));

            body.position = eased + Vector3.up * (oscillation * damping * riseBounceAmplitude);
            body.rotation = restRotation * Quaternion.Euler(0f, 0f, oscillation * damping * swayTilt);

            ApplyTint(Mathf.Lerp(startTint, 0f, eased01));

            elapsed += Time.deltaTime;
            yield return null;
        }

        anchorPosition = restPosition;
        body.position = restPosition;
        body.rotation = restRotation;

        ApplyTint(0f);

        IsLowered = false;
        CurrentLevel = -1;
        IsMoving = false;
        motionRoutine = null;

        if (logDiagnostics)
            Debug.Log("[OxiOBodyMotion] Oxi-O remonte à sa position d'origine.", this);

        onRiseEnd?.Invoke();
    }

    private float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = body != null ? body : transform;
        Vector3 origin = Application.isPlaying ? restPosition : target.position;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);

        foreach (DescentLevel level in levels)
        {
            if (level == null)
                continue;

            Vector3 point = origin + Vector3.down * level.depth;
            Gizmos.DrawLine(origin, point);
            Gizmos.DrawWireCube(point, new Vector3(2f, 0.3f, 0f));
        }

        Vector3 landing = origin + Vector3.down * fallDepth;

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.95f);
        Gizmos.DrawLine(origin, landing);
        Gizmos.DrawWireCube(landing, new Vector3(6f, 0.5f, 0f));
        Gizmos.DrawWireSphere(landing, 0.6f);
    }
}