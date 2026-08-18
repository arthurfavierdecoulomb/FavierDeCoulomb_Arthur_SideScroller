using UnityEngine;
using System.Collections;


[RequireComponent(typeof(Rigidbody2D))]
public class TrainPlatform : MonoBehaviour
{
    enum TrainState { MovingRight, MovingLeft, Bouncing, Paused, WaitingForPlayer }

    [Header("Trajet")]
    [Tooltip("Distance parcourue vers la droite depuis la position de départ")]
    [SerializeField] float rightDistance = 8f;
    [Tooltip("Distance parcourue vers la gauche depuis la position de départ")]
    [SerializeField] float leftDistance = 8f;
    [Tooltip("Vitesse de déplacement du train (unités/seconde)")]
    [SerializeField] float moveSpeed = 5f;
    [Tooltip("Direction de départ du train")]
    [SerializeField] bool startMovingRight = true;

    [Header("Attente du joueur")]
    [Tooltip("Si coché : le train ne part que lorsque le joueur est monté dessus")]
    [SerializeField] bool waitForPlayer = true;
    [Tooltip("Délai entre le moment où le joueur monte et le départ (laisse le temps de se stabiliser)")]
    [SerializeField] float departDelay = 0.5f;
    [Tooltip("Temps max d'attente sans joueur avant de repartir quand même. 0 = attente infinie.")]
    [SerializeField] float maxWaitTime = 0f;

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

   
    bool nextDirIsRight;   
    float waitTimer;       
    float emptyWaitTimer;  

    float rightLimitX;
    float leftLimitX;

    Coroutine bounceCoroutine;

    
    //  Initialisation
    
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

        EnterWaitOrMove(startMovingRight);
    }

    
    //  Respawn
    
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

        DetachPlayer();
        SetMoveDir(0);

        EnterWaitOrMove(startMovingRight);
    }

    
    void EnterWaitOrMove(bool goRight)
    {
        nextDirIsRight = goRight;
        waitTimer = 0f;
        emptyWaitTimer = 0f;

        if (waitForPlayer)
            state = TrainState.WaitingForPlayer;
        else
            state = goRight ? TrainState.MovingRight : TrainState.MovingLeft;
    }

    void Depart()
    {
        state = nextDirIsRight ? TrainState.MovingRight : TrainState.MovingLeft;
        waitTimer = 0f;
        emptyWaitTimer = 0f;
    }

    
    //  Détection du joueur (collision sur le dessus)
    
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

    //  FixedUpdate : mouvement + transport du joueur
    
    void FixedUpdate()
    {
       
        if (state == TrainState.Bouncing)
        {
            TransportPlayer();
            return;
        }

       
        if (state == TrainState.WaitingForPlayer)
        {
            TransportPlayer();
            SetMoveDir(0);

            if (playerOnTrain)
            {
                emptyWaitTimer = 0f;
                waitTimer += Time.fixedDeltaTime;

                if (waitTimer >= departDelay)
                    Depart();
            }
            else
            {
                
                waitTimer = 0f;

                if (maxWaitTime > 0f)
                {
                    emptyWaitTimer += Time.fixedDeltaTime;
                    if (emptyWaitTimer >= maxWaitTime)
                        Depart();
                }
            }
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

        
        TransportPlayer();
        SetMoveDir(newMoveDir);
    }

    
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

    
    void StartBounceAndPause(int arrivalDir)
    {
        state = TrainState.Bouncing;
        SetMoveDir(0);

        if (animator != null)
        {
            if (arrivalDir > 0) animator.SetTrigger(ArretDroitHash);
            else animator.SetTrigger(ArretGaucheHash);
        }

        bounceCoroutine = StartCoroutine(BounceThenPauseThenReverse(arrivalDir));
    }

    IEnumerator BounceThenPauseThenReverse(int arrivalDir)
    {
        
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

        
        state = TrainState.Paused;
        float pauseElapsed = 0f;

        while (pauseElapsed < pauseDuration)
        {
            TransportPlayer();
            pauseElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        
        EnterWaitOrMove(arrivalDir < 0);

        bounceCoroutine = null;
    }

    
    //  Animator helper
    
    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
    }

    
    //  Gizmos
    
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