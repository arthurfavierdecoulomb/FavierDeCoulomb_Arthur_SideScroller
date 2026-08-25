using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum TelevisionIdleState { Auto, Off, NoSignal, Error }
public enum TelevisionReaction { Auto, Ignore, Watch, Possess }

public class WallTelevision : MonoBehaviour
{
    [Header("Écran")]
    [SerializeField] SpriteRenderer screenRenderer;
    [SerializeField] Light2D screenLight;

    [Header("Sprites d'état")]
    [SerializeField] Sprite offSprite;
    [SerializeField] Sprite noSignalSprite;
    [SerializeField] Sprite errorSprite;

    [Header("Oxi-O")]
    [SerializeField] Sprite[] oxioFrames;

    [Header("Parasites")]
    [SerializeField] Sprite[] interferenceSprites;

    [Header("État au repos")]
    [SerializeField] TelevisionIdleState idleState = TelevisionIdleState.Auto;

    [Header("Réaction à l'approche")]
    [SerializeField] TelevisionReaction reaction = TelevisionReaction.Auto;
    [SerializeField, Range(0f, 1f)] float watchChance = 0.45f;
    [SerializeField, Range(0f, 1f)] float possessChance = 0.12f;

    [Header("Détection")]
    [SerializeField] float detectionRadius = 6f;
    [SerializeField] LayerMask playerMask;
    [SerializeField] float detectionInterval = 0.1f;

    [Header("Suivi du regard")]
    [SerializeField] float frameInterval = 0.12f;
    [SerializeField] bool trackWhilePossessed = true;

    [Header("Possession — connexion")]
    [SerializeField] float connectionDuration = 0.7f;
    [SerializeField] Vector2 connectionFlickerRange = new Vector2(0.03f, 0.08f);

    [Header("Possession — présence")]
    [SerializeField] Vector2 faceVisibleRange = new Vector2(0.6f, 2.6f);
    [SerializeField] Vector2 faceHiddenRange = new Vector2(0.04f, 0.18f);

    [Header("Lumière")]
    [SerializeField] Color idleLightColor = new Color(0.85f, 0.8f, 0.6f);
    [SerializeField] Color possessedLightColor = new Color(1f, 0.92f, 0.25f);
    [SerializeField] float idleLightIntensity = 0.35f;
    [SerializeField] float possessedLightIntensity = 0.9f;
    [SerializeField] float interferenceLightIntensity = 1.8f;

    [Header("Son")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip possessionClip;

    TelevisionIdleState resolvedIdle;
    TelevisionReaction resolvedReaction;
    Transform player;
    Sprite idleSprite;

    float detectionTimer;
    float frameTimer;
    float possessTimer;
    float connectionTimer;
    int currentFrame = -1;
    bool possessed;
    bool connecting;
    bool faceVisible;

    public bool IsPossessed => possessed;

    void Awake()
    {
        if (screenRenderer == null) screenRenderer = GetComponentInChildren<SpriteRenderer>();

        float idleRoll = HashFromPosition(0.41f);
        float reactionRoll = HashFromPosition(5.77f);

        resolvedIdle = idleState;
        if (idleState == TelevisionIdleState.Auto)
        {
            if (idleRoll < 0.34f) resolvedIdle = TelevisionIdleState.Off;
            else if (idleRoll < 0.67f) resolvedIdle = TelevisionIdleState.NoSignal;
            else resolvedIdle = TelevisionIdleState.Error;
        }

        resolvedReaction = reaction;
        if (reaction == TelevisionReaction.Auto)
        {
            if (reactionRoll < possessChance) resolvedReaction = TelevisionReaction.Possess;
            else if (reactionRoll < possessChance + watchChance) resolvedReaction = TelevisionReaction.Watch;
            else resolvedReaction = TelevisionReaction.Ignore;
        }

        idleSprite = IdleSprite();
        ShowIdle();
    }

    void Update()
    {
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = detectionInterval;
            DetectPlayer();
        }

        if (possessed)
        {
            UpdatePossessed();
            return;
        }

        if (player == null)
        {
            if (currentFrame != -1) ShowIdle();
            return;
        }

        if (resolvedReaction == TelevisionReaction.Possess)
        {
            Possess();
            return;
        }

        if (resolvedReaction == TelevisionReaction.Watch)
            UpdateWatching();
    }

