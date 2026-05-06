using UnityEngine;

/// <summary>
/// Zone de sable mouvant / boue épaisse.
/// 
/// Comportement :
///   - Le joueur entre dans la zone → ses mouvements sont drastiquement ralentis
///   - Une force constante l'aspire vers le bas (effet "succion")
///   - S'il essaie de SAUTER ou DASH → mort instantanée (mécanique punitive)
///   - Pour s'en sortir : avancer lentement vers les côtés et sortir par le bord
/// 
/// À placer sur un GameObject avec un Collider2D en mode TRIGGER
/// (typiquement un Tilemap avec Composite Collider 2D en Trigger).
/// 
/// Le script délègue la détection saut/dash au CharaController via la méthode
/// SetInQuicksand() pour garder une seule source de vérité sur l'état du joueur.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class QuicksandZone : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Configuration
    // ════════════════════════════════════════════════════════════

    [Header("Mouvement dans le sable")]
    [Tooltip("Vitesse horizontale max dans le sable (en unités/sec). Très basse = très dur de sortir.")]
    [SerializeField] float maxHorizontalSpeed = 1.5f;

    [Tooltip("Vitesse verticale max d'enfoncement (limite de la chute libre)")]
    [SerializeField] float maxSinkSpeed = 1.2f;

    [Tooltip("Force constante d'aspiration vers le bas (en unités/sec²)")]
    [SerializeField] float suctionForce = 3f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";

    // ════════════════════════════════════════════════════════════
    //  État runtime
    // ════════════════════════════════════════════════════════════

    Rigidbody2D playerRb;
    CharaController playerController;
    bool playerInside;

    // ════════════════════════════════════════════════════════════
    //  Détection entrée/sortie
    // ════════════════════════════════════════════════════════════

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerRb = other.GetComponent<Rigidbody2D>();
        playerController = other.GetComponent<CharaController>();
        if (playerRb == null || playerController == null) return;

        playerInside = true;
        playerController.SetInQuicksand(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (playerController == null) return;

        playerInside = false;
        playerController.SetInQuicksand(false);

        playerRb = null;
        playerController = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Application de la physique de sable
    // ════════════════════════════════════════════════════════════

    void FixedUpdate()
    {
        if (!playerInside || playerRb == null) return;

        Vector2 vel = playerRb.linearVelocity;

        // 1. Limite la vitesse horizontale (très dur de bouger)
        vel.x = Mathf.Clamp(vel.x, -maxHorizontalSpeed, maxHorizontalSpeed);

        // 2. Applique la force d'aspiration vers le bas
        vel.y -= suctionForce * Time.fixedDeltaTime;

        // 3. Limite la vitesse de chute (le joueur ne tombe pas en chute libre)
        vel.y = Mathf.Max(vel.y, -maxSinkSpeed);

        playerRb.linearVelocity = vel;
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Couleur sable/boue (marron jaunâtre transparent)
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.7f, 0.5f, 0.2f, 0.3f);
        Bounds b = col.bounds;
        Gizmos.DrawCube(b.center, b.size);

        Gizmos.color = new Color(0.7f, 0.5f, 0.2f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}