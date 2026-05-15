using UnityEngine;
using System.Collections;

/// <summary>
/// Piège qui charge dans une direction quand le joueur entre dans une zone d'activation.
/// 
/// Cycle de vie :
///   1. Idle     : le piège attend que le joueur entre dans la zone
///   2. Charging : le joueur est entré → le piège fonce dans la direction configurée
///   3. Paused   : arrivé au bout, il s'arrête un moment (le joueur peut passer)
///   4. Returning: il retourne lentement à sa position initiale
///   5. Cooldown : courte période sans redéclenchement possible, puis Idle
/// 
/// Mort du joueur :
///   - Pendant la phase Charging uniquement, tout contact avec le joueur tue celui-ci.
///   - Pendant les autres phases, le piège est inoffensif.
/// 
/// Reset au respawn via SpawnManager.OnPlayerRespawn.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ChargingHazard : MonoBehaviour
{
    enum HazardState { Idle, Charging, Paused, Returning, Cooldown }

    public enum ChargeDirection { Right, Left, Up, Down }

    [Header("Zone d'activation")]
    [Tooltip("Position de la zone d'activation, relative au piège")]
    [SerializeField] Vector2 activationZoneOffset = Vector2.zero;
    [Tooltip("Taille de la zone d'activation (largeur × hauteur)")]
    [SerializeField] Vector2 activationZoneSize = new Vector2(4f, 4f);
    [Tooltip("Layer du joueur (utilisé pour la détection de zone)")]
    [SerializeField] LayerMask playerLayer;

    [Header("Charge")]
    [SerializeField] ChargeDirection chargeDirection = ChargeDirection.Right;
    [Tooltip("Distance totale parcourue pendant la charge (en unités Unity)")]
    [SerializeField] float chargeDistance = 6f;
    [Tooltip("Vitesse pendant la charge (unités/seconde) — rapide")]
    [SerializeField] float chargeSpeed = 20f;

    [Header("Pause")]
    [SerializeField] float pauseDuration = 1.5f;

    [Header("Retour")]
    [SerializeField] float returnSpeed = 4f;

    [Header("Cooldown")]
    [SerializeField] float cooldownDuration = 0.5f;

    [Header("Tag joueur (pour Die)")]
    [SerializeField] string playerTag = "Player";

    Rigidbody2D rb;
    Vector2 startPosition;
    Vector2 chargeTargetPosition;
    HazardState state = HazardState.Idle;
    Coroutine cycleCoroutine;

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

        startPosition = rb.position;
        chargeTargetPosition = startPosition + GetDirectionVector() * chargeDistance;
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
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        state = HazardState.Idle;
    }

    // ════════════════════════════════════════════════════════════
    //  Détection de la zone d'activation
    // ════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════
    //  Cycle complet : Charge → Pause → Retour → Cooldown
    //  CHANGEMENT MAJEUR : on utilise MovePosition au lieu de linearVelocity
    // ════════════════════════════════════════════════════════════

    IEnumerator ChargeCycle()
    {
        // ── Phase 1 : Charge rapide ─────────────────────────────
        state = HazardState.Charging;
        Vector2 direction = GetDirectionVector();

        while (true)
        {
            float remaining = Vector2.Distance(rb.position, chargeTargetPosition);
            float step = chargeSpeed * Time.fixedDeltaTime;

            // Si on va dépasser ou atteindre cette frame, on snap
            if (remaining <= step)
            {
                rb.MovePosition(chargeTargetPosition);
                break;
            }

            // Sinon on avance d'un pas
            Vector2 newPos = rb.position + direction * step;
            rb.MovePosition(newPos);

            yield return new WaitForFixedUpdate();
        }

        // ── Phase 2 : Pause ─────────────────────────────────────
        state = HazardState.Paused;
        yield return new WaitForSeconds(pauseDuration);

        // ── Phase 3 : Retour lent ───────────────────────────────
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

        // ── Phase 4 : Cooldown ──────────────────────────────────
        state = HazardState.Cooldown;
        yield return new WaitForSeconds(cooldownDuration);

        state = HazardState.Idle;
        cycleCoroutine = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Collision : tue le joueur pendant la charge
    // ════════════════════════════════════════════════════════════

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
        if (chara != null) chara.Die();
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        Vector3 origin = Application.isPlaying ? (Vector3)startPosition : transform.position;

        // Zone d'activation
        Vector3 zoneCenter = transform.position + (Vector3)activationZoneOffset;
        Gizmos.color = (state == HazardState.Idle || !Application.isPlaying)
            ? new Color(0.2f, 1f, 0.2f, 0.4f)
            : new Color(1f, 1f, 0.2f, 0.4f);
        Gizmos.DrawCube(zoneCenter, activationZoneSize);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawWireCube(zoneCenter, activationZoneSize);

        // Trajectoire de charge
        Vector3 target = origin + (Vector3)GetDirectionVector() * chargeDistance;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, target);
        Gizmos.DrawWireSphere(target, 0.25f);

        // Point de départ
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.20f);
    }
}