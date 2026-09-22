using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class TrainPlatform : MonoBehaviour
{
    enum TrainState { MovingRight, MovingLeft, Bouncing, Paused, WaitingForPlayer }

    [Header("Trajet")]
    [SerializeField] float rightDistance = 8f;
    [SerializeField] float leftDistance = 8f;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] bool startMovingRight = true;

    [Header("Attente du joueur")]
    [SerializeField] bool waitForPlayer = true;
    [SerializeField] float departDelay = 0.5f;
    [SerializeField] float maxWaitTime = 0f;

    [Header("Pause aux extrémités")]
    [SerializeField] float pauseDuration = 1f;

    [Header("Bounce d'arrivée")]
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

    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup loopGroup;
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Moteur (loop)")]
    [SerializeField] AudioClip motorLoop;
    [Range(0f, 1f)]
    [SerializeField] float motorIdleVolume = 0f;
    [Range(0f, 1f)]
    [SerializeField] float motorMaxVolume = 0.7f;
    [SerializeField] float motorIdlePitch = 0.6f;
    [SerializeField] float motorMaxPitch = 1f;
    [SerializeField] float motorSmoothing = 1.5f;

    [Header("Alarme (départ imminent / fin de course)")]
    [SerializeField] AudioClip[] alarmClips;
    [Range(0f, 1f)]
    [SerializeField] float alarmVolume = 0.8f;
    [SerializeField] float alarmLeadTime = 2f;
    [SerializeField] Vector2 alarmPitchRange = new Vector2(0.98f, 1.02f);

    [Header("Sons de proximité (milieu du wagon)")]
    [SerializeField] AudioProxi proximity;
    [SerializeField] AudioClip[] oxiLogoClips;
    [SerializeField] AudioClip[] oxiTextClips;
    [SerializeField] AudioClip[] leftArrowClips;
    [SerializeField] AudioClip[] rightArrowClips;
    [Range(0f, 1f)]
    [SerializeField] float proximitySfxVolume = 0.8f;
    [SerializeField] Vector2 proximityPitchRange = new Vector2(0.97f, 1.03f);

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

    AudioSource motorSource;
    AudioSource oneShotSource;
    AudioClip lastOneShotClip;
    bool departureAlarmPlayed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null) animator = GetComponent<Animator>();
        if (proximity == null) proximity = GetComponent<AudioProxi>();

        startPosition = rb.position;
        previousPosition = startPosition;

        rightLimitX = startPosition.x + rightDistance;
        leftLimitX = startPosition.x - leftDistance;

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.outputAudioMixerGroup = sfxGroup;

        if (motorLoop != null)
        {
            motorSource = gameObject.AddComponent<AudioSource>();
            motorSource.playOnAwake = false;
            motorSource.loop = true;
            motorSource.spatialBlend = 0f;
            motorSource.volume = motorIdleVolume;
            motorSource.pitch = motorIdlePitch;
            motorSource.clip = motorLoop;
            motorSource.outputAudioMixerGroup = loopGroup;
        }

        EnterWaitOrMove(startMovingRight);
    }

    void Start()
    {
        if (motorSource != null)
        {
            motorSource.Play();
            motorSource.time = Random.Range(0f, motorLoop.length);
        }
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
        departureAlarmPlayed = false;

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
        if (playerOnTrain)
        {
            timeSinceLastContact += Time.deltaTime;
            if (timeSinceLastContact > contactLostThreshold)
                DetachPlayer();
        }

        UpdateMotorAudio();
    }

    void DetachPlayer()
    {
        playerOnTrain = false;
        playerRb = null;
        waitTimer = 0f;
        departureAlarmPlayed = false;
    }

    void UpdateMotorAudio()
    {
        if (motorSource == null) return;

        bool moving = state == TrainState.MovingRight || state == TrainState.MovingLeft;
        float targetVolume = moving ? motorMaxVolume : motorIdleVolume;
        float targetPitch = moving ? motorMaxPitch : motorIdlePitch;

        motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVolume, motorSmoothing * Time.unscaledDeltaTime);
        motorSource.pitch = Mathf.MoveTowards(motorSource.pitch, targetPitch, motorSmoothing * Time.unscaledDeltaTime);

        if (motorSource.volume <= 0.001f && targetVolume <= 0.001f)
        {
            if (motorSource.isPlaying) motorSource.Pause();
        }
        else if (!motorSource.isPlaying)
        {
            motorSource.UnPause();
        }
    }

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

                CheckDepartureAlarm(departDelay - waitTimer);

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

        PlayAlarm();

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
        departureAlarmPlayed = false;
        float pauseElapsed = 0f;

        while (pauseElapsed < pauseDuration)
        {
            TransportPlayer();
            CheckDepartureAlarm(pauseDuration - pauseElapsed);
            pauseElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        EnterWaitOrMove(arrivalDir < 0);

        bounceCoroutine = null;
    }

    void CheckDepartureAlarm(float timeRemaining)
    {
        if (departureAlarmPlayed) return;
        if (timeRemaining > alarmLeadTime) return;

        departureAlarmPlayed = true;
        PlayAlarm();
    }

    void PlayAlarm()
    {
        PlayOneShot(alarmClips, alarmVolume, alarmPitchRange);
    }

    public void PlayOxiLogoSound()
    {
        PlayProximitySfx(oxiLogoClips);
    }

    public void PlayOxiTextSound()
    {
        PlayProximitySfx(oxiTextClips);
    }

    public void PlayLeftArrowSound()
    {
        PlayProximitySfx(leftArrowClips);
    }

    public void PlayRightArrowSound()
    {
        PlayProximitySfx(rightArrowClips);
    }

    void PlayProximitySfx(AudioClip[] clips)
    {
        float attenuation = proximity != null ? proximity.GetAttenuation() : 1f;
        if (attenuation <= 0.001f) return;

        PlayOneShot(clips, proximitySfxVolume * attenuation, proximityPitchRange);
    }

    void PlayOneShot(AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        if (oneShotSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastOneShotClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastOneShotClip = clip;
        oneShotSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShotSource.PlayOneShot(clip, volume);
    }

    void SetMoveDir(int dir)
    {
        if (dir == currentMoveDir) return;
        currentMoveDir = dir;
        if (animator != null) animator.SetInteger(MoveDirHash, dir);
    }

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