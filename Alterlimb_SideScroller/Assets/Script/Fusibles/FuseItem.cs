using UnityEngine;

/// <summary>
/// Un fusible ramassable, dispersé dans la map.
/// Calqué sur AbilityPickup : trigger, détection du joueur, notification, disparition.
/// 
/// Au contact du joueur :
///   - Notifie le FuseManager (incrémente le compteur de fusibles en main)
///   - L'objet disparaît
/// 
/// Setup :
///   - GameObject avec un SpriteRenderer (le visuel du fusible)
///   - Un Collider2D en mode Trigger
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FuseItem : MonoBehaviour
{
    [Header("Détection")]
    [SerializeField] string playerTag = "Player";

    void Reset()
    {
        // Force le collider en Trigger dès l'ajout du composant
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (FuseManager.Instance == null)
        {
            Debug.LogError("[FuseItem] FuseManager.Instance introuvable dans la scène !");
            return;
        }

        FuseManager.Instance.CollectFuse();
        Debug.Log("Item ramassé : Fusible");

        
        Destroy(gameObject);
    }
}