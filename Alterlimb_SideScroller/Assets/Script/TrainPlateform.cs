using UnityEngine;
using System.Collections;

/// <summary>
/// Train qui fait des allers-retours automatiques entre deux points.
/// Le joueur peut monter dessus et garder le contrôle total (saut + déplacement) :
/// le train l'emmène en appliquant son delta de déplacement à la position du
/// joueur, sans toucher à sa vélocité (donc ses sauts/courses restent intacts).
/// 
/// Cycle : roule vers une extrémité → bounce d'arrivée → pause → repart dans
/// l'autre sens → etc. (boucle infinie).
/// 
/// Le joueur transporté :
///   - On calcule le delta de déplacement du train à chaque FixedUpdate.
///   - Si le joueur est sur le train, on ajoute ce delta à sa position.
///   - On NE touche PAS à sa vélocité → il peut sauter/courir/dasher normalement.
///   - Pendant un saut, le joueur quitte le contact → il n'est plus transporté
///     (inertie réaliste : il "rate" le train s'il saute trop longtemps).
/// 
/// Reset au respawn via SpawnManager.OnPlayerRespawn.
/// 
/// Animator :
///   - Int "moveDir" : 1 = droite, -1 = gauche, 0 = arrêt
///   - Trigger "arretDroit" / "arretGauche" : bounce visuel d'arrivée
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TrainPlatform : MonoBehaviour
{
    enum TrainState { MovingRight, MovingLeft, Bouncing, Paused }

    [Header("Trajet")]
    [Tooltip("Distance parcourue vers la droite depuis la position de départ")]
    [SerializeField] float rightDistance = 8f;
    [Tooltip("Distance parcourue vers la gauche depuis la position de départ")]
    [SerializeField] float leftDistance = 8f;
    [Tooltip("Vitesse de déplacement du train (unités/seconde)")]
    [SerializeField] float moveSpeed = 5f;
    [Tooltip("Direction de départ du train")]
    [SerializeField] bool startMovingRight = true;

    [Header("Pause aux extrémités")]
    [Tooltip("Temps d'arrêt à chaque extrémité avant de repartir")]
    [SerializeField] float pauseDuration = 1f;

    [Header("Bounce d'arrivée")]
    [Tooltip("Amplitude du premier overshoot (en unités Unity)")]
    [SerializeField] float bounceAmplitude = 0.3f;
    [Tooltip("Durée totale du bounce (en secondes)")]
    [SerializeField] float bounceDuration = 0.5f;
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.4f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";
    [Tooltip("Angle maximal du contact pour considérer que le joueur est SUR le train.")]
    [SerializeField] float maxStandingAngle = 45f;
    [Tooltip("Délai sans contact avant de considérer que le joueur a quitté le train.")]
    [SerializeField] float contactLostThreshold = 0.1f;

    [Header("Animation")]
    [SerializeField] Animator animator;

    static readonly int MoveDirHash = Animator.StringToHash("moveDir");
    static readonly int ArretDroitHash = Animator.StringToHash("arretDroit");
    static readonly int ArretGaucheHash = Animator.StringToHash("arretGauche");

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    Vector2 startPosition;
    Vector2 previousPosition;

    bool playerOnTrain;
    float timeSinceLastContact;
    int currentMoveDir;

    TrainState state;
    float rightLimitX;
    float leftLimitX;
    Coroutine bounceCoroutine;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null) animator = GetComponent<Animator>();

        startPosition = rb.position;
        previousPosition = startPosition;

        rightLimitX = startPosition.x + rightDistance;
        leftLimitX = startPosition.x - leftDistance;

        state = startMovingRight ? TrainState.MovingRight : TrainState.MovingLeft;
    }

    // ════════════════════════════════════════════════════════════
    //  Respawn
    // ════════════════════════════════════════════════════════════

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

        rb.position = startPosition;
        previousPosition = startPosition;
        rb.linearVelocity = Vector2.zero;

        state = startMovingRight ? TrainState.MovingRight : TrainState.MovingLeft;

        DetachPlayer();
        SetMoveDir(0);
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du joueur (collision sur le dessus)
    // ════════════════════════════════════════════════════════════

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            float angle = Vector2.Angle(contact.normal, Vector2.down);
            if (angle <= maxStandingAngle)
            {
                if (!playerOnTrain)
                {
                    playerRb = collision.collider.attachedRigidbody;
                    playerOnTrain = true;
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
        if (!playerOnTrain) return;

        timeSinceLastContact += Time.deltaTime;
        if (timeSinceLastContact > contactLostThreshold)
            DetachPlayer();
    }

    void DetachPlayer()
    {
        playerOnTrain = false;
        playerRb = null;
    }

    // ════════════════════════════════════════════════════════════
    //  FixedUpdate : mouvement + transport du joueur
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        // Pendant le bounce, la coroutine gère le mouvement.
        // On gère quand même le transport du joueur (voir plus bas).
        if (state == TrainState.Bouncing)
        {
            TransportPlayer();
            return;
        }

        int newMoveDir = 0;

        if (state == TrainState.MovingRight)
        {
            float step = moveSpeed * Time.fixedDeltaTime;
            float newX = rb.position.x + step;
            newMoveDir = 1;

            if (newX >= rightLimitX)
            {
                rb.position = new Vector2(rightLimitX, startPosition.y);
                TransportPlayer();
                StartBounceAndPause(+1);
                return;
            }

            rb.position = new Vector2(newX, startPosition.y);
        }
        else if (state == TrainState.MovingLeft)
        {
            float step = moveSpeed * Time.fixedDeltaTime;
            float newX = rb.position.x - step;
            newMoveDir = -1;

            if (newX <= leftLimitX)
            {
                rb.position = new Vector2(leftLimitX, startPosition.y);
                TransportPlayer();
                StartBounceAndPause(-1);
                return;
            }

            rb.position = new Vector2(newX, startPosition.y);
        }

        // Transport du joueur (applique le delta du train à sa position)
        TransportPlayer();

        SetMoveDir(newMoveDir);
    }

    /// <summary>
    /// Applique le déplacement du train (delta depuis la frame précédente)
    /// à la position du joueur, SANS toucher à sa vélocité.
    /// Le joueur garde donc le plein contrôle de ses sauts et déplacements.
    /// </summary>
    void TransportPlayer()
    {
        Vector2 currentPos = rb.position;
        Vector2 trainDelta = currentPos - previousPosition;
        previousPosition = currentPos;

        if (playerOnTrain && playerRb != null && trainDelta.sqrMagnitude > 0.0000001f)
        {
            playerRb.position += trainDelta;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Bounce + pause + demi-tour
    // ════════════════════════════════════════════════════════════

    void StartBounceAndPause(int arrivalDir)
    {
        state = TrainState.Bouncing;
        SetMoveDir(0);

        // Déclenche l'animation de bounce visuel correspondante
        if (animator != null)
        {
            if (arrivalDir > 0) animator.SetTrigger(ArretDroitHash);
            else animator.SetTrigger(ArretGaucheHash);
        }

        bounceCoroutine = StartCoroutine(BounceThenPauseThenReverse(arrivalDir));
    }

    IEnumerator BounceThenPauseThenReverse(int arrivalDir)
    {
        // ── Bounce d'overshoot horizontal ───────────────────────
        float baseX = rb.position.x;
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;
            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float offset = oscillation * dampingCurve * bounceAmplitude * arrivalDir;

            rb.position = new Vector2(baseX + offset, startPosition.y);

            TransportPlayer();

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.position = new Vector2(baseX, startPosition.y);
        TransportPlayer();

        // ── Pause ───────────────────────────────────────────────
        state = TrainState.Paused;

        float pauseElapsed = 0f;
        while (pauseElapsed < pauseDuration)
        {
            TransportPlayer(); // le train est immobile mais on garde la cohérence
            pauseElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // ── Demi-tour : on repart dans l'autre sens ─────────────
        state = (arrivalDir > 0) ? TrainState.MovingLeft : TrainState.MovingRight;

        bounceCoroutine = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Animator helper
    // ════════════════════════════════════════════════════════════

    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        Vector2 reference = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 rightPoint = reference + Vector2.right * rightDistance;
        Vector2 leftPoint = reference + Vector2.left * leftDistance;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(leftPoint, rightPoint);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rightPoint, 0.25f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(leftPoint, 0.25f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(reference, 0.20f);
    }
}