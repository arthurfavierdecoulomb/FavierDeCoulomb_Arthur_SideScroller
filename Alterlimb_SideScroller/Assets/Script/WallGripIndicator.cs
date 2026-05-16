using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Affiche une icône sur chaque bloc "grippable" des tilemaps GroundWallGrip.
/// L'opacité de chaque icône dépend de la distance au joueur :
///   - Loin   → transparente (voire invisible)
///   - Proche → opaque
/// 
/// Gère PLUSIEURS tilemaps (ex: un par zone de jeu — Usine, Metro, etc.).
/// 
/// C'est un système d'affordance : il guide le joueur en douceur vers les
/// surfaces sur lesquelles il peut s'accrocher (wall-grip).
/// 
/// IMPORTANT : ce script ne fait QUE l'affichage. Le wall-grip lui-même est
/// géré par la physique (Physics Materials 2D).
/// 
/// Setup :
///   - Place ce script sur un GameObject vide (ex: "WallGripIndicatorManager").
///   - Assigne TOUS les tilemaps GroundWallGrip (Usine, Metro...) dans la liste.
///   - Assigne le prefab d'icône (un sprite avec SpriteRenderer).
///   - Assigne la référence du joueur (ou laisse le script le trouver par tag).
/// </summary>
public class WallGripIndicator : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Tous les tilemaps GroundWallGrip du jeu (un par zone : Usine, Metro, etc.)")]
    [SerializeField] List<Tilemap> wallGripTilemaps = new List<Tilemap>();
    [Tooltip("Prefab de l'icône à afficher sur chaque bloc (doit avoir un SpriteRenderer)")]
    [SerializeField] GameObject iconPrefab;
    [Tooltip("Le joueur (si laissé vide, recherché automatiquement par tag)")]
    [SerializeField] Transform player;
    [SerializeField] string playerTag = "Player";

    [Header("Distances de fade")]
    [Tooltip("Distance en-dessous de laquelle l'icône est totalement opaque")]
    [SerializeField] float fullVisibilityDistance = 3f;
    [Tooltip("Distance au-delà de laquelle l'icône est totalement invisible")]
    [SerializeField] float invisibleDistance = 10f;

    [Header("Apparence")]
    [Tooltip("Opacité maximale de l'icône (1 = totalement opaque)")]
    [Range(0f, 1f)]
    [SerializeField] float maxAlpha = 1f;

    [Header("Optimisation")]
    [Tooltip("Si activé, les icônes au-delà de invisibleDistance sont désactivées (économie de rendu)")]
    [SerializeField] bool disableWhenInvisible = true;

    // ── Runtime ──
    readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();

    // ════════════════════════════════════════════════════════════
    //  Initialisation
    // ════════════════════════════════════════════════════════════

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) player = p.transform;
        }

        if (player == null)
        {
            Debug.LogError("[WallGripIndicator] Joueur introuvable !");
            enabled = false;
            return;
        }

        if (iconPrefab == null)
        {
            Debug.Log("[WallGripIndicator] Prefab d'icône non assigné !");
            enabled = false;
            return;
        }

        if (wallGripTilemaps == null || wallGripTilemaps.Count == 0)
        {
            Debug.LogError("[WallGripIndicator] Aucun tilemap GroundWallGrip assigné !");
            enabled = false;
            return;
        }

        SpawnIconsOnAllTilemaps();
    }

    /// <summary>
    /// Parcourt TOUS les tilemaps de la liste et instancie une icône
    /// sur chaque cellule contenant une tuile.
    /// </summary>
    void SpawnIconsOnAllTilemaps()
    {
        int totalIcons = 0;

        foreach (Tilemap tilemap in wallGripTilemaps)
        {
            if (tilemap == null)
            {
                Debug.LogWarning("[WallGripIndicator] Un slot de tilemap est vide, ignoré.");
                continue;
            }

            int iconsForThisTilemap = SpawnIconsOnTilemap(tilemap);
            totalIcons += iconsForThisTilemap;
        }

        Debug.Log($"[WallGripIndicator] {totalIcons} icônes créées sur {wallGripTilemaps.Count} tilemap(s).");
    }

    /// <summary>
    /// Scanne un tilemap donné et crée une icône par tuile peinte.
    /// </summary>
    /// <returns>Le nombre d'icônes créées pour ce tilemap.</returns>
    int SpawnIconsOnTilemap(Tilemap tilemap)
    {
        int count = 0;
        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int cellPos in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cellPos)) continue;

            Vector3 worldPos = tilemap.GetCellCenterWorld(cellPos);

            GameObject icon = Instantiate(iconPrefab, worldPos, Quaternion.identity, transform);
            SpriteRenderer sr = icon.GetComponent<SpriteRenderer>();

            if (sr != null)
            {
                iconRenderers.Add(sr);
                count++;
            }
            else
            {
                Debug.LogWarning("[WallGripIndicator] Le prefab d'icône n'a pas de SpriteRenderer !");
                Destroy(icon);
            }
        }

        return count;
    }

    // ════════════════════════════════════════════════════════════
    //  Mise à jour de l'opacité chaque frame
    // ════════════════════════════════════════════════════════════

    void Update()
    {
        if (player == null) return;

        Vector2 playerPos = player.position;

        foreach (SpriteRenderer sr in iconRenderers)
        {
            if (sr == null) continue;

            float distance = Vector2.Distance(playerPos, sr.transform.position);

            // Optimisation : si l'icône est trop loin, on la désactive carrément
            if (disableWhenInvisible && distance >= invisibleDistance)
            {
                if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                continue;
            }

            // Réactive l'icône si elle revient dans la zone visible
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);

            // Calcule l'alpha selon la distance
            float t = Mathf.InverseLerp(invisibleDistance, fullVisibilityDistance, distance);
            float alpha = t * maxAlpha;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Gizmos
    // ════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(player.position, fullVisibilityDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(player.position, invisibleDistance);
    }
}