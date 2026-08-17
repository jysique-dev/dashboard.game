using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Movement
{
    /// <summary>
    /// Pathfinding A* sobre la grilla. Es C# puro (sin MonoBehaviour): recibe la geometría
    /// (IReadOnlyGrid, para límites y vecinos) y la caminabilidad (IGridWalkability).
    ///
    /// Implementación directa de A* con lista abierta simple (extracción lineal del mínimo f).
    /// Es correcta y suficiente para grillas de citybuilder; para grillas enormes se puede
    /// cambiar la lista abierta por un binary heap (ver notas de Red Blob Games).
    /// </summary>
    public class GridPathfinder
    {
        private readonly IReadOnlyGrid _grid;
        private readonly IGridWalkability _walk;
        private readonly bool _allowDiagonal;

        private static readonly Vector2Int[] Orthogonal =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };
        private static readonly Vector2Int[] Diagonal =
        {
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        public GridPathfinder(IReadOnlyGrid grid, IGridWalkability walkability, bool allowDiagonal = false)
        {
            _grid = grid;
            _walk = walkability;
            _allowDiagonal = allowDiagonal;
        }

        /// <summary>
        /// Devuelve la lista de celdas de 'start' a 'goal' (ambas incluidas), o null si no hay camino.
        /// No exige que 'start' sea caminable (el personaje ya está ahí), pero 'goal' sí debe serlo.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            if (_grid == null || _walk == null) return null;
            if (!_grid.IsInside(start) || !_grid.IsInside(goal)) return null;
            if (!_walk.IsWalkable(goal)) return null;
            if (start == goal) return new List<Vector2Int> { start };

            var open = new List<Vector2Int> { start };
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
            var fScore = new Dictionary<Vector2Int, float> { [start] = Heuristic(start, goal) };

            while (open.Count > 0)
            {
                Vector2Int current = LowestF(open, fScore);

                if (current == goal) return Reconstruct(cameFrom, current);

                open.Remove(current);
                closed.Add(current);

                foreach (Vector2Int neighbor in Neighbors(current))
                {
                    if (closed.Contains(neighbor)) continue;
                    if (!_grid.IsInside(neighbor) || !_walk.IsWalkable(neighbor)) continue;

                    float tentative = gScore[current] + StepCost(current, neighbor);
                    if (!gScore.TryGetValue(neighbor, out float known) || tentative < known)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentative;
                        fScore[neighbor] = tentative + Heuristic(neighbor, goal);
                        if (!open.Contains(neighbor)) open.Add(neighbor);
                    }
                }
            }

            return null; // sin camino
        }

        // --- Internos ---

        private IEnumerable<Vector2Int> Neighbors(Vector2Int c)
        {
            foreach (Vector2Int d in Orthogonal) yield return c + d;
            if (_allowDiagonal)
                foreach (Vector2Int d in Diagonal) yield return c + d;
        }

        private float StepCost(Vector2Int a, Vector2Int b)
            => (a.x != b.x && a.y != b.y) ? 1.41421356f : 1f;

        private float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            if (_allowDiagonal)
            {
                // Distancia diagonal (octile).
                int min = Mathf.Min(dx, dy);
                return (dx + dy) - (2f - 1.41421356f) * min;
            }
            return dx + dy; // Manhattan
        }

        private static Vector2Int LowestF(List<Vector2Int> open, Dictionary<Vector2Int, float> fScore)
        {
            Vector2Int best = open[0];
            float bestF = fScore.TryGetValue(best, out float f0) ? f0 : float.PositiveInfinity;
            for (int i = 1; i < open.Count; i++)
            {
                float f = fScore.TryGetValue(open[i], out float v) ? v : float.PositiveInfinity;
                if (f < bestF) { bestF = f; best = open[i]; }
            }
            return best;
        }

        private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.TryGetValue(current, out Vector2Int prev))
            {
                current = prev;
                path.Add(current);
            }
            path.Reverse();
            return path;
        }
    }
}