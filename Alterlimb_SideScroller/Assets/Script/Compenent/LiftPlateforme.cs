using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class LiftPlatform : MonoBehaviour
{
    [Header("Contrôles")]
    [SerializeField] KeyCode upKey = KeyCode.W;
    [SerializeField] KeyCode downKey = KeyCode.S;

    [Header("Mouvement")]
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float upLimit = 5f;
    [SerializeField] float downLimit = 0f;

    [Header("Bounce aux limites")]
    [SerializeField] float bounceAmplitude = 0.3f;
    [SerializeField] float bounceDuration = 0.5f;
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.4f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] float maxStandingAngle = 45f;
    [SerializeField] float contactLostThreshold = 0.1f;

    [Header("Animation")]
    [SerializeField] Animator animator;

    [Header("Audio")]
    [SerializeField] PlatformAudio platformAudio;

    const float LIMIT_EPSILON = 0.01f;
    static readonly int MoveDirHash = Animator.StringToHash("moveDir");

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    Vector2 startPosition;
    Vector2 platformVelocity;
    bool playerOnPlatform;
    float timeSinceLastContact;
    int currentMoveDir;

    bool isBouncing;
    Coroutine bounceCoroutine;

    int previousMoveDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null) animator = GetComponent<Animator>();
        if (platformAudio == null) platformAudio = GetComponent<PlatformAudio>();

        startPosition = rb.position;
        SetMoveDir(0);
    }

    void OnEnable()
    {
        SpawnManager.OnPlayerRespawn += ResetToStartPosition;
    }

    void OnDisable()
    {
        SpawnManager.OnPlayerRespawn -= ResetToStartPosition;
    }

    void ResetToStartPosition()
    {
        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }
        isBouncing = false;

        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        platformVelocity = Vector2.zero;

        previousMoveDir = 0;

        DetachPlayer();
        SetMoveDir(0);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            float angle = Vector2.Angle(contact.normal, Vector2.down);
            if (angle <= maxStandingAngle)
            {
                if (!playerOnPlatform)
                {
                    playerRb = collision.collider.attachedRigidbody;
                    playerOnPlatform = true;
                }
                timeSinceLastContact = 0f;
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;
        DetachPlayer();
    }

    void Update()
    {
        if (!playerOnPlatform) return;

        timeSinceLastContact += Time.deltaTime;
        if (timeSinceLastContact > contactLostThreshold)
            DetachPlayer();
    }

    void DetachPlayer()
    {
        playerOnPlatform = false;
        playerRb = null;
    }

    void FixedUpdate()
    {
        if (isBouncing) return;

        int newMoveDir = 0;
        platformVelocity = Vector2.zero;

        if (playerOnPlatform)
        {
            if (Input.GetKey(upKey)) newMoveDir += 1;
            if (Input.GetKey(downKey)) newMoveDir -= 1;

            if (newMoveDir != 0)
            {
                Vector2 currentPos = rb.position;
                float minY = startPosition.y - downLimit;
                float maxY = startPosition.y + upLimit;

                bool blockedUp = newMoveDir > 0 && currentPos.y >= maxY - LIMIT_EPSILON;
                bool blockedDown = newMoveDir < 0 && currentPos.y <= minY + LIMIT_EPSILON;

                if ((blockedUp && previousMoveDir > 0) || (blockedDown && previousMoveDir < 0))
                {
                    float snappedY = blockedUp ? maxY : minY;
                    rb.position = new Vector2(startPosition.x, snappedY);

                    float bounceDirection = blockedUp ? 1f : -1f;
                    StartBounce(bounceDirection);

                    previousMoveDir = 0;
                    SetMoveDir(0);
                    return;
                }

                if (blockedUp || blockedDown)
                {
                    newMoveDir = 0;
                }
                else
                {
                    platformVelocity = new Vector2(0f, newMoveDir * moveSpeed);
                }
            }
        }

        rb.linearVelocity = platformVelocity;

        if (playerOnPlatform && playerRb != null && platformVelocity.y < 0f)
        {
            Vector2 v = playerRb.linearVelocity;
            if (v.y <= 0f)
            {
                v.y = platformVelocity.y;
                playerRb.linearVelocity = v;
            }
        }

        SetMoveDir(newMoveDir);
        previousMoveDir = newMoveDir;
    }

    void StartBounce(float direction)
    {
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(BounceRoutine(direction));
    }

    IEnumerator BounceRoutine(float direction)
    {
        isBouncing = true;

        float baseY = rb.position.y;
        float previousY = baseY;
        float elapsed = 0f;

        rb.linearVelocity = Vector2.zero;

        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;

            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float offset = oscillation * dampingCurve * bounceAmplitude * direction;

            float newY = baseY + offset;
            float deltaY = newY - previousY;

            rb.position = new Vector2(startPosition.x, newY);

            if (playerOnPlatform && playerRb != null)
            {
                float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
                Vector2 v = playerRb.linearVelocity;
                v.y = deltaY / dt;
                playerRb.linearVelocity = v;
            }

            previousY = newY;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.position = new Vector2(startPosition.x, baseY);

        isBouncing = false;
        bounceCoroutine = null;
    }

    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
        if (platformAudio != null) platformAudio.SetDirection(dir);
    }

    void OnDrawGizmos()
    {
        Vector2 referencePos = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 topPoint = referencePos + Vector2.up * upLimit;
        Vector2 bottomPoint = referencePos + Vector2.down * downLimit;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(topPoint, bottomPoint);
        Gizmos.DrawWireSphere(topPoint, 0.15f);
        Gizmos.DrawWireSphere(bottomPoint, 0.15f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(referencePos, 0.1f);
    }
}