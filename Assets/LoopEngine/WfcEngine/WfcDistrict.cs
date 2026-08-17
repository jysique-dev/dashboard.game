using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Un barrio: mantiene el registro de volumenes resueltos y crea uno nuevo por cada
    /// edificio colocado, cosiendolo con sus vecinos.
    ///
    /// No conoce GridEngine. Escucha un IWfcPlacementFeed, que puede ser el adaptador de
    /// GridEngine, el pincel manual del editor, o cualquier otra cosa. El acoplamiento con
    /// el sistema de colocacion vive entero en el adaptador.
    ///
    /// Politica: un edificio nuevo se adapta a los que ya estan; los existentes no se
    /// rehacen salvo que se pida. Es lo que espera un jugador que acaba de colocar algo,
    /// y evita que media ciudad parpadee por poner una casa.
    /// </summary>
    /// <remarks>
    /// ExecuteAlways es obligatorio: sin el, OnEnable no corre en modo edicion, el feed
    /// nunca se suscribe y colocar desde el editor no hace absolutamente nada, en silencio.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("LoopEngine/WFC/District")]
    public class WfcDistrict : MonoBehaviour
    {
        [Tooltip("Tamano de celda de la ciudad. Debe coincidir con el cellSize de los sets.")]
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [SerializeField] private int seed = 1;

        [Tooltip("Al colocar, vuelve a resolver los edificios que tocan al nuevo.")]
        [SerializeField] private bool resolveNeighbours;

        [SerializeField] private WfcSeamFailurePolicy seamPolicy = WfcSeamFailurePolicy.LeaveOpen;

        [SerializeField] private bool drawGizmos = true;

        private readonly WfcVolumeRegistry registry = new WfcVolumeRegistry();
        private readonly Dictionary<Vector3Int, WfcRunner> placed = new Dictionary<Vector3Int, WfcRunner>();
        private readonly List<WfcRunner> scratch = new List<WfcRunner>();

        private IWfcPlacementFeed feed;

        public WfcVolumeRegistry Registry => registry;
        public float CellSize => cellSize;
        public int Count => placed.Count;
        public IReadOnlyDictionary<Vector3Int, WfcRunner> Placed => placed;

        private void OnEnable() => EnsureSubscribed();

        private void OnValidate() => EnsureSubscribed();

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// Idempotente y llamable en cualquier momento. Hace falta porque el feed se puede
        /// anadir al GameObject despues que el distrito, y anadir un componente no vuelve
        /// a disparar el OnEnable de sus hermanos.
        /// </summary>
        public void EnsureSubscribed()
        {
            var current = GetComponent<IWfcPlacementFeed>();

            if (ReferenceEquals(current, feed) && feed != null) return;

            Unsubscribe();

            feed = current;
            if (feed == null) return;

            feed.Placed += OnPlaced;
            feed.Removed += OnRemoved;
        }

        private void Unsubscribe()
        {
            if (feed == null) return;

            feed.Placed -= OnPlaced;
            feed.Removed -= OnRemoved;
            feed = null;
        }

        public bool HasFeed => feed != null;

        private void OnPlaced(Vector3Int cell, WfcBuildingDefinition definition) => Place(cell, definition);

        private void OnRemoved(Vector3Int cell) => Remove(cell);

        // ------------------------------------------------------------------ colocacion

        public Vector3 CellToWorld(Vector3Int cell)
            => transform.position + new Vector3(cell.x, cell.y, cell.z) * cellSize;

        public Vector3Int WorldToCell(Vector3 world)
        {
            var local = (world - transform.position) / cellSize;
            return new Vector3Int(
                Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y), Mathf.RoundToInt(local.z));
        }

        public WfcRunner Place(Vector3Int cell, WfcBuildingDefinition definition)
        {
            // Cada salida temprana avisa: un Place que no hace nada y calla es
            // indistinguible de un click que no llego.
            if (definition == null)
            {
                Debug.LogWarning("[WFC] Colocacion sin Building Definition.", this);
                return null;
            }

            if (definition.ModuleSet == null)
            {
                Debug.LogWarning($"[WFC] '{definition.name}' no tiene Module Set.", definition);
                return null;
            }

            Remove(cell);

            var go = new GameObject($"Building {cell.x},{cell.z} ({definition.name})");
            go.transform.SetParent(transform, false);
            go.transform.position = CellToWorld(cell);
            go.hideFlags = HideFlags.DontSave;

            var runner = go.AddComponent<WfcRunner>();
            ConfigureRunner(runner, definition);

            var seams = go.AddComponent<WfcSeamConstraints>();
            seams.OriginCell = cell;
            seams.SetFacadeProvider(registry);

            registry.Register(runner, cell, definition.Size);
            placed[cell] = runner;

            if (!runner.Prepare())
            {
                Debug.LogWarning(
                    $"[WFC] No se pudo preparar '{definition.name}': el set no produjo variantes. " +
                    "Revisa la ventana Neighbor Matrix.", definition);
                return runner;
            }

            var state = runner.RunToCompletion();

            if (state != Core.WfcSolverState.Solved)
            {
                Debug.LogWarning(
                    $"[WFC] '{definition.name}' en {cell} termino en {state}. " +
                    (string.IsNullOrEmpty(runner.ConstraintMessage)
                        ? "Sin solucion para este volumen."
                        : runner.ConstraintMessage),
                    runner);
            }

            if (resolveNeighbours) ResolveNeighboursOf(cell, definition.Size, runner);

            return runner;
        }

        /// <summary>
        /// El runner se configura por SerializedObject-equivalente: campos privados via
        /// metodos publicos donde existen, y el resto por el propio inspector del prefab.
        /// Aqui solo se toca lo que el edificio define.
        /// </summary>
        private void ConfigureRunner(WfcRunner runner, WfcBuildingDefinition definition)
        {
            runner.Configure(
                definition.ModuleSet,
                definition.Exclusions,
                definition.Size,
                definition.Heuristic,
                definition.MaxBacktracks);

            runner.Seed = seed + placed.Count * 977;
        }

        public bool Remove(Vector3Int cell)
        {
            if (!placed.TryGetValue(cell, out var runner)) return false;

            placed.Remove(cell);
            registry.Unregister(runner);

            if (runner != null)
            {
                runner.Clear();

                if (Application.isPlaying) Destroy(runner.gameObject);
                else DestroyImmediate(runner.gameObject);
            }

            return true;
        }

        public void Clear()
        {
            var cells = new List<Vector3Int>(placed.Keys);
            foreach (var cell in cells) Remove(cell);

            registry.Clear();
        }

        /// <summary>Rehace los vecinos para que la costura case por ambos lados, no solo por el nuevo.</summary>
        public void ResolveNeighboursOf(Vector3Int cell, Vector3Int size, WfcRunner exclude)
        {
            scratch.Clear();
            registry.FindTouching(cell, size, scratch);

            foreach (var neighbour in scratch)
            {
                if (neighbour == null || neighbour == exclude) continue;

                neighbour.Prepare();
                neighbour.RunToCompletion();
            }
        }

        public void ResolveAll()
        {
            foreach (var pair in placed)
            {
                if (pair.Value == null) continue;

                pair.Value.Prepare();
                pair.Value.RunToCompletion();
            }
        }

        /// <summary>Resumen de costuras: cuantas caras quedaron sin casar en todo el barrio.</summary>
        public string DescribeSeams()
        {
            int constrained = 0;
            int open = 0;

            foreach (var pair in placed)
            {
                if (pair.Value == null) continue;

                var seams = pair.Value.GetComponent<WfcSeamConstraints>();
                if (seams == null) continue;

                constrained += seams.ConstrainedFaces;
                open += seams.OpenSeams;
            }

            return $"{placed.Count} edificios · {constrained} caras cosidas · {open} juntas abiertas";
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.35f);

            foreach (var pair in placed)
            {
                if (pair.Value == null) continue;

                Gizmos.DrawWireCube(CellToWorld(pair.Key), Vector3.one * cellSize * 0.4f);
            }
        }
    }
}