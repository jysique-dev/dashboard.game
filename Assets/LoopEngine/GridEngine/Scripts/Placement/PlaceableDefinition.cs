using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Describe algo que se puede colocar en la grilla (edificio, decoración, etc.) como asset.
    /// La huella es rectangular por defecto (size), o una lista de celdas locales para formas
    /// no rectangulares (customCells). Los offsets locales parten de (0,0).
    /// </summary>
    [CreateAssetMenu(fileName = "Placeable", menuName = LoopRoutes.PlaceableDefinition)]
    public class PlaceableDefinition : ScriptableObject
    {
        [Tooltip("Identificador legible (para guardado, economía, etc.).")]
        public string id = "placeable";

        [Tooltip("Prefab visual a instanciar. Puede quedar vacío para probar solo la ocupación.")]
        public GameObject visualPrefab;

        [Header("Huella rectangular (en celdas)")]
        [Min(1)] public int width = 1;
        [Min(1)] public int height = 1;

        [Header("Huella personalizada (opcional)")]
        [Tooltip("Si tiene elementos, sustituye a la huella rectangular. Offsets desde (0,0).")]
        public List<Vector2Int> customCells = new();

        [Header("Movimiento")]
        [Tooltip("Si es true, las celdas de esta pieza bloquean el paso de personajes (edificio). " +
                 "Ponlo en false para calles, suelos o decoración transitable.")]
        public bool blocksMovement = true;

        /// <summary>Celdas locales (sin rotar) que ocupa esta pieza, relativas al ancla (0,0).</summary>
        public IEnumerable<Vector2Int> GetLocalCells()
        {
            if (customCells != null && customCells.Count > 0)
            {
                foreach (Vector2Int c in customCells) yield return c;
                yield break;
            }

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    yield return new Vector2Int(x, y);
        }
    }
}