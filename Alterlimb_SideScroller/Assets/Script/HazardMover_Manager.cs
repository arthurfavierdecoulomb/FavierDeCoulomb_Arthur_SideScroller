using UnityEngine;
using System.Collections.Generic;

public class HazardManager : MonoBehaviour
{
    public enum MovementType
    {
        UpDown,
        LeftRight,
        Rotation,
        CircularOrbit,
        PingPongDiag,
        Pendulum,
        Breakable
    }

    [System.Serializable]
    public class Hazard
    {
        [Header("Objet")]
        public GameObject target;

        [Header("Type de mouvement")]
        public MovementType movementType = MovementType.UpDown;

        [Header("Paramètres généraux")]
        public float speed = 3f;
        public float amplitude = 2f;

        [Range(0f, 1f)]
        public float phase = 0f;

        [Header("Pause aux extrémités (UpDown / LeftRight)")]
        public float pauseDuration = 0f;

        [Header("Rebond aux extrémités (UpDown / LeftRight)")]
        public float bounceOvershoot = 0f;
        public float bounceDuration = 0.3f;
        public int bounceCount = 2;

        [Header("Rotation")]
        public float rotationSpeed = 90f;

        [Header("Orbite")]
        public Transform orbitCenter;
        public float orbitRadius = 3f;

        [Header("Pendule")]
        public float pendulumMaxAngle = 45f;

        [Header("Plateforme Cassable")]
        public float breakDelay = 1.5f;
        public float fallSpeed = 8f;
        public float destroyDelay = 2f;
        public float respawnDelay = 5f;

        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public Quaternion startRotation;
        [HideInInspector] public float timer;
        [HideInInspector] public float orbitAngle;
        [HideInInspector] public Vector3 previousPosition;
        [HideInInspector] public Animator animator;
        [HideInInspector] public Rigidbody2D rb;
        [HideInInspector] public bool justResumed;
        [HideInInspector] public PlatformAudio platformAudio;
        [HideInInspector] public int audioDirection;

        [HideInInspector] public bool isPaused;
        [HideInInspector] public float pauseTimer;

        [HideInInspector] public bool isBouncing;
        [HideInInspector] public float bounceTimer;
        [HideInInspector] public float bounceExtremitySign;

        [HideInInspector] public bool playerOnPlatform;
        [HideInInspector] public float breakTimer;
        [HideInInspector] public bool isBroken;
        [HideInInspector] public bool isFalling;
        [HideInInspector] public float fallTimer;
        [HideInInspector] public Collider2D platformCollider;
        [HideInInspector] public SpriteRenderer spriteRenderer;
    }

    [Header("Liste des pièges")]
    [SerializeField] List<Hazard> hazards = new List<Hazard>();

    static readonly int MoveDir = Animator.StringToHash("moveDir");

    void Awake()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;

            h.startPosition = h.target.transform.position;
            h.startRotation = h.target.transform.rotation;
            h.timer = 0f;
            h.orbitAngle = h.phase * Mathf.PI * 2f;
            h.previousPosition = h.target.transform.position;
            h.animator = h.target.GetComponent<Animator>();
            h.rb = h.target.GetComponent<Rigidbody2D>();
            h.platformAudio = h.target.GetComponent<PlatformAudio>();
            h.audioDirection = 0;

