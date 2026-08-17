using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Responde que socket presenta el vecindario en una celda y una direccion.
    /// Quien resuelve un volumen no necesita saber si al lado hay otro edificio, una calle
    /// o el borde del mundo: solo pregunta aqui.
    /// </summary>
    public interface IWfcFacadeProvider
    {
        /// <param name="worldCell">Celda en coordenadas de ciudad.</param>
        /// <param name="fromDirection">Direccion en la que se mira desde la celda consultante.</param>
        /// <param name="socket">Socket que el vecino presenta hacia el consultante.</param>
        bool TryGetFacadeSocket(Vector3Int worldCell, WfcDirection fromDirection, out WfcSocketDescriptor socket);
    }

    /// <summary>
    /// Volumenes ya resueltos de un distrito, indexados por su celda de origen en la ciudad.
    ///
    /// La consulta lee el estado vivo del solver de cada volumen, no una copia: si un
    /// edificio se vuelve a resolver, sus vecinos ven la fachada nueva sin que nadie tenga
    /// que invalidar caches.
    /// </summary>
    public sealed class WfcVolumeRegistry : IWfcFacadeProvider
    {
        private sealed class Entry
        {
            public WfcRunner Runner;
            public Vector3Int Origin;
            public Vector3Int Size;

            public bool Contains(Vector3Int cell)
                => cell.x >= Origin.x && cell.x < Origin.x + Size.x
                && cell.y >= Origin.y && cell.y < Origin.y + Size.y
                && cell.z >= Origin.z && cell.z < Origin.z + Size.z;
        }

        private readonly List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        public void Register(WfcRunner runner, Vector3Int origin, Vector3Int size)
        {
            if (runner == null) return;

            Unregister(runner);
            entries.Add(new Entry { Runner = runner, Origin = origin, Size = size });
        }

        public void Unregister(WfcRunner runner)
        {
            entries.RemoveAll(entry => entry.Runner == runner || entry.Runner == null);
        }

        public void Clear() => entries.Clear();

        /// <summary>Volumenes cuya huella toca la del rectangulo dado. Para re-resolver vecinos.</summary>
        public void FindTouching(Vector3Int origin, Vector3Int size, List<WfcRunner> results)
        {
            foreach (var entry in entries)
            {
                if (entry.Runner == null) continue;

                bool separatedX = entry.Origin.x >= origin.x + size.x || origin.x >= entry.Origin.x + entry.Size.x;
                bool separatedZ = entry.Origin.z >= origin.z + size.z || origin.z >= entry.Origin.z + entry.Size.z;

                // Se cuenta como vecino si comparte borde: se expande uno de los dos en 1.
                bool touchingX = entry.Origin.x > origin.x + size.x || origin.x > entry.Origin.x + entry.Size.x;
                bool touchingZ = entry.Origin.z > origin.z + size.z || origin.z > entry.Origin.z + entry.Size.z;

                if (touchingX || touchingZ) continue;
                if (separatedX && separatedZ) continue;

                results.Add(entry.Runner);
            }
        }

        public bool TryGetFacadeSocket(
            Vector3Int worldCell, WfcDirection fromDirection, out WfcSocketDescriptor socket)
        {
            socket = WfcSocketDescriptor.Invalid;

            var offset = WfcDirections.Offset(fromDirection);
            var target = worldCell + new Vector3Int(offset.X, offset.Y, offset.Z);

            foreach (var entry in entries)
            {
                if (entry.Runner == null || !entry.Runner.IsPrepared) continue;
                if (!entry.Contains(target)) continue;

                var local = target - entry.Origin;
                var topology = entry.Runner.Solver.Topology;

                if (!topology.Contains(local.x, local.y, local.z)) continue;

                int index = topology.Index(local.x, local.y, local.z);
                int variant = entry.Runner.Solver.GetCollapsed(index);

                // Vecino aun sin fijar: no impone nada todavia.
                if (variant < 0) return false;

                // El vecino nos mira por su cara opuesta a nuestra direccion de consulta.
                socket = entry.Runner.Adjacency.Baked.GetSocket(
                    variant, WfcDirections.Opposite(fromDirection));

                return socket.IsValid;
            }

            return false;
        }
    }
}