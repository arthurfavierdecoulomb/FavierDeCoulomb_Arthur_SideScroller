using UnityEngine;

/// <summary>
/// Zone de zoom de caméra : un collider trigger placé dans le niveau qui
/// demande à la CameraFollow de zoomer/dézoomer quand le joueur entre,
/// et de revenir au zoom par défaut quand il sort.
/// 
/// Setup :
///   - Un GameObject avec un Collider2D en mode Trigger (BoxCollider2D par ex).
///   - Ce script dessus.
///   - Place et redimensionne le collider pour couvrir la zone du niveau
///     où tu veux le zoom personnalisé.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraZoomZone : MonoBehaviour
{
    [Header("Zoom souhaité dans cette zone")]
    [Tooltip("Orthographic size de la caméra quand le joueur est dans cette zone. " +
             "Plus petit = plus zoomé, plus grand = plus dézoomé.")]
    [SerializeField] float zoomValue = 4f;

    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";

    [Header("Référence (optionnel)")]
    [Tooltip("La CameraFollow à piloter. Si vide, on la trouve via Camera.main.")]
    [SerializeField] CameraFollow cameraFollow;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (cameraFollow == null) return;

        cameraFollow.SetTargetZoom(zoomValue);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (cameraFollow == null) return;

        cameraFollow.ResetZoom();
    }

    // ── Gizmos : visualiser la zone et le zoom dans l'éditeur ──
    void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        // Couleur selon que c'est un zoom in ou zoom out
        // (par rapport à un zoom "moyen" arbitraire de 5)
        Gizmos.color = zoomValue < 5f
            ? new Color(0.3f, 0.8f, 1f, 0.25f)   // bleu = zoom in
            : new Color(1f, 0.6f, 0.2f, 0.25f);  // orange = zoom out

        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}