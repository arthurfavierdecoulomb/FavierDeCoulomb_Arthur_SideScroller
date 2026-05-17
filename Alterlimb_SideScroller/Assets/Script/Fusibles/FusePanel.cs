using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Panneau à fusibles : le joueur y insère les fusibles qu'il a ramassés.
/// 
/// Quand le joueur entre dans la zone, ses fusibles s'insèrent un par un
/// (effet visuel via délai). Le compteur est mis à jour.
/// 
/// L'ouverture de la porte n'est PAS gérée ici : la Door en mode "Fuses"
/// s'abonne à FuseManager.OnAllFusesInstalledStatic de son côté.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FusePanel : MonoBehaviour
{
    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";

    [Header("Visuels des fusibles installés")]
    [Tooltip("Les 5 GameObjects des fusibles dans le panneau (désactivés au départ). Ordre = ordre d'apparition.")]
    [SerializeField] GameObject[] fuseSlots;

    [Header("Animation d'insertion")]
    [Tooltip("Délai entre l'insertion de deux fusibles consécutifs (secondes)")]
    [SerializeField] float delayBetweenInserts = 0.4f;

    [Header("Compteur")]
    [SerializeField] TextMeshProUGUI counterText;

    bool isInserting;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        foreach (GameObject slot in fuseSlots)
        {
            if (slot != null) slot.SetActive(false);
        }
        UpdateCounter();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        TryStartInsertion();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        TryStartInsertion();
    }

    void TryStartInsertion()
    {
        if (isInserting) return;
        if (FuseManager.Instance == null) return;
        if (FuseManager.Instance.FusesCarried <= 0) return;

        StartCoroutine(InsertFusesRoutine());
    }

    IEnumerator InsertFusesRoutine()
    {
        isInserting = true;

        while (FuseManager.Instance.FusesCarried > 0 && !FuseManager.Instance.AllFusesInstalled)
        {
            bool installed = FuseManager.Instance.TryInstallOneFuse();
            if (!installed) break;

            int slotIndex = FuseManager.Instance.FusesInstalled - 1;
            if (slotIndex >= 0 && slotIndex < fuseSlots.Length && fuseSlots[slotIndex] != null)
            {
                fuseSlots[slotIndex].SetActive(true);
            }

            UpdateCounter();

            // (Optionnel : son d'insertion ici)

            yield return new WaitForSeconds(delayBetweenInserts);
        }

        isInserting = false;
    }

    void UpdateCounter()
    {
        if (counterText == null) return;
        if (FuseManager.Instance == null) return;

        int installed = FuseManager.Instance.FusesInstalled;
        int total = FuseManager.Instance.TotalFuses;
        counterText.text = $"{installed:00}/{total:00}";
    }
}