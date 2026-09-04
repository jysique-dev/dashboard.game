using System;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Maps each <see cref="CellType"/> to a traversal cost.
    ///
    /// An enum cannot carry data, so the weight lives in a table instead. Serializable
    /// so it can be tuned in the Inspector, and shared by value through GridSettings.
    ///
    /// These are terrain-level defaults. Per-unit modifiers (a flier ignoring water, a
    /// tank refused entry to a forest) belong to the movement system, not here: the grid
    /// describes the ground, not who walks on it.
    /// </summary>
    [Serializable]
    public class CellWeightTable
    {
        [Serializable]
        public struct Entry
        {
            public CellType type;

            [Tooltip("Traversal cost. Higher is slower. Ignored when Passable is off.")]
            [Min(0f)] public float weight;

            [Tooltip("Off means the cell cannot be entered at all.")]
            public bool passable;

            public Color color;
        }

        [Tooltip("Used for any cell type missing from the list below.")]
        [SerializeField, Min(0f)] private float defaultWeight = 1f;
        [SerializeField] private bool defaultPassable = true;

        [SerializeField]
        private Entry[] entries =
        {
            new Entry { type = CellType.Empty,     weight = 0f, passable = false ,color = new Color(0f, 0f, 0f, 0.5f)},
            new Entry { type = CellType.Ground,    weight = 1f, passable = true  ,color = new Color(0.35f, 0.70f, 0.40f, 0.55f) },
            new Entry { type = CellType.Blocked,   weight = 0f, passable = false ,color = new Color(0.75f, 0.25f, 0.25f, 0.65f) },
            new Entry { type = CellType.Difficult, weight = 2f, passable = true  ,color = new Color(0.25f, 0.50f, 0.85f, 0.55f) },
            new Entry { type = CellType.Spawn,     weight = 1f, passable = true  ,color = new Color(0.95f, 0.80f, 0.20f, 0.60f) },
            new Entry { type = CellType.Objective, weight = 1f, passable = true  ,color = new Color(0.85f, 0.40f, 0.85f, 0.60f)}
        };

        /// <summary>Cost of entering a cell of this type. Returns <see cref="Mathf.Infinity"/> when impassable.</summary>
        public float GetWeight(CellType type)
        {
            if (!TryGetEntry(type, out Entry entry))
                return defaultPassable ? defaultWeight : Mathf.Infinity;

            return entry.passable ? Mathf.Max(0f, entry.weight) : Mathf.Infinity;
        }

        public bool IsPassable(CellType type)
            => TryGetEntry(type, out Entry entry) ? entry.passable : defaultPassable;

        private bool TryGetEntry(CellType type, out Entry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].type != type) continue;
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }


        public bool TryGetPaletteColor(CellType type, out Color color)
        {
            color = default;
            if (type == CellType.Empty) return false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].type != type) continue;
                color = entries[i].color;
                return true;
            }

            return false;
        }

        /// <summary>Deep copy, so an asset never shares its array with a scene object.</summary>
        public CellWeightTable Clone()
        {
            CellWeightTable copy = new CellWeightTable
            {
                defaultWeight = this.defaultWeight,
                defaultPassable = this.defaultPassable,
                entries = entries != null ? (Entry[])entries.Clone() : Array.Empty<Entry>()
            };
            return copy;
        }
    }
}