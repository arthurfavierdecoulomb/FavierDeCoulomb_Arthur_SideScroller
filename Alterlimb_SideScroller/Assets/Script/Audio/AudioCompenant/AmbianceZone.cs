using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbianceZone : MonoBehaviour
{
    public enum ZoneShape { Circle, Box }

    [Header("Son")]
    [SerializeField] AudioClip loopClip;
    [Range(0f, 1f)]
    [SerializeField] float maxVolume = 1f;
    [SerializeField] bool randomStartOffset = true;

    [Header("Forme de la zone")]
    [SerializeField] ZoneShape shape = ZoneShape.Circle;
    [SerializeField] Vector2 centerOffset = Vector2.zero;
    [SerializeField] float innerRadius = 4f;
    [SerializeField] float outerRadius = 12f;
    [SerializeField] Vector2 boxSize = new Vector2(12f, 6f);
    [SerializeField] float boxFalloff = 6f;

    [Header("Fondu")]
    [SerializeField] AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] float volumeSmoothing = 4f;

    [Header("Cible")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] float updateInterval = 0.05f;

    [Header("Economie")]
    [SerializeField] bool pauseWhenSilent = true;
    [SerializeField] bool muteWhilePaused = false;

    [Header("Debug")]
    [SerializeField] bool drawGizmos = true;

    AudioSource source;
    Transform playerTransform;
    float updateTimer;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = loopClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    void Start()
    {
        if (loopClip == null)
            Debug.LogError($"[AmbienceZone] '{name}' n'a pas de Loop Clip assigné.", this);

        if (source.outputAudioMixerGroup == null)
            Debug.LogError($"[AmbienceZone] '{name}' : le champ Output de l'AudioSource est vide, il devrait pointer sur le groupe Ambience.", this);

        if (outerRadius <= innerRadius)
            Debug.LogError($"[AmbienceZone] '{name}' : Outer Radius doit être plus grand que Inner Radius, sinon le fondu est instantané.", this);

        AcquirePlayer();

        if (loopClip != null)
        {
            source.Play();
            if (randomStartOffset) source.time = Random.Range(0f, loopClip.length);
            source.Pause();
        }
    }

    void Update()
    {
        updateTimer -= Time.unscaledDeltaTime;
        if (updateTimer > 0f) return;
        updateTimer = updateInterval;

        if (playerTransform == null)
        {
            AcquirePlayer();
            if (playerTransform == null) return;
        }

        float target = muteWhilePaused && Time.timeScale <= 0f ? 0f : ComputeTargetVolume();

        source.volume = Mathf.Lerp(source.volume, target, volumeSmoothing * Time.unscaledDeltaTime);

        if (!pauseWhenSilent) return;

        bool audible = source.volume > 0.001f || target > 0.001f;

        if (audible && !source.isPlaying) source.UnPause();
        else if (!audible && source.isPlaying) source.Pause();
    }

    float ComputeTargetVolume()
    {
        Vector2 center = (Vector2)transform.position + centerOffset;
        Vector2 playerPosition = playerTransform.position;

        float proximity;

        if (shape == ZoneShape.Circle)
        {
            float distance = Vector2.Distance(playerPosition, center);
            proximity = Mathf.InverseLerp(outerRadius, innerRadius, distance);
        }
        else
        {
            Vector2 delta = new Vector2(
                Mathf.Abs(playerPosition.x - center.x) - boxSize.x * 0.5f,
                Mathf.Abs(playerPosition.y - center.y) - boxSize.y * 0.5f);

            float distance = new Vector2(Mathf.Max(delta.x, 0f), Mathf.Max(delta.y, 0f)).magnitude;
            proximity = Mathf.InverseLerp(boxFalloff, 0f, distance);
        }

        return falloffCurve.Evaluate(Mathf.Clamp01(proximity)) * maxVolume;
    }

    void AcquirePlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) playerTransform = p.transform;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 center = transform.position + (Vector3)centerOffset;

        if (shape == ZoneShape.Circle)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireSphere(center, innerRadius);

            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(center, outerRadius);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(boxSize.x, boxSize.y, 0f));

            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireCube(center, new Vector3(boxSize.x + boxFalloff * 2f, boxSize.y + boxFalloff * 2f, 0f));
        }
    }
}