using UnityEngine;

/// <summary>
/// Gestion des éléments visuels du drone :
///   - Hélice : tourne en permanence, vitesse adaptée à la vélocité du drone
///   - Petite aile : s'incline selon la direction verticale du drone (haut/bas)
///   - Aile principale : suit visuellement la trajectoire (légère inclinaison sur la vitesse horizontale)
///   - Flip : le sprite se retourne selon la direction de mouvement (gauche/droite)
/// </summary>
[RequireComponent(typeof(DroneEnemy))]
public class DroneVisuals : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Hélice")]
    [SerializeField] Transform propeller;
    [Tooltip("Vitesse de rotation de l'hélice quand le drone est immobile (degrés/sec)")]
    [SerializeField] float propellerIdleSpeed = 360f;
    [Tooltip("Vitesse de rotation de l'hélice quand le drone se déplace à pleine vitesse (degrés/sec)")]
    [SerializeField] float propellerMaxSpeed = 1800f;
    [Tooltip("Vitesse du drone qui correspond à la vitesse max d'hélice")]
    [SerializeField] float referenceMaxSpeed = 5f;

    [Header("Petite aile (donne la direction)")]
    [SerializeField] Transform smallWing;
    [Tooltip("Angle max d'inclinaison de la petite aile (degrés)")]
    [SerializeField] float smallWingMaxAngle = 35f;
    [Tooltip("Vitesse à laquelle la petite aile suit la direction verticale")]
    [SerializeField] float smallWingFollowSpeed = 8f;
    [Tooltip("Vitesse verticale à laquelle l'aile atteint son angle max")]
    [SerializeField] float verticalSpeedReference = 3f;

    [Header("Aile principale (suit la trajectoire)")]
    [SerializeField] Transform mainWing;
    [Tooltip("Angle max d'inclinaison de l'aile principale (degrés)")]
    [SerializeField] float mainWingMaxAngle = 15f;
    [Tooltip("Vitesse à laquelle l'aile principale suit la direction horizontale")]
    [SerializeField] float mainWingFollowSpeed = 5f;
    [Tooltip("Vitesse horizontale à laquelle l'aile atteint son angle max")]
    [SerializeField] float horizontalSpeedReference = 3f;

    [Header("Flip horizontal (regarde dans le sens du mouvement)")]
    [Tooltip("Active le flip du drone selon sa direction horizontale")]
    [SerializeField] bool useFlip = true;
    [Tooltip("Transform à flipper (en général le Body, ou l'objet parent qui contient le sprite). " +
             "Laisser vide = on flip ce GameObject lui-même.")]
    [SerializeField] Transform flipTarget;
    [Tooltip("Vitesse minimale pour déclencher un flip (anti-jitter quand le drone est presque immobile)")]
    [SerializeField] float flipThreshold = 0.5f;
    [Tooltip("Si le sprite original regarde à GAUCHE par défaut, cocher pour inverser le sens du flip")]
    [SerializeField] bool spriteDefaultFacesLeft = false;

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    DroneEnemy drone;
    float currentSmallWingAngle;
    float currentMainWingAngle;
    int currentFacingDirection = 1; // +1 = droite, -1 = gauche

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        drone = GetComponent<DroneEnemy>();

        // Si pas de flipTarget assigné, on flip ce GameObject (le drone entier)
        if (flipTarget == null)
            flipTarget = transform;
    }

    // ════════════════════════════════════════════════════════════
    //  Update visuel
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        Vector2 velocity = drone.CurrentVelocity;
        float speed = velocity.magnitude;

        UpdatePropeller(speed);
        UpdateSmallWing(velocity.y);
        UpdateMainWing(velocity.x);

        if (useFlip) UpdateFlip(velocity.x);
    }

    /// <summary>
    /// Hélice : tourne en permanence, plus vite quand le drone bouge.
    /// </summary>
    void UpdatePropeller(float speed)
    {
        if (propeller == null) return;

        float t = Mathf.Clamp01(speed / referenceMaxSpeed);
        float rotationSpeed = Mathf.Lerp(propellerIdleSpeed, propellerMaxSpeed, t);

        propeller.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Petite aile : s'incline selon la vitesse verticale.
    /// </summary>
    void UpdateSmallWing(float verticalVelocity)
    {
        if (smallWing == null) return;

        float t = Mathf.Clamp(verticalVelocity / verticalSpeedReference, -1f, 1f);
        float targetAngle = t * smallWingMaxAngle;

        currentSmallWingAngle = Mathf.Lerp(currentSmallWingAngle, targetAngle,
                                            smallWingFollowSpeed * Time.deltaTime);

        smallWing.localRotation = Quaternion.Euler(0f, 0f, currentSmallWingAngle);
    }

    /// <summary>
    /// Aile principale : s'incline légèrement selon la vitesse horizontale.
    /// </summary>
    void UpdateMainWing(float horizontalVelocity)
    {
        if (mainWing == null) return;

        float t = Mathf.Clamp(horizontalVelocity / horizontalSpeedReference, -1f, 1f);
        float targetAngle = -t * mainWingMaxAngle;

        currentMainWingAngle = Mathf.Lerp(currentMainWingAngle, targetAngle,
                                          mainWingFollowSpeed * Time.deltaTime);

        mainWing.localRotation = Quaternion.Euler(0f, 0f, currentMainWingAngle);
    }

    /// <summary>
    /// Flip horizontal selon la direction de mouvement.
    /// Le drone "regarde" dans le sens où il va.
    /// </summary>
    void UpdateFlip(float horizontalVelocity)
    {
        if (flipTarget == null) return;

        // Si la vitesse est trop faible, on ne change pas de direction (anti-jitter)
        if (Mathf.Abs(horizontalVelocity) < flipThreshold) return;

        // Détermine la nouvelle direction
        int newDirection = horizontalVelocity > 0 ? 1 : -1;

        // Si le sprite original regarde à gauche, on inverse
        if (spriteDefaultFacesLeft)
            newDirection = -newDirection;

        if (newDirection != currentFacingDirection)
        {
            currentFacingDirection = newDirection;

            // Applique le flip via localScale.x
            Vector3 scale = flipTarget.localScale;
            scale.x = Mathf.Abs(scale.x) * newDirection;
            flipTarget.localScale = scale;
        }
    }
}