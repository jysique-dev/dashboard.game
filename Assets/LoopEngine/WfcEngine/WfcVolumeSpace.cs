using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Traduccion entre indices de celda y posiciones de mundo. Es la unica pieza que sabe
    /// las dos cosas a la vez: el solver solo maneja indices y la escena solo posiciones.
    ///
    /// Origin es el centro de la celda (0,0,0), no su esquina, porque los prefabs de modulo
    /// tienen el pivote centrado. Si el pivote estuviera en la base, todo el volumen saldria
    /// desplazado media celda al rotar.
    /// </summary>
    public readonly struct WfcVolumeSpace
    {
        public readonly WfcTopology Topology;
        public readonly float CellSize;
        public readonly Vector3 Origin;

        public WfcVolumeSpace(WfcTopology topology, float cellSize, Vector3 origin)
        {
            Topology = topology;
            CellSize = cellSize <= 0f ? 1f : cellSize;
            Origin = origin;
        }

        public Vector3 Size => new Vector3(
            Topology.SizeX * CellSize,
            Topology.SizeY * CellSize,
            Topology.SizeZ * CellSize);

        public Vector3 CellToWorld(int x, int y, int z)
            => Origin + new Vector3(x, y, z) * CellSize;

        public Vector3 CellToWorld(int index)
        {
            Topology.CoordsOf(index, out int x, out int y, out int z);
            return CellToWorld(x, y, z);
        }

        public bool TryWorldToCell(Vector3 world, out int index)
        {
            var local = (world - Origin) / CellSize;

            int x = Mathf.RoundToInt(local.x);
            int y = Mathf.RoundToInt(local.y);
            int z = Mathf.RoundToInt(local.z);

            if (!Topology.Contains(x, y, z))
            {
                index = -1;
                return false;
            }

            index = Topology.Index(x, y, z);
            return true;
        }

        /// <summary>Caja que envuelve el volumen entero, util para gizmos y encuadre de camara.</summary>
        public Bounds Bounds
        {
            get
            {
                var center = Origin + new Vector3(
                    (Topology.SizeX - 1) * 0.5f,
                    (Topology.SizeY - 1) * 0.5f,
                    (Topology.SizeZ - 1) * 0.5f) * CellSize;

                return new Bounds(center, Size);
            }
        }
    }
}