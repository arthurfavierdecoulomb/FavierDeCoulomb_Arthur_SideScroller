using UnityEngine;

/// <summary>
/// Gère les flammes de réacteur du drone (deux propulseurs gauche/droite).
/// 
/// Comportement :
///   - UNE SEULE flamme active à la fois : celle opposée au déplacement
///     (drone va à gauche → réacteur DROIT actif ; drone va à droite → GAUCHE).
///   - La flamme active est ORIENTÉE pour que ses particules partent dans le
///     bon sens (drone va à gauche → particules vers la droite, et inversement).
///   - L'intensité varie selon la vitesse du drone.
///   - Drone immobile → les deux flammes éteintes.
/// 
/// Setup :
///   - Deux GameObjects vides enfants du drone (ancrages gauche/droite).
///   - Un prefab de Particle System (les flammes) assigné dans l'Inspector.
///   - Indiquer le sens d'émission par défaut du prefab via 'defaultDirection'.
/// </summary>
public class DroneReactor : MonoBehaviour
{
    /// <summary>Sens dans lequel le prefab de flammes crache quand sa rotation Z = 0.</summary>
    public enum FlameDirection { Left, Right }

    [Header("Prefab de flammes")]
    [Tooltip("Le prefab du Particle System des flammes de réacteur")]
    [SerializeField] GameObject flamePrefab;
    [Tooltip("Sens d'émission du prefab quand sa rotation Z = 0 (vérifié dans l'éditeur)")]
    [SerializeField] FlameDirection defaultDirection = FlameDirection.Right;

    [Header("Points d'ancrage (empties enfants du drone)")]
    [SerializeField] Transform anchorLeft;
    [SerializeField] Transform anchorRight;

    [Header("Intensité selon la vitesse")]
    [SerializeField] float minEmission = 8f;
    [SerializeField] float maxEmission = 40f;
    [SerializeField] float speedForMaxEmission = 4f;
    [SerializeField] float intensitySmoothing = 8f;

    [Header("Seuil d'immobilité")]
    [Tooltip("En-dessous de cette vitesse, le drone est immobile (flammes éteintes)")]
    [SerializeField] float idleSpeedThreshold = 0.2f;

    // ── Runtime ──
    Rigidbody2D droneRb;

    ParticleSystem flameLeft;
    ParticleSystem flameRight;
    ParticleSystem.EmissionModule emissionLeft;
    ParticleSystem.EmissionModule emissionRight;

    float currentEmissionLeft;
    float currentEmissionRight;

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        droneRb = GetComponent<Rigidbody2D>();

        if (flamePrefab == null)
        {
            Debug.LogError("[DroneReactor] Aucun prefab de flammes assigné !");
            enabled = false;
            return;
        }

        if (anchorLeft == null || anchorRight == null)
        {
            Debug.LogError("[DroneReactor] Points d'ancrage gauche/droite non assignés !");
            enabled = false;
            return;
        }

        flameLeft = InstantiateFlame(anchorLeft);
        flameRight = InstantiateFlame(anchorRight);

        emissionLeft = flameLeft.emission;
        emissionRight = flameRight.emission;

        currentEmissionLeft = 0f;
        currentEmissionRight = 0f;
    }

    ParticleSystem InstantiateFlame(Transform anchor)
    {
        GameObject flameObj = Instantiate(flamePrefab, anchor.position, anchor.rotation, anchor);
        ParticleSystem ps = flameObj.GetComponent<ParticleSystem>();

        if (ps == null)
            Debug.LogError("[DroneReactor] Le prefab de flammes n'a pas de ParticleSystem !");

        return ps;
    }

    // ════════════════════════════════════════════════════════════
    //  Update : flamme active + intensité + orientation
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (flameLeft == null || flameRight == null) return;

        float velocityX = droneRb != null ? droneRb.linearVelocity.x : 0f;
        float speed = droneRb != null ? droneRb.linearVelocity.magnitude : 0f;

        float targetLeft = 0f;
        float targetRight = 0f;

        if (speed >= idleSpeedThreshold)
        {
            float speedRatio = Mathf.Clamp01(speed / speedForMaxEmission);
            float activeEmission = Mathf.Lerp(minEmission, maxEmission, speedRatio);

            if (velocityX > 0f)
            {
                // Drone va à DROITE → réacteur GAUCHE actif, particules vers la GAUCHE
                targetLeft = activeEmission;
                OrientFlame(flameLeft, FlameDirection.Left);
            }
            else if (velocityX < 0f)
            {
                // Drone va à GAUCHE → réacteur DROIT actif, particules vers la DROITE
                targetRight = activeEmission;
                OrientFlame(flameRight, FlameDirection.Right);
            }
        }

        // Transition douce
        currentEmissionLeft = Mathf.Lerp(currentEmissionLeft, targetLeft,
                                         intensitySmoothing * Time.deltaTime);
        currentEmissionRight = Mathf.Lerp(currentEmissionRight, targetRight,
                                          intensitySmoothing * Time.deltaTime);

        emissionLeft.rateOverTime = currentEmissionLeft;
        emissionRight.rateOverTime = currentEmissionRight;
    }

    /// <summary>
    /// Oriente une flamme pour que ses particules partent dans le sens voulu.
    /// Applique une rotation Z de 0° ou 180° selon le sens par défaut du prefab.
    /// </summary>
    void OrientFlame(ParticleSystem flame, FlameDirection wantedDirection)
    {
        // Si le sens voulu correspond au sens par défaut → rotation 0
        // Sinon → rotation 180° pour inverser
        float zRotation = (wantedDirection == defaultDirection) ? 0f : 180f;
        flame.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
    }
}