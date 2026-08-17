using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Cálculo de las celdas que ocupa una huella y de dónde/ cómo va su visual.
    /// Separado del sistema para que lo compartan la validación, la colocación, el ghost
    /// y la vista previa de editor — así runtime y editor colocan los prefabs igual.
    /// </summary>
    public static class GridFootprint
    {
        /// <summary>Centro geométrico de las celdas (deja el prefab centrado sobre su huella).</summary>
        public static Vector3 CenterWorld(IReadOnlyGrid grid, List<Vector2Int> cells)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector2Int c in cells) sum += grid.GetCellCenterWorld(c);
            return sum / Mathf.Max(1, cells.Count);
        }

        /// <summary>Giro del visual alrededor de la normal del plano según la rotación de grilla.</summary>
        public static Quaternion VisualRotation(IReadOnlyGrid grid, GridRotation rotation)
        {
            Vector3 axis = grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.forward;
            return Quaternion.AngleAxis(rotation.ToDegrees(), axis);
        }

        /// <summary>Rellena 'result' con las celdas ocupadas = ancla + offsetLocal rotado.</summary>
        public static void ComputeCells(
            PlaceableDefinition definition, Vector2Int anchor, GridRotation rotation, List<Vector2Int> result)
        {
            result.Clear();
            if (definition == null) return;

            foreach (Vector2Int local in definition.GetLocalCells())
                result.Add(anchor + rotation.Rotate(local));
        }

        /// <summary>Versión que crea una lista nueva (cómoda, pero asigna memoria).</summary>
        public static List<Vector2Int> ComputeCells(
            PlaceableDefinition definition, Vector2Int anchor, GridRotation rotation)
        {
            var list = new List<Vector2Int>();
            ComputeCells(definition, anchor, rotation, list);
            return list;
        }
    }
}