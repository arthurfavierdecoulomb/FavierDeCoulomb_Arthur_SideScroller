using UnityEngine;

/// <summary>
/// Zone de déclenchement d'une transition vers un autre niveau.
/// 
/// Setup dans Unity :
///   - Créer un GameObject vide avec un BoxCollider2D en Trigger
///   - Assigner le LevelData cible (ex: LD_Metro pour la zone en fin d'Usine)
///   - Optionnel : ajuster autoRunDirection si la transition se fait vers la gauche
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TransitionZone : MonoBehaviour
{
    [Header("Niveau cible")]
    [Tooltip("Données du niveau vers lequel transitionner")]
    [SerializeField] LevelData nextLevel;

    [Header("Course automatique d'entrée")]
    [Tooltip("Direction de course quand le joueur entre dans la zone (+1 = droite, -1 = gauche)")]
    [SerializeField] float autoRunDirection = 1f;

    [Tooltip("Distance horizontale parcourue en course auto avant le flicker (X depuis la position d'entrée)")]
    [SerializeField] float runDistanceBeforeFlicker = 6f;

    [Header("Tag joueur")]
    [SerializeField] string playerTag = "Player";

    bool triggered;

    void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag(playerTag)) return;
        if (nextLevel == null)
        {
            Debug.LogError($"[TransitionZone] Aucun LevelData assigné sur '{gameObject.name}' !");
            return;
        }
        if (LevelTransitionManager.Instance == null)
        {
            Debug.LogError("[TransitionZone] LevelTransitionManager.Instance introuvable dans la scène !");
            return;
        }

        triggered = true;
        LevelTransitionManager.Instance.StartTransition(
            nextLevel,
            autoRunDirection,
            runDistanceBeforeFlicker
        );
    }

    // ── Gizmos ──────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.right * autoRunDirection * 1.5f;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.15f);
    }
}