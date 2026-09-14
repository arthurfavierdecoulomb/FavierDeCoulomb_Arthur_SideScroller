using UnityEngine;

[RequireComponent(typeof(SfxEmitter))]
public class LiquidSplash : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] AudioClip[] splashClips;
    [SerializeField] AudioClip[] heavySplashClips;
    [SerializeField] float heavySpeedThreshold = 14f;

    [Header("Intensite")]
    [SerializeField] float minSpeedForFullVolume = 4f;
    [SerializeField] float maxSpeedForFullVolume = 18f;
    [Range(0f, 1f)]
    [SerializeField] float minVolumeScale = 0.45f;

    [Header("Detection")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] float retriggerCooldown = 0.5f;

    SfxEmitter sfx;
    float cooldownTimer;

    void Awake()
    {
        sfx = GetComponent<SfxEmitter>();
    }

    void Start()
    {
        if (splashClips == null || splashClips.Length == 0)
            Debug.LogError($"[LiquidSplash] '{name}' n'a aucun Splash Clip assigné.", this);
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (cooldownTimer > 0f) return;
        if (!other.CompareTag(playerTag)) return;

        cooldownTimer = retriggerCooldown;

        float impactSpeed = 0f;
        Rigidbody2D otherRb = other.attachedRigidbody;
        if (otherRb != null) impactSpeed = Mathf.Abs(otherRb.linearVelocity.y);

        float scale = Mathf.Lerp(
            minVolumeScale, 1f,
            Mathf.InverseLerp(minSpeedForFullVolume, maxSpeedForFullVolume, impactSpeed));

        bool heavy = impactSpeed >= heavySpeedThreshold
                  && heavySplashClips != null
                  && heavySplashClips.Length > 0;

        sfx.Play(heavy ? heavySplashClips : splashClips, scale);
    }
}