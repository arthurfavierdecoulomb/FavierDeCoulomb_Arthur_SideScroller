using UnityEngine;
using System.Collections.Generic;

public class HazardManager : MonoBehaviour
{
    [System.Serializable]
    public class Hazard
    {
        [Header("Objet")]
        public GameObject target;

        [Header("Type de mouvement")]
        public MovementType movementType = MovementType.UpDown;

        [Header("Paramètres")]
        public float speed = 3f;
        public float amplitude = 2f;
        public float phase = 0f;

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

        // ── Données runtime ──
        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public Quaternion startRotation;
        [HideInInspector] public float timer;
        [HideInInspector] public float orbitAngle;
        [HideInInspector] public Vector3 previousPosition;
        [HideInInspector] public Animator animator;

        // Breakable runtime
        [HideInInspector] public bool playerOnPlatform;
        [HideInInspector] public float breakTimer;
        [HideInInspector] public bool isBroken;
        [HideInInspector] public bool isFalling;
        [HideInInspector] public float fallTimer;
        [HideInInspector] public Collider2D platformCollider;
        [HideInInspector] public SpriteRenderer spriteRenderer;
    }

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

    [Header("Liste des pièges")]
    [SerializeField] List<Hazard> hazards = new List<Hazard>();

    // Hash des paramètres Animator pour le monte-charge
    static readonly int MoveDir = Animator.StringToHash("moveDir");

    void Awake()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;
            h.startPosition = h.target.transform.position;
            h.startRotation = h.target.transform.rotation;
            h.timer = h.phase * Mathf.PI * 2f;
            h.orbitAngle = 0f;
            h.previousPosition = h.target.transform.position;
            h.animator = h.target.GetComponent<Animator>();

            if (h.movementType == MovementType.Breakable)
            {
                h.platformCollider = h.target.GetComponent<Collider2D>();
                h.spriteRenderer = h.target.GetComponent<SpriteRenderer>();

                // Ajoute le détecteur de collision si y'en a pas
                BreakablePlatformTrigger trigger = h.target.GetComponent<BreakablePlatformTrigger>();
                if (trigger == null)
                    trigger = h.target.AddComponent<BreakablePlatformTrigger>();
                trigger.hazard = h;
            }
        }
    }

    void Update()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;

            if (h.movementType == MovementType.Breakable)
            {
                ProcessBreakable(h);
                continue;
            }

            h.timer += Time.deltaTime * h.speed;
            ProcessHazard(h);
            UpdateHazardAnimation(h);
        }
    }

    void ProcessHazard(Hazard h)
    {
        Transform t = h.target.transform;

        switch (h.movementType)
        {
            case MovementType.UpDown:
                t.position = h.startPosition + new Vector3(0f, Mathf.Sin(h.timer) * h.amplitude, 0f);
                break;

            case MovementType.LeftRight:
                t.position = h.startPosition + new Vector3(Mathf.Sin(h.timer) * h.amplitude, 0f, 0f);
                break;

            case MovementType.Rotation:
                t.Rotate(0f, 0f, h.rotationSpeed * Time.deltaTime);
                break;

            case MovementType.CircularOrbit:
                h.orbitAngle += h.speed * Time.deltaTime;
                Vector3 center = h.orbitCenter != null ? h.orbitCenter.position : h.startPosition;
                t.position = center + new Vector3(
                    Mathf.Cos(h.orbitAngle) * h.orbitRadius,
                    Mathf.Sin(h.orbitAngle) * h.orbitRadius,
                    0f
                );
                break;

            case MovementType.PingPongDiag:
                float diag = Mathf.Sin(h.timer) * h.amplitude;
                t.position = h.startPosition + new Vector3(diag, diag, 0f);
                break;

            case MovementType.Pendulum:
                float angle = Mathf.Sin(h.timer) * h.pendulumMaxAngle;
                t.rotation = Quaternion.Euler(0f, 0f, angle);
                break;
        }
    }

    // ── Animation du monte-charge ──────────────────────────────
    void UpdateHazardAnimation(Hazard h)
    {
        if (h.animator == null) return;

        float velocityY = h.target.transform.position.y - h.previousPosition.y;
        h.previousPosition = h.target.transform.position;

        // moveDir : 1 = monte, -1 = descend, 0 = arrêté
        int dir = 0;
        if (velocityY > 0.01f) dir = 1;
        else if (velocityY < -0.01f) dir = -1;

        h.animator.SetInteger(MoveDir, dir);
    }

    // ── Plateforme cassable ────────────────────────────────────
    void ProcessBreakable(Hazard h)
    {
        if (h.isBroken)
        {
            if (h.isFalling)
            {
                h.target.transform.position += Vector3.down * h.fallSpeed * Time.deltaTime;
                h.fallTimer += Time.deltaTime;

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
            h.breakTimer += Time.deltaTime;

            // Optionnel : tremblement avant la casse
            float shake = Mathf.Sin(h.breakTimer * 40f) * 0.03f;
            h.target.transform.position = h.startPosition + new Vector3(shake, 0f, 0f);

            if (h.breakTimer >= h.breakDelay)
                BreakPlatform(h);
        }
        else
        {
            h.breakTimer = 0f;
            h.target.transform.position = h.startPosition;
        }
    }

    void BreakPlatform(Hazard h)
    {
        h.isBroken = true;
        h.isFalling = true;
        h.fallTimer = 0f;

        if (h.platformCollider != null)
            h.platformCollider.enabled = false;
    }

    // ── Appelé via Invoke pour respawn ──
    void RespawnBreakable()
    {
        foreach (Hazard h in hazards)
        {
            if (h.movementType != MovementType.Breakable) continue;
            if (h.target.activeSelf) continue;

            h.target.transform.position = h.startPosition;
            h.target.SetActive(true);
            h.isBroken = false;
            h.isFalling = false;
            h.breakTimer = 0f;
            h.playerOnPlatform = false;

            if (h.platformCollider != null)
                h.platformCollider.enabled = true;
            break;
        }
    }

    void OnDrawGizmos()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;

            Gizmos.color = Color.red;
            Vector3 origin = Application.isPlaying ? h.startPosition : h.target.transform.position;

            switch (h.movementType)
            {
                case MovementType.UpDown:
                    Gizmos.DrawLine(origin + Vector3.up * h.amplitude, origin + Vector3.down * h.amplitude);
                    break;
                case MovementType.LeftRight:
                    Gizmos.DrawLine(origin + Vector3.left * h.amplitude, origin + Vector3.right * h.amplitude);
                    break;
                case MovementType.CircularOrbit:
                    Vector3 c = h.orbitCenter != null ? h.orbitCenter.position : origin;
                    Gizmos.DrawWireSphere(c, h.orbitRadius);
                    break;
                case MovementType.PingPongDiag:
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