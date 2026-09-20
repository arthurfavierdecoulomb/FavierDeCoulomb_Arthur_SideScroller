using UnityEngine;

public class DroneReactor : MonoBehaviour
{
    public enum FlameDirection { Left, Right }

    [Header("Prefab de flammes")]
    [SerializeField] GameObject flamePrefab;
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
    [SerializeField] float idleSpeedThreshold = 0.2f;

    Rigidbody2D droneRb;

    ParticleSystem flameLeft;
    ParticleSystem flameRight;

    float currentEmissionLeft;
    float currentEmissionRight;

    public float ThrustLevel => maxEmission <= 0.01f
        ? 0f
        : Mathf.Clamp01(Mathf.Max(currentEmissionLeft, currentEmissionRight) / maxEmission);

    public bool IsThrusting => ThrustLevel > 0.01f;

    public bool LeftFlameActive => currentEmissionLeft > currentEmissionRight;

    void Awake()
    {
        droneRb = GetComponent<Rigidbody2D>();

        if (flamePrefab == null)
        {
            Debug.LogError("[DroneReactor] Aucun prefab de flammes assigné !", this);
            enabled = false;
            return;
        }

        if (anchorLeft == null || anchorRight == null)
        {
            Debug.LogError("[DroneReactor] Points d'ancrage gauche/droite non assignés !", this);
            enabled = false;
            return;
        }

        flameLeft = InstantiateFlame(anchorLeft);
        flameRight = InstantiateFlame(anchorRight);

        currentEmissionLeft = 0f;
        currentEmissionRight = 0f;
    }

    ParticleSystem InstantiateFlame(Transform anchor)
    {
        GameObject flameObj = Instantiate(flamePrefab, anchor.position, anchor.rotation, anchor);
        ParticleSystem ps = flameObj.GetComponent<ParticleSystem>();

        if (ps == null)
            Debug.LogError("[DroneReactor] Le prefab de flammes n'a pas de ParticleSystem !", this);

        return ps;
    }

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
                targetLeft = activeEmission;
                OrientFlame(flameLeft, FlameDirection.Left);
            }
            else if (velocityX < 0f)
            {
                targetRight = activeEmission;
                OrientFlame(flameRight, FlameDirection.Right);
            }
        }

        currentEmissionLeft = Mathf.Lerp(currentEmissionLeft, targetLeft,
                                         intensitySmoothing * Time.deltaTime);
        currentEmissionRight = Mathf.Lerp(currentEmissionRight, targetRight,
                                          intensitySmoothing * Time.deltaTime);

        ApplyEmission(flameLeft, currentEmissionLeft);
        ApplyEmission(flameRight, currentEmissionRight);
    }

    void ApplyEmission(ParticleSystem flame, float rate)
    {
        if (flame == null) return;

        ParticleSystem.EmissionModule emission = flame.emission;
        emission.rateOverTime = rate;
    }

    void OrientFlame(ParticleSystem flame, FlameDirection wantedDirection)
    {
        float zRotation = (wantedDirection == defaultDirection) ? 0f : 180f;
        flame.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
    }
}