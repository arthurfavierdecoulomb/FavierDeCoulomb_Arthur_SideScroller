using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "SurfaceTileDatabase", menuName = "Alterlimb/Surface Tile Database")]
public class SurfaceTileDatabase : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public SurfaceType surface;
        public List<TileBase> tiles = new List<TileBase>();
    }

    [Header("Correspondances")]
    public List<Entry> entries = new List<Entry>();

    Dictionary<TileBase, SurfaceType> lookup;

    void OnEnable()
    {
        lookup = null;
    }

    void OnValidate()
    {
        lookup = null;
    }

    public bool TryGetSurface(TileBase tile, out SurfaceType surface)
    {
        surface = default;
        if (tile == null) return false;

        if (lookup == null) BuildLookup();

        return lookup.TryGetValue(tile, out surface);
    }

    void BuildLookup()
    {
        lookup = new Dictionary<TileBase, SurfaceType>();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || entry.tiles == null) continue;

            for (int t = 0; t < entry.tiles.Count; t++)
            {
                TileBase tile = entry.tiles[t];
                if (tile == null) continue;

                lookup[tile] = entry.surface;
            }
        }
    }
}