            if (h.movementType == MovementType.Breakable)
                InitBreakable(h);
        }
    }

    void InitBreakable(Hazard h)
    {
        h.platformCollider = h.target.GetComponent<Collider2D>();
        h.spriteRenderer = h.target.GetComponent<SpriteRenderer>();

        BreakablePlatformTrigger trigger = h.target.GetComponent<BreakablePlatformTrigger>();
        if (trigger == null)
            trigger = h.target.AddComponent<BreakablePlatformTrigger>();
        trigger.hazard = h;
    }

    void FixedUpdate()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;

            if (h.movementType == MovementType.Breakable)
            {
                ProcessBreakable(h);
                continue;
            }

            if (h.isBouncing)
            {
                h.bounceTimer -= Time.fixedDeltaTime;
                ProcessBounce(h);
                UpdateHazardAnimation(h);
                UpdateHazardAudio(h);

                if (h.bounceTimer <= 0f)
                    EndBounce(h);

                continue;
            }

            if (h.isPaused)
            {
                h.pauseTimer -= Time.fixedDeltaTime;
                if (h.pauseTimer <= 0f)
                {
                    h.isPaused = false;
                    h.justResumed = true;
                }

                UpdateHazardAnimation(h);
                UpdateHazardAudio(h);
                continue;
            }

            h.timer += Time.fixedDeltaTime * h.speed;
            ProcessHazard(h);
            UpdateHazardAnimation(h);
            UpdateHazardAudio(h);
        }
    }

    void ProcessHazard(Hazard h)
    {
        Transform t = h.target.transform;
        float phaseRad = h.phase * Mathf.PI * 2f;

        switch (h.movementType)
        {
            case MovementType.UpDown:
                {
                    float sin = Mathf.Sin(h.timer + phaseRad);
                    Vector3 newPos = h.startPosition + new Vector3(0f, sin * h.amplitude, 0f);
                    MovePlatform(h, newPos);
                    h.audioDirection = Mathf.Cos(h.timer + phaseRad) >= 0f ? 1 : -1;
                    CheckPause(h, sin);
                    break;
                }

            case MovementType.LeftRight:
                {
                    float sin = Mathf.Sin(h.timer + phaseRad);
                    Vector3 newPos = h.startPosition + new Vector3(sin * h.amplitude, 0f, 0f);
                    MovePlatform(h, newPos);
                    h.audioDirection = Mathf.Cos(h.timer + phaseRad) >= 0f ? 1 : -1;
                    CheckPause(h, sin);
                    break;
                }

            case MovementType.Rotation:
                t.Rotate(0f, 0f, h.rotationSpeed * Time.fixedDeltaTime);
                break;

            case MovementType.CircularOrbit:
                {
                    h.orbitAngle += h.speed * Time.fixedDeltaTime;
                    Vector3 center = h.orbitCenter != null ? h.orbitCenter.position : h.startPosition;
                    Vector3 newPos = center + new Vector3(
                        Mathf.Cos(h.orbitAngle) * h.orbitRadius,
                        Mathf.Sin(h.orbitAngle) * h.orbitRadius,
                        0f
                    );
                    MovePlatform(h, newPos);
                    break;
                }

            case MovementType.PingPongDiag:
                {
                    float diag = Mathf.Sin(h.timer + phaseRad) * h.amplitude;
                    Vector3 newPos = h.startPosition + new Vector3(diag, diag, 0f);
                    MovePlatform(h, newPos);
                    break;
                }

            case MovementType.Pendulum:
                {
                    float angle = Mathf.Sin(h.timer + phaseRad) * h.pendulumMaxAngle;
                    Quaternion newRot = Quaternion.Euler(0f, 0f, angle);
                    if (h.rb != null) h.rb.MoveRotation(newRot);
                    else t.rotation = newRot;
                    break;
                }
        }
    }

    void MovePlatform(Hazard h, Vector3 targetPos)
    {
        if (h.rb != null)
            h.rb.MovePosition(targetPos);
        else
            h.target.transform.position = targetPos;
    }

    void CheckPause(Hazard h, float sinValue)
    {
        bool bounceEnabled = h.bounceOvershoot > 0f && h.bounceDuration > 0f;
        if (h.pauseDuration <= 0f && !bounceEnabled) return;

        if (h.justResumed)
        {
            if (Mathf.Abs(Mathf.Abs(sinValue) - 1f) > 0.1f)
                h.justResumed = false;
            return;
        }

        if (Mathf.Abs(Mathf.Abs(sinValue) - 1f) < 0.02f)
        {
            if (bounceEnabled)
            {
                h.isBouncing = true;
                h.bounceTimer = h.bounceDuration;
                h.bounceExtremitySign = Mathf.Sign(sinValue);
            }
            else
            {
                h.isPaused = true;
                h.pauseTimer = h.pauseDuration;
            }
        }
    }

    void ProcessBounce(Hazard h)
    {
        float elapsed = h.bounceDuration - h.bounceTimer;
        float decay = Mathf.Exp(-6f * elapsed / h.bounceDuration);
        float oscillation = Mathf.Cos(elapsed * h.bounceCount * 2f * Mathf.PI / h.bounceDuration);
        float displacement = h.bounceExtremitySign * (h.amplitude + h.bounceOvershoot * decay * oscillation);

        Vector3 newPos = h.movementType == MovementType.UpDown
            ? h.startPosition + new Vector3(0f, displacement, 0f)
            : h.startPosition + new Vector3(displacement, 0f, 0f);

        MovePlatform(h, newPos);
        h.audioDirection = 0;
    }

    void EndBounce(Hazard h)
    {
        h.isBouncing = false;

        float displacement = h.bounceExtremitySign * h.amplitude;
        Vector3 finalPos = h.movementType == MovementType.UpDown
            ? h.startPosition + new Vector3(0f, displacement, 0f)
            : h.startPosition + new Vector3(displacement, 0f, 0f);

        MovePlatform(h, finalPos);

        if (h.pauseDuration > 0f)
        {
            h.isPaused = true;
            h.pauseTimer = h.pauseDuration;
        }
        else
        {
            h.justResumed = true;
        }
    }

    void UpdateHazardAnimation(Hazard h)
    {
        if (h.animator == null) return;

        float velocityY = h.target.transform.position.y - h.previousPosition.y;
        h.previousPosition = h.target.transform.position;

        int dir = 0;
        if (velocityY > 0.01f) dir = 1;
        else if (velocityY < -0.01f) dir = -1;

        h.animator.SetInteger(MoveDir, dir);
    }

    void UpdateHazardAudio(Hazard h)
    {
        if (h.platformAudio == null) return;
        if (h.movementType != MovementType.UpDown && h.movementType != MovementType.LeftRight) return;

        h.platformAudio.SetDirection(h.isPaused || h.isBouncing ? 0 : h.audioDirection);
    }

    void ProcessBreakable(Hazard h)
    {
        if (h.isBroken)
        {
            if (h.isFalling)
            {
                Vector3 newPos = h.target.transform.position + Vector3.down * h.fallSpeed * Time.fixedDeltaTime;
                MovePlatform(h, newPos);
                h.fallTimer += Time.fixedDeltaTime;

                if (h.fallTimer >= h.destroyDelay)
                {
                    h.target.SetActive(false);
                    h.isFalling = false;
                    Invoke(nameof(RespawnBreakable), h.respawnDelay);
                }
            }
            return;
        }

        if (h.playerOnPlatform)
        {
            h.breakTimer += Time.fixedDeltaTime;

            float shake = Mathf.Sin(h.breakTimer * 40f) * 0.03f;
            Vector3 newPos = h.startPosition + new Vector3(shake, 0f, 0f);
            MovePlatform(h, newPos);

            if (h.platformAudio != null)
                h.platformAudio.SetCollapsing(true, h.breakDelay > 0f ? h.breakTimer / h.breakDelay : 1f);

            if (h.breakTimer >= h.breakDelay)
                BreakPlatform(h);
        }
        else
        {
            h.breakTimer = 0f;
            MovePlatform(h, h.startPosition);

            if (h.platformAudio != null)
                h.platformAudio.SetCollapsing(false, 0f);
        }
    }

    void BreakPlatform(Hazard h)
    {
        h.isBroken = true;
        h.isFalling = true;
        h.fallTimer = 0f;

        if (h.platformCollider != null)
            h.platformCollider.enabled = false;

        if (h.platformAudio != null)
            h.platformAudio.PlayBreak();
    }

    void RespawnBreakable()
    {
        foreach (Hazard h in hazards)
        {
            if (h.movementType != MovementType.Breakable) continue;
            if (h.target.activeSelf) continue;

            MovePlatform(h, h.startPosition);
            h.target.SetActive(true);
            h.isBroken = false;
            h.isFalling = false;
            h.breakTimer = 0f;
            h.playerOnPlatform = false;

            if (h.platformCollider != null)
                h.platformCollider.enabled = true;

            if (h.platformAudio != null)
                h.platformAudio.SetCollapsing(false, 0f);
            break;
        }
    }

    void OnDrawGizmos()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;

            Vector3 origin = Application.isPlaying ? h.startPosition : h.target.transform.position;

            switch (h.movementType)
            {
                case MovementType.UpDown:
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin + Vector3.up * h.amplitude, origin + Vector3.down * h.amplitude);
                    Gizmos.DrawWireSphere(origin + Vector3.up * h.amplitude, 0.15f);
                    Gizmos.DrawWireSphere(origin + Vector3.down * h.amplitude, 0.15f);
                    break;

                case MovementType.LeftRight:
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin + Vector3.left * h.amplitude, origin + Vector3.right * h.amplitude);
                    Gizmos.DrawWireSphere(origin + Vector3.left * h.amplitude, 0.15f);
                    Gizmos.DrawWireSphere(origin + Vector3.right * h.amplitude, 0.15f);
                    break;

                case MovementType.CircularOrbit:
                    Gizmos.color = Color.red;
                    Vector3 c = h.orbitCenter != null ? h.orbitCenter.position : origin;
                    Gizmos.DrawWireSphere(c, h.orbitRadius);
                    break;

                case MovementType.PingPongDiag:
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin - new Vector3(h.amplitude, h.amplitude, 0f),
                                    origin + new Vector3(h.amplitude, h.amplitude, 0f));
                    break;

                case MovementType.Pendulum:
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(origin, 0.2f);
                    break;

                case MovementType.Breakable:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(origin, Vector3.one);
                    break;
            }
        }
    }
}