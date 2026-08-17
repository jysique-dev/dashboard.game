using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Movement
{
    /// <summary>
    /// Mueve este Transform a lo largo de un camino de celdas (convertidas a centros del mundo).
    /// Separa "movimiento" de "pathfinding": aquí solo se sigue una ruta ya calculada.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Mover")]
    public class GridMover : MonoBehaviour
    {
        [SerializeField] private float speed = 3f;
        [SerializeField] private float arriveThreshold = 0.02f;
        [Tooltip("Desplazamiento vertical sobre el plano (para pivotes que no están en la base).")]
        [SerializeField] private float heightOffset = 0f;
        [SerializeField] private bool faceMovement = true;

        private IReadOnlyGrid _grid;
        private readonly List<Vector3> _waypoints = new();
        private int _index;

        public bool IsMoving => _index < _waypoints.Count;

        public void SetGrid(IReadOnlyGrid grid) => _grid = grid;

        /// <summary>Empieza a seguir el camino dado (lista de celdas). Ignora la primera si ya está en ella.</summary>
        public void FollowPath(IReadOnlyList<Vector2Int> cells)
        {
            _waypoints.Clear();
            _index = 0;
            if (_grid == null || cells == null) return;

            Vector3 up = _grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.back;
            foreach (Vector2Int c in cells)
                _waypoints.Add(_grid.GetCellCenterWorld(c) + up * heightOffset);

            // Si el primer waypoint es prácticamente la posición actual, sáltalo.
            if (_waypoints.Count > 0 &&
                (transform.position - _waypoints[0]).sqrMagnitude < arriveThreshold * arriveThreshold)
                _index = 1;
        }

        public void Stop()
        {
            _waypoints.Clear();
            _index = 0;
        }

        private void Update()
        {
            if (!IsMoving) return;

            Vector3 target = _waypoints[_index];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (faceMovement)
            {
                Vector3 dir = target - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }

            if ((transform.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold)
                _index++;
        }
    }
}