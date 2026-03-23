using UnityEngine;
using System.Collections.Generic;

public class HazardManager : MonoBehaviour
{
    [System.Serializable]
    public class Hazard
    {
        [Header("Objet")]
        public GameObject target;           // l'objet piège

        [Header("Type de mouvement")]
        public MovementType movementType = MovementType.UpDown;

        [Header("Paramètres")]
        public float speed = 3f;
        public float amplitude = 2f;
        public float phase = 0f;            // décalage 0-1 pour désynchroniser

        [Header("Rotation")]
        public float rotationSpeed = 90f;

        [Header("Orbite")]
        public Transform orbitCenter;
        public float orbitRadius = 3f;

        [Header("Pendule")]
        public float pendulumMaxAngle = 45f;

        // ── Données runtime (cachées dans l'inspecteur) ──
        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public Quaternion startRotation;
        [HideInInspector] public float timer;
        [HideInInspector] public float orbitAngle;
    }

    public enum MovementType
    {
        UpDown,
        LeftRight,
        Rotation,
        CircularOrbit,
        PingPongDiag,
        Pendulum
    }

    [Header("Liste des pièges")]
    [SerializeField] List<Hazard> hazards = new List<Hazard>();

    void Awake()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;
            h.startPosition = h.target.transform.position;
            h.startRotation = h.target.transform.rotation;
            h.timer = h.phase * Mathf.PI * 2f;
            h.orbitAngle = 0f;
        }
    }

    void Update()
    {
        foreach (Hazard h in hazards)
        {
            if (h.target == null) continue;
            h.timer += Time.deltaTime * h.speed;
            ProcessHazard(h);
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

    void OnDrawGizmosSelected()
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
            }
        }
    }
}