    public void Possess()
    {
        if (possessed) return;

        possessed = true;
        connecting = true;
        faceVisible = false;
        connectionTimer = connectionDuration;
        possessTimer = 0f;

        if (audioSource != null && possessionClip != null)
            audioSource.PlayOneShot(possessionClip);
    }

    void DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerMask);
        player = hit != null ? hit.transform : null;
    }

    void UpdateWatching()
    {
        frameTimer -= Time.deltaTime;
        if (frameTimer > 0f) return;
        frameTimer = frameInterval;

        ShowFace(idleLightIntensity);
    }

    void UpdatePossessed()
    {
        possessTimer -= Time.deltaTime;

        if (connecting)
        {
            connectionTimer -= Time.deltaTime;

            if (possessTimer <= 0f)
            {
                possessTimer = Random.Range(connectionFlickerRange.x, connectionFlickerRange.y);
                if (Random.value < 0.4f) ShowFace(possessedLightIntensity);
                else ShowInterference();
            }

            if (connectionTimer <= 0f)
            {
                connecting = false;
                faceVisible = true;
                possessTimer = Random.Range(faceVisibleRange.x, faceVisibleRange.y);
                ShowFace(possessedLightIntensity);
            }
            return;
        }

        if (possessTimer <= 0f)
        {
            faceVisible = !faceVisible;
            possessTimer = faceVisible
                ? Random.Range(faceVisibleRange.x, faceVisibleRange.y)
                : Random.Range(faceHiddenRange.x, faceHiddenRange.y);

            if (faceVisible) ShowFace(possessedLightIntensity);
            else ShowInterference();
            return;
        }

        if (!faceVisible || !trackWhilePossessed) return;

        frameTimer -= Time.deltaTime;
        if (frameTimer > 0f) return;
        frameTimer = frameInterval;

        ShowFace(possessedLightIntensity);
    }

    void ShowFace(float lightIntensity)
    {
        if (oxioFrames == null || oxioFrames.Length == 0) return;

        int index = oxioFrames.Length / 2;

        if (player != null)
        {
            float offset = player.position.x - transform.position.x;
            float t = Mathf.InverseLerp(-detectionRadius, detectionRadius, offset);
            index = Mathf.Clamp(Mathf.RoundToInt(t * (oxioFrames.Length - 1)), 0, oxioFrames.Length - 1);
        }

        currentFrame = index;
        screenRenderer.sprite = oxioFrames[index];
        screenRenderer.color = Color.white;
        ApplyLight(possessed ? possessedLightColor : idleLightColor, lightIntensity);
    }

    void ShowInterference()
    {
        currentFrame = -1;

        Sprite sprite = null;
        if (interferenceSprites != null && interferenceSprites.Length > 0)
            sprite = interferenceSprites[Random.Range(0, interferenceSprites.Length)];

        screenRenderer.sprite = sprite != null ? sprite : noSignalSprite;
        screenRenderer.color = Color.white;
        ApplyLight(possessedLightColor, interferenceLightIntensity);
    }

    void ShowIdle()
    {
        currentFrame = -1;
        screenRenderer.sprite = idleSprite;
        screenRenderer.color = Color.white;

        if (resolvedIdle == TelevisionIdleState.Off) ApplyLight(idleLightColor, 0f);
        else ApplyLight(idleLightColor, idleLightIntensity);
    }

    Sprite IdleSprite()
    {
        switch (resolvedIdle)
        {
            case TelevisionIdleState.NoSignal: return noSignalSprite;
            case TelevisionIdleState.Error: return errorSprite;
            default: return offSprite;
        }
    }

    void ApplyLight(Color color, float intensity)
    {
        if (screenLight == null) return;

        screenLight.color = color;
        screenLight.intensity = intensity;
    }

    float HashFromPosition(float offset)
    {
        Vector3 p = transform.position;
        float value = Mathf.Abs(Mathf.Sin((p.x + offset) * 12.9898f + (p.y - offset) * 78.233f) * 43758.5453f);
        return value - Mathf.Floor(value);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}