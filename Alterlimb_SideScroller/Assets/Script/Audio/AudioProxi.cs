using UnityEngine;

public class AudioProxi : MonoBehaviour
{
    [Header("Portee")]
    [SerializeField] Vector2 centerOffset = Vector2.zero;
    [SerializeField] float innerRadius = 6f;
    [SerializeField] float outerRadius = 20f;
    [SerializeField] AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0f, 1f)]
    [SerializeField] float minAttenuation = 0f;

    [Header("Cible")]
    [SerializeField] string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] bool drawGizmos = true;

    static Transform cachedPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cachedPlayer = null;
    }

    void Start()
    {
        if (outerRadius <= innerRadius)
            Debug.LogError($"[AudioProximity] '{name}' : Outer Radius doit être plus grand que Inner Radius, sinon l'atténuation est brutale.", this);
    }

    public float GetAttenuation()
    {
        if (cachedPlayer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) cachedPlayer = p.transform;
        }

        if (cachedPlayer == null) return 1f;

        Vector2 center = (Vector2)transform.position + centerOffset;
        Vector2 listener = cachedPlayer.position;

        float distance = Vector2.Distance(listener, center);
        float proximity = Mathf.Clamp01(Mathf.InverseLerp(outerRadius, innerRadius, distance));

        return Mathf.Max(falloffCurve.Evaluate(proximity), minAttenuation);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 center = transform.position + (Vector3)centerOffset;

        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(center, innerRadius);

        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(center, outerRadius);
    }
}