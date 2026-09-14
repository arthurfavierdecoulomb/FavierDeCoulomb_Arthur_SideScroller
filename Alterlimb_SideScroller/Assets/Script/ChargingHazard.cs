using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class ChargingHazard : MonoBehaviour
{
    enum HazardState { Idle, Warning, Charging, Paused, Returning, Cooldown }

    public enum ChargeDirection { Right, Left, Up, Down }

    [Header("Zone d'activation")]
    [SerializeField] Vector2 activationZoneOffset = Vector2.zero;
    [SerializeField] Vector2 activationZoneSize = new Vector2(4f, 4f);
    [SerializeField] LayerMask playerLayer;

    [Header("Charge")]
    [SerializeField] ChargeDirection chargeDirection = ChargeDirection.Right;
    [SerializeField] float chargeDistance = 6f;
    [SerializeField] float chargeSpeed = 20f;

    [Header("Avertissement")]
    [SerializeField] float warningDuration = 0.3f;

    [Header("Pause")]
    [SerializeField] float pauseDuration = 1.5f;

    [Header("Retour")]
    [SerializeField] float returnSpeed = 4f;

    [Header("Cooldown")]
    [SerializeField] float cooldownDuration = 0.5f;

    [Header("Tag joueur (pour Die)")]
    [SerializeField] string playerTag = "Player";

    [Header("Audio")]
    [SerializeField] SfxEmitter sfx;
    [SerializeField] AudioClip[] warningClips;
    [SerializeField] AudioClip[] releaseClips;
    [SerializeField] AudioClip[] crushClips;
    [SerializeField] AudioClip[] impactClips;

    Rigidbody2D rb;
    Vector2 startPosition;
    Vector2 chargeTargetPosition;
    HazardState state = HazardState.Idle;
    Coroutine cycleCoroutine;
    bool hasCrushedThisCharge;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        startPosition = rb.position;
        chargeTargetPosition = startPosition + GetDirectionVector() * chargeDistance;

        if (sfx == null) sfx = GetComponent<SfxEmitter>();

        if (sfx == null && HasAnyClip())
            Debug.LogError($"[ChargingHazard] '{name}' a des clips assignés mais aucun SfxEmitter : aucun son ne sera joué.", this);
    }

    Vector2 GetDirectionVector()
    {
        switch (chargeDirection)
        {
            case ChargeDirection.Right: return Vector2.right;
            case ChargeDirection.Left: return Vector2.left;
            case ChargeDirection.Up: return Vector2.up;
            case ChargeDirection.Down: return Vector2.down;
            default: return Vector2.right;
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
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        state = HazardState.Idle;
        hasCrushedThisCharge = false;
    }

    void FixedUpdate()
    {
        if (state != HazardState.Idle) return;

        Vector2 worldCenter = (Vector2)transform.position + activationZoneOffset;
        Collider2D hit = Physics2D.OverlapBox(worldCenter, activationZoneSize, 0f, playerLayer);

        if (hit != null)
        {
            cycleCoroutine = StartCoroutine(ChargeCycle());
        }
    }

    IEnumerator ChargeCycle()
    {
        hasCrushedThisCharge = false;

        if (warningDuration > 0f)
        {
            state = HazardState.Warning;
            PlayClips(warningClips);
            yield return new WaitForSeconds(warningDuration);
        }

        state = HazardState.Charging;
        PlayClips(releaseClips);

        Vector2 direction = GetDirectionVector();

        while (true)
        {
            float remaining = Vector2.Distance(rb.position, chargeTargetPosition);
            float step = chargeSpeed * Time.fixedDeltaTime;

            if (remaining <= step)
            {
                rb.MovePosition(chargeTargetPosition);
                break;
            }

            Vector2 newPos = rb.position + direction * step;
            rb.MovePosition(newPos);

            yield return new WaitForFixedUpdate();
        }

        state = HazardState.Paused;
        PlayClips(impactClips);
        yield return new WaitForSeconds(pauseDuration);

        state = HazardState.Returning;
        Vector2 returnDirection = -direction;

        while (true)
        {
            float remaining = Vector2.Distance(rb.position, startPosition);
            float step = returnSpeed * Time.fixedDeltaTime;

            if (remaining <= step)
            {
                rb.MovePosition(startPosition);
                break;
            }

            Vector2 newPos = rb.position + returnDirection * step;
            rb.MovePosition(newPos);

            yield return new WaitForFixedUpdate();
        }

        state = HazardState.Cooldown;
        yield return new WaitForSeconds(cooldownDuration);

        state = HazardState.Idle;
        cycleCoroutine = null;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryKillPlayer(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryKillPlayer(collision.collider);
    }

    void TryKillPlayer(Collider2D other)
    {
        if (state != HazardState.Charging) return;
        if (!other.CompareTag(playerTag)) return;

        CharaController chara = other.GetComponent<CharaController>();
        if (chara == null) return;

        if (!hasCrushedThisCharge)
        {
            hasCrushedThisCharge = true;
            PlayClips(crushClips);
        }

        chara.Die();
    }

    void PlayClips(AudioClip[] clips)
    {
        if (sfx != null) sfx.Play(clips);
    }

    bool HasAnyClip()
    {
        return (warningClips != null && warningClips.Length > 0)
            || (releaseClips != null && releaseClips.Length > 0)
            || (crushClips != null && crushClips.Length > 0)
            || (impactClips != null && impactClips.Length > 0);
    }

    void OnDrawGizmos()
    {
        Vector3 origin = Application.isPlaying ? (Vector3)startPosition : transform.position;

        Vector3 zoneCenter = transform.position + (Vector3)activationZoneOffset;

        if (!Application.isPlaying || state == HazardState.Idle)
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        else if (state == HazardState.Warning)
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        else
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.4f);

        Gizmos.DrawCube(zoneCenter, activationZoneSize);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawWireCube(zoneCenter, activationZoneSize);

        Vector3 target = origin + (Vector3)GetDirectionVector() * chargeDistance;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, target);
        Gizmos.DrawWireSphere(target, 0.25f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.20f);
    }
}