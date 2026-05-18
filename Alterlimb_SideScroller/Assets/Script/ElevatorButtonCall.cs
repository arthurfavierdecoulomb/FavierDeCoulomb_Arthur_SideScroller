using UnityEngine;

/// <summary>
/// Bouton d'appel d'ascenseur, placé à un étage donné.
/// 
/// Quand le joueur est à proximité et fait un clic droit, le bouton
/// appelle l'ascenseur à SON étage (floorIndex).
/// 
/// Calqué sur le mode Proximity de la Door : détection par distance + clic droit.
/// 
/// Setup :
///   - Place un bouton à chaque étage.
///   - Assigne l'ascenseur cible et le numéro d'étage de CE bouton.
/// </summary>
public class ElevatorCallButton : MonoBehaviour
{
    [Header("Ascenseur cible")]
    [Tooltip("L'ascenseur que ce bouton commande")]
    [SerializeField] ElevatorPlatform elevator;
    [Tooltip("Index de l'étage où se trouve CE bouton (0 = étage le plus bas, comme dans 'floors')")]
    [SerializeField] int floorIndex = 0;

    [Header("Interaction")]
    [Tooltip("Distance maximale pour que le joueur puisse appuyer sur le bouton")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1; // clic droit
    [SerializeField] string playerTag = "Player";

    [Header("Feedback (optionnel)")]
    [Tooltip("Animator du bouton — déclenche un 'press' visuel (paramètre Trigger 'Press')")]
    [SerializeField] Animator buttonAnimator;

    Transform player;

    static readonly int PressTrigger = Animator.StringToHash("Press");

    void Awake()
    {
        if (elevator == null)
            Debug.LogWarning($"[ElevatorCallButton] '{name}' n'a pas d'ascenseur assigné.", this);

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null || elevator == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            CallElevator();
        }
    }

    void CallElevator()
    {
        elevator.CallToFloor(floorIndex);

        if (buttonAnimator != null)
            buttonAnimator.SetTrigger(PressTrigger);

        Debug.Log($"[ElevatorCallButton] Ascenseur appelé à l'étage {floorIndex}.");
    }

    // ── Gizmo : zone d'interaction ──
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (elevator != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, elevator.transform.position);
        }
    }
}