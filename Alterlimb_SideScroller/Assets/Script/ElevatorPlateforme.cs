using UnityEngine;
using System.Collections;

/// <summary>
/// Ascenseur à N étages contrôlable par le joueur.
/// 
/// Fonctionnement :
///   - Le joueur monte sur l'ascenseur (détecté via collision sur le dessus)
///   - Tant qu'il est dessus et que l'ascenseur est À L'ARRÊT :
///     * Z (upKey) → monte d'UN étage dans la liste
///     * S (downKey) → descend d'UN étage dans la liste
///   - Une fois lancé, le mouvement va FORCÉMENT jusqu'à l'étage suivant
///     (pas d'interruption ni de changement de direction en plein vol)
///   - À l'arrivée, un BOUNCE d'overshoot se joue (sinus amorti).
/// 
/// Setup :
///   - Crée des GameObjects vides nommés "Etage_0", "Etage_1", etc.
///     et place-les visuellement aux bonnes positions dans la scène.
///   - Drag-and-drop ces Transforms dans la liste "floors" de l'Inspector,
///     dans l'ordre que tu veux (ils seront triés automatiquement par Y).
///   - Configure "startFloorIndex" : l'étage initial (0 = le plus bas après tri).
/// 
/// Reset au respawn :
///   - L'ascenseur revient à son étage de départ quand le joueur meurt.
/// 
/// Animator :
///   - Paramètre Int "moveDir" :  1 = monte,  -1 = descend,  0 = arrêt
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ElevatorPlatform : MonoBehaviour
{
    enum ElevatorState { Idle, Moving, Bouncing }

    [Header("Contrôles")]
    [SerializeField] KeyCode upKey = KeyCode.W;
    [SerializeField] KeyCode downKey = KeyCode.S;

    [Header("Étages")]
    [Tooltip("Liste des Transforms représentant chaque étage. Place-les visuellement dans la scène.")]
    [SerializeField] Transform[] floors;
    [Tooltip("Index de l'étage de départ APRÈS tri par Y croissant (0 = étage le plus bas, etc.)")]
    [SerializeField] int startFloorIndex = 0;

    [Header("Mouvement")]
    [Tooltip("Vitesse de déplacement vertical (unités/seconde)")]
    [SerializeField] float moveSpeed = 4f;

    [Header("Bounce d'arrivée")]
    [Tooltip("Amplitude du premier overshoot (en unités Unity). 0.3 = 30cm.")]
    [SerializeField] float bounceAmplitude = 0.3f;
    [Tooltip("Durée totale du bounce (en secondes)")]
    [SerializeField] float bounceDuration = 0.5f;
    [Tooltip("Nombre de rebonds amortis")]
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Tooltip("Facteur d'amortissement entre rebonds")]
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
    Vector2 startPosition;
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

        // Trie les étages par Y croissant pour avoir une indexation cohérente
        BuildSortedFloorList();

        // Vérifie la validité de la config
        if (sortedFloorYs == null || sortedFloorYs.Length < 2)
        {
            Debug.LogError($"[ElevatorPlatform] '{name}' : il faut au moins 2 étages dans 'floors' !");
            enabled = false;
            return;
        }

        // Clamp l'index de départ et place l'ascenseur sur cet étage
        startFloorIndex = Mathf.Clamp(startFloorIndex, 0, sortedFloorYs.Length - 1);
        currentFloorIndex = startFloorIndex;
        targetFloorIndex = startFloorIndex;
        targetY = sortedFloorYs[startFloorIndex];

        // Snap initial à l'étage de départ
        rb.position = new Vector2(rb.position.x, targetY);
        startPosition = rb.position;

        SetMoveDir(0);
    }

    /// <summary>
    /// Construit le tableau sortedFloorYs en triant les positions Y des Transforms
    /// par ordre croissant. L'index 0 sera donc le plus bas.
    /// </summary>
    void BuildSortedFloorList()
    {
        if (floors == null || floors.Length == 0)
        {
            sortedFloorYs = null;
            return;
        }

        // Filtre les références nulles (au cas où un slot a été laissé vide)
        System.Collections.Generic.List<float> ys = new System.Collections.Generic.List<float>();
        foreach (Transform t in floors)
        {
            if (t != null) ys.Add(t.position.y);
        }

        ys.Sort();
        sortedFloorYs = ys.ToArray();
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
        // L'ascenseur NE revient PAS à sa position de départ : il reste là où
        // il est au moment de la mort (comportement naturel d'un ascenseur).
        // On se contente d'arrêter proprement tout mouvement / bounce en cours
        // et de remettre l'ascenseur dans un état stable "à l'arrêt".

        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }

        rb.linearVelocity = Vector2.zero;
        platformVelocity = Vector2.zero;

        // Si le joueur est mort pendant un trajet, on "termine" le mouvement :
        // l'ascenseur se cale sur l'étage le plus proche de sa position actuelle,
        // pour ne pas rester bloqué entre deux étages.
        SnapToNearestFloor();

        state = ElevatorState.Idle;

        DetachPlayer();
        SetMoveDir(0);
    }

    /// <summary>
    /// Cale l'ascenseur sur l'étage dont le Y est le plus proche de sa position
    /// actuelle, et met à jour currentFloorIndex en conséquence.
    /// Évite que l'ascenseur reste figé entre deux étages si le joueur meurt
    /// en plein trajet.
    /// </summary>
    void SnapToNearestFloor()
    {
        if (sortedFloorYs == null || sortedFloorYs.Length == 0) return;

        float currentY = rb.position.y;
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(sortedFloorYs[0] - currentY);

        for (int i = 1; i < sortedFloorYs.Length; i++)
        {
            float distance = Mathf.Abs(sortedFloorYs[i] - currentY);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        currentFloorIndex = nearestIndex;
        targetFloorIndex = nearestIndex;
        targetY = sortedFloorYs[nearestIndex];

        rb.position = new Vector2(rb.position.x, targetY);
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
    //  Logique de mouvement entre étages
    // ════════════════════════════════════════════════════════════

    /// <summary>Tente de monter d'UN étage (incrément d'index).</summary>
    void TryMoveUp()
    {
        if (currentFloorIndex >= sortedFloorYs.Length - 1) return; // déjà tout en haut
        StartMovement(currentFloorIndex + 1);
    }

    /// <summary>Tente de descendre d'UN étage (décrément d'index).</summary>
    void TryMoveDown()
    {
        if (currentFloorIndex <= 0) return; // déjà tout en bas
        StartMovement(currentFloorIndex - 1);
    }

    void StartMovement(int destinationIndex)
    {
        targetFloorIndex = destinationIndex;
        targetY = sortedFloorYs[destinationIndex];
        state = ElevatorState.Moving;
    }

    // ════════════════════════════════════════════════════════════
    //  FixedUpdate : mouvement principal (avec détection d'arrivée robuste)
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

            // Détection d'arrivée robuste : on arrête dès que le prochain pas dépasserait,
            // ou si on a déjà dépassé (signe inversé)
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
    //  Bounce : coroutine d'overshoot avec rebonds amortis
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
    //  Gizmos : tous les étages visualisés
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        if (floors == null || floors.Length == 0) return;

        float currentX = transform.position.x;

        // Récupère et trie les Y des étages valides (à chaque frame d'éditeur,
        // pour rester live quand on déplace les Transforms)
        System.Collections.Generic.List<float> ys = new System.Collections.Generic.List<float>();
        foreach (Transform t in floors)
        {
            if (t != null) ys.Add(t.position.y);
        }
        ys.Sort();

        if (ys.Count == 0) return;

        // Ligne verticale verte reliant tous les étages
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(currentX, ys[0], 0f),
                        new Vector3(currentX, ys[ys.Count - 1], 0f));

        // Sphère pour chaque étage avec couleur qui varie du rouge (bas) au cyan (haut)
        for (int i = 0; i < ys.Count; i++)
        {
            float ratio = (ys.Count == 1) ? 0f : (float)i / (ys.Count - 1);
            // Rouge → Jaune → Cyan
            Gizmos.color = (ratio < 0.5f)
                ? Color.Lerp(Color.red, Color.yellow, ratio * 2f)
                : Color.Lerp(Color.yellow, Color.cyan, (ratio - 0.5f) * 2f);

            Vector3 pos = new Vector3(currentX, ys[i], 0f);
            Gizmos.DrawWireSphere(pos, 0.25f);

            // Petite ligne horizontale pour mieux visualiser le niveau
            Gizmos.DrawLine(pos + Vector3.left * 0.5f, pos + Vector3.right * 0.5f);
        }
    }
}