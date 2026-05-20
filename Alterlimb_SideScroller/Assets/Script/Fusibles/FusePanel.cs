using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Panneau à fusibles : le joueur y insère les fusibles qu'il a ramassés.
/// 
/// Quand le joueur entre dans la zone, ses fusibles s'insèrent un par un
/// (effet visuel via délai). Le compteur est mis à jour.
/// 
/// VISUEL : le panneau a un SpriteRenderer dont l'image change à chaque
/// fusible installé. On fournit une LISTE DE SPRITES dans l'Inspector,
/// un par état (0 fusible, 1 fusible, ... 5 fusibles). Le code pioche
/// le bon sprite selon FuseManager.FusesInstalled. Pas de GameObjects
/// à activer, pas d'Animator à monter.
/// 
/// L'ouverture de la porte n'est PAS gérée ici : la Door en mode "Fuses"
/// s'abonne à FuseManager.OnAllFusesInstalledStatic de son côté.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FusePanel : MonoBehaviour
{
    [Header("Détection joueur")]
    [SerializeField] string playerTag = "Player";

    [Header("Visuel du panneau")]
    [Tooltip("Le SpriteRenderer dont on change le sprite. Si vide, on prend celui de ce GameObject.")]
    [SerializeField] SpriteRenderer panelRenderer;
    [Tooltip("Liste des sprites du panneau, indexés par nombre de fusibles installés. " +
             "Index 0 = panneau vide, index 1 = 1 fusible, ... index 5 = tous installés.")]
    [SerializeField] Sprite[] fuseStateSprites;

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
        // Si pas assigné dans l'Inspector, on essaie de le trouver
        if (panelRenderer == null)
            panelRenderer = GetComponent<SpriteRenderer>();

        // État visuel initial : aucun fusible installé
        UpdateSprite();
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

            // Changement de sprite selon le nombre de fusibles installés
            UpdateSprite();
            UpdateCounter();

            // (Optionnel : son d'insertion ici)

            yield return new WaitForSeconds(delayBetweenInserts);
        }

        isInserting = false;
    }

    /// <summary>
    /// Met le sprite du panneau qui correspond au nombre de fusibles installés.
    /// Pioche dans la liste fuseStateSprites en utilisant FusesInstalled comme index.
    /// </summary>
    void UpdateSprite()
    {
        if (panelRenderer == null) return;
        if (FuseManager.Instance == null) return;
        if (fuseStateSprites == null || fuseStateSprites.Length == 0) return;

        int installed = FuseManager.Instance.FusesInstalled;

        // Sécurité : on borne l'index dans la liste (au cas où la liste n'aurait pas
        // assez de sprites pour le total de fusibles)
        int index = Mathf.Clamp(installed, 0, fuseStateSprites.Length - 1);

        Sprite spriteToShow = fuseStateSprites[index];
        if (spriteToShow != null)
            panelRenderer.sprite = spriteToShow;
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