using UnityEngine;
using System.Collections;

/// <summary>
/// Ascenseur à N étages.
/// 
/// Deux façons de le commander :
///   - PILOTAGE : le joueur monte dessus, et tant qu'il est dessus et que
///     l'ascenseur est à l'arrêt, W/S le déplacent d'UN étage.
///   - APPEL : des boutons d'appel (ElevatorCallButton), un par étage,
///     peuvent appeler l'ascenseur à leur étage via CallToFloor(). L'ascenseur
///     traverse alors autant d'étages que nécessaire d'un seul trajet.
/// 
/// À l'arrivée, un BOUNCE d'overshoot se joue (sinus amorti).
/// 
/// Pas de logique de respawn : le système de boutons d'appel garantit que
/// le joueur peut toujours faire venir l'ascenseur, où qu'il respawn.
/// 
/// Setup :
///   - GameObjects vides "Etage_0", "Etage_1"... placés dans la scène,
///     drag-and-droppés dans "floors" (triés automatiquement par Y).
/// 
/// Animator :
///   - Paramètre Int "moveDir" : 1 = monte, -1 = descend, 0 = arrêt
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ElevatorPlatform : MonoBehaviour
{
    enum ElevatorState { Idle, Moving, Bouncing }

    [Header("Contrôles de pilotage (joueur sur l'ascenseur)")]
    [SerializeField] KeyCode upKey = KeyCode.W;
    [SerializeField] KeyCode downKey = KeyCode.S;

    [Header("Étages")]
    [Tooltip("Liste des Transforms représentant chaque étage. Place-les visuellement dans la scène.")]
    [SerializeField] Transform[] floors;
    [Tooltip("Index de l'étage de départ APRÈS tri par Y croissant (0 = étage le plus bas)")]
    [SerializeField] int startFloorIndex = 0;

    [Header("Mouvement")]
    [SerializeField] float moveSpeed = 4f;

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

    static readonly int MoveDirHash = Animator.StringToHash("moveDir");

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    Vector2 platformVelocity;

    bool playerOnPlatform;
    float timeSinceLastContact;
    int currentMoveDir;

    // ── Étages triés par Y croissant (0 = le plus bas) ──
    float[] sortedFloorYs;
    int currentFloorIndex;
    int targetFloorIndex;
    float targetY;

    ElevatorState state = ElevatorState.Idle;
    Coroutine bounceCoroutine;

    // ── Accès public ──
    public int FloorCount => sortedFloorYs != null ? sortedFloorYs.Length : 0;
    public int CurrentFloor => currentFloorIndex;
    public bool IsIdle => state == ElevatorState.Idle;

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

        BuildSortedFloorList();

        if (sortedFloorYs == null || sortedFloorYs.Length < 2)
        {
            Debug.LogError($"[ElevatorPlatform] '{name}' : il faut au moins 2 étages dans 'floors' !");
            enabled = false;
            return;
        }

        startFloorIndex = Mathf.Clamp(startFloorIndex, 0, sortedFloorYs.Length - 1);
        currentFloorIndex = startFloorIndex;
        targetFloorIndex = startFloorIndex;
        targetY = sortedFloorYs[startFloorIndex];

        rb.position = new Vector2(rb.position.x, targetY);

        SetMoveDir(0);
    }

    void BuildSortedFloorList()
    {
        if (floors == null || floors.Length == 0)
        {
            sortedFloorYs = null;
            return;
        }

        System.Collections.Generic.List<float> ys = new System.Collections.Generic.List<float>();
        foreach (Transform t in floors)
        {
            if (t != null) ys.Add(t.position.y);
        }

        ys.Sort();
        sortedFloorYs = ys.ToArray();
    }

    // ════════════════════════════════════════════════════════════
    //  Détection du joueur
    // ════════════════════════════════════════════════════════════

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
        if (playerOnPlatform)
        {
            timeSinceLastContact += Time.deltaTime;
            if (timeSinceLastContact > contactLostThreshold)
                DetachPlayer();
        }

        // Pilotage W/S : seulement si le joueur est dessus et l'ascenseur à l'arrêt
        if (state == ElevatorState.Idle && playerOnPlatform)
        {
            if (Input.GetKeyDown(upKey)) TryMoveUp();
            else if (Input.GetKeyDown(downKey)) TryMoveDown();
        }
    }

    void DetachPlayer()
    {
        playerOnPlatform = false;
        playerRb = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Commandes : pilotage (1 étage) et appel (multi-étages)
    // ════════════════════════════════════════════════════════════

    /// <summary>Pilotage : monte d'UN étage.</summary>
    void TryMoveUp()
    {
        if (currentFloorIndex >= sortedFloorYs.Length - 1) return;
        StartMovement(currentFloorIndex + 1);
    }

    /// <summary>Pilotage : descend d'UN étage.</summary>
    void TryMoveDown()
    {
        if (currentFloorIndex <= 0) return;
        StartMovement(currentFloorIndex - 1);
    }

    /// <summary>
    /// APPEL : fait venir l'ascenseur à un étage précis (depuis un bouton d'appel).
    /// L'ascenseur traverse autant d'étages que nécessaire en un seul trajet.
    /// Ignoré si l'ascenseur n'est pas à l'arrêt, ou s'il est déjà à cet étage.
    /// </summary>
    public void CallToFloor(int floorIndex)
    {
        if (sortedFloorYs == null) return;

        // L'appel n'est pris en compte que si l'ascenseur est à l'arrêt
        if (state != ElevatorState.Idle)
        {
            Debug.Log("[ElevatorPlatform] Appel ignoré : l'ascenseur est déjà en mouvement.");
            return;
        }

        floorIndex = Mathf.Clamp(floorIndex, 0, sortedFloorYs.Length - 1);

        // Déjà à cet étage : rien à faire
        if (floorIndex == currentFloorIndex)
        {
            Debug.Log($"[ElevatorPlatform] Appel ignoré : déjà à l'étage {floorIndex}.");
            return;
        }

        StartMovement(floorIndex);
    }

    void StartMovement(int destinationIndex)
    {
        targetFloorIndex = destinationIndex;
        targetY = sortedFloorYs[destinationIndex];
        state = ElevatorState.Moving;
    }

    // ════════════════════════════════════════════════════════════
    //  FixedUpdate : mouvement (gère un trajet multi-étages)
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        int newMoveDir = 0;
        platformVelocity = Vector2.zero;

        if (state == ElevatorState.Moving)
        {
            float currentY = rb.position.y;
            float remaining = targetY - currentY;
            int desiredDir = (remaining > 0f) ? 1 : -1;
            float stepDistance = moveSpeed * Time.fixedDeltaTime;

            bool wouldOvershoot = Mathf.Abs(remaining) <= stepDistance;
            bool alreadyOvershot = (currentMoveDir != 0) && (desiredDir != currentMoveDir);

            if (wouldOvershoot || alreadyOvershot)
            {
                rb.position = new Vector2(rb.position.x, targetY);
                currentFloorIndex = targetFloorIndex;

                float bounceDirection = currentMoveDir != 0 ? currentMoveDir : desiredDir;

                state = ElevatorState.Bouncing;
                SetMoveDir(0);

                bounceCoroutine = StartCoroutine(BounceRoutine(bounceDirection));
                return;
            }

            newMoveDir = desiredDir;
            platformVelocity = new Vector2(0f, newMoveDir * moveSpeed);
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
    }

    // ════════════════════════════════════════════════════════════
    //  Bounce
    // ════════════════════════════════════════════════════════════

    IEnumerator BounceRoutine(float direction)
    {
        float baseY = targetY;
        float elapsed = 0f;
        float previousY = baseY;

        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;
            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float offset = oscillation * dampingCurve * bounceAmplitude * direction;

            float newY = baseY + offset;
            float deltaY = newY - previousY;

            rb.position = new Vector2(rb.position.x, newY);

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

        rb.position = new Vector2(rb.position.x, baseY);
        state = ElevatorState.Idle;
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
        if (floors == null || floors.Length == 0) return;

        float currentX = transform.position.x;

        System.Collections.Generic.List<float> ys = new System.Collections.Generic.List<float>();
        foreach (Transform t in floors)
        {
            if (t != null) ys.Add(t.position.y);
        }
        ys.Sort();

        if (ys.Count == 0) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(currentX, ys[0], 0f),
                        new Vector3(currentX, ys[ys.Count - 1], 0f));

        for (int i = 0; i < ys.Count; i++)
        {
            float ratio = (ys.Count == 1) ? 0f : (float)i / (ys.Count - 1);
            Gizmos.color = (ratio < 0.5f)
                ? Color.Lerp(Color.red, Color.yellow, ratio * 2f)
                : Color.Lerp(Color.yellow, Color.cyan, (ratio - 0.5f) * 2f);

            Vector3 pos = new Vector3(currentX, ys[i], 0f);
            Gizmos.DrawWireSphere(pos, 0.25f);
            Gizmos.DrawLine(pos + Vector3.left * 0.5f, pos + Vector3.right * 0.5f);
        }
    }
}