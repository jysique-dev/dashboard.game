using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Ejecuta el solver sobre un volumen y mantiene la escena sincronizada con su estado.
    ///
    /// La sincronizacion es reconciliacion, no acumulacion: en cada paso se comparan las
    /// celdas marcadas con lo que dice el solver y se corrige la diferencia. Es la unica
    /// forma correcta de presentar un solver con backtracking, porque una celda colapsada
    /// puede dejar de estarlo. Instanciar en CellCollapsed y olvidarse dejaria geometria
    /// fantasma de ramas descartadas.
    ///
    /// Sin pooling todavia: cada cambio destruye e instancia. Es medible y suficiente para
    /// volumenes de edificio; el pool llega en la sesion 9, cuando haya con que compararlo.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Runner")]
    public class WfcRunner : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private WfcModuleSet moduleSet;
        [SerializeField] private WfcExclusionSet exclusions;
        [SerializeField] private bool dropDuplicateRotations = true;

        [Header("Volumen")]
        [SerializeField] private Vector3Int size = new Vector3Int(6, 4, 6);
        [SerializeField] private bool centerOnTransform = true;

        [Header("Solver")]
        [SerializeField] private int seed = 1;
        [SerializeField] private WfcHeuristic heuristic = WfcHeuristic.ShannonEntropy;
        [SerializeField, Min(0)] private int maxBacktracks = 10000;

        [Header("Ejecucion")]
        [SerializeField] private bool solveOnStart = true;
        [SerializeField] private bool animate = true;
        [SerializeField, Min(1)] private int stepsPerFrame = 1;

        [Tooltip("0 = sin limite. Corta la animacion cuando el frame ya ha gastado esto.")]
        [SerializeField, Min(0f)] private float frameBudgetMilliseconds = 4f;
        [SerializeField] private bool usePool = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawVolume = true;
        [SerializeField] private bool drawOpenCells = true;

        private const string ContainerName = "WFC Output";

        private Transform container;
        private Transform parking;
        private WfcInstancePool pool;
        private GameObject[] instances;
        private int[] instanceVariant;

        private readonly HashSet<int> dirty = new HashSet<int>();
        private bool fullResync;

        public WfcAdjacency Adjacency { get; private set; }
        public WfcSolver Solver { get; private set; }
        public WfcVolumeSpace Space { get; private set; }

        public WfcModuleSet ModuleSet => moduleSet;
        public int Seed { get => seed; set => seed = value; }
        public bool Animate { get => animate; set => animate = value; }
        public int StepsPerFrame => stepsPerFrame;

        public bool IsPrepared => Solver != null && Adjacency != null && Adjacency.Matrix != null;

        /// <summary>Motivo del fallo de las restricciones previas, o null si todo fue bien.</summary>
        public string ConstraintMessage { get; private set; }

        public WfcSolverState State => Solver != null ? Solver.State : WfcSolverState.Running;

        /// <summary>0..1 segun celdas ya fijadas.</summary>
        public float Progress
        {
            get
            {
                if (Solver == null) return 0f;
                int total = Solver.Topology.CellCount;
                return total == 0 ? 0f : 1f - (float)Solver.RemainingCells / total;
            }
        }

        /// <summary>Se dispara al materializar una celda. Punto de enganche para otros sistemas.</summary>
        public event Action<int, int, GameObject> CellInstantiated;

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (!solveOnStart) return;

            Prepare();

            if (!animate) RunToCompletion();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!animate || !IsPrepared) return;
            if (Solver.State != WfcSolverState.Running) return;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < stepsPerFrame; i++)
            {
                if (Solver.Step() != WfcSolverState.Running) break;

                if (frameBudgetMilliseconds > 0f &&
                    watch.Elapsed.TotalMilliseconds >= frameBudgetMilliseconds)
                {
                    break;
                }
            }

            SyncInstances();
        }

        // ------------------------------------------------------------------ ciclo

        /// <summary>Hornea, construye la matriz y deja el solver listo en la primera celda.</summary>
        public bool Prepare()
        {
            Clear();

            if (moduleSet == null) return false;

            Adjacency = WfcAdjacencyBuilder.Build(moduleSet, exclusions, dropDuplicateRotations);

            if (Adjacency.Matrix == null || Adjacency.VariantCount == 0)
            {
                Solver = null;
                return false;
            }

            var topology = new WfcTopology(size.x, size.y, size.z);
            Space = new WfcVolumeSpace(topology, moduleSet.CellSize, ComputeOrigin(topology));

            var weights = new float[Adjacency.VariantCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = Adjacency.Baked.Core.GetWeight(i);

            Solver = new WfcSolver(Adjacency.Matrix, topology, weights, new WfcSolverSettings
            {
                Seed = unchecked((uint)seed),
                Heuristic = heuristic,
                MaxBacktracks = maxBacktracks
            });

            Solver.CellCollapsed += OnCellCollapsed;
            Solver.StateReverted += OnStateReverted;

            instances = new GameObject[topology.CellCount];
            instanceVariant = new int[topology.CellCount];
            for (int i = 0; i < instanceVariant.Length; i++) instanceVariant[i] = -1;

            dirty.Clear();
            fullResync = false;

            pool = usePool ? new WfcInstancePool(EnsureParking()) : null;

            ApplyConstraintSource();

            return true;
        }

        /// <summary>
        /// Restricciones previas, si hay quien las aporte en este GameObject. El runner no
        /// conoce ninguna implementacion concreta: solo la interfaz.
        /// </summary>
        private void ApplyConstraintSource()
        {
            ConstraintMessage = null;

            // Varias fuentes pueden convivir: los bordes pintados a mano y la costura con
            // los edificios vecinos son restricciones distintas sobre el mismo volumen.
            var sources = GetComponents<IWfcConstraintSource>();

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].ApplyConstraints(this, out string failure)) continue;

                ConstraintMessage = failure;
                Debug.LogWarning($"[WFC] Restricciones imposibles: {failure}", this);
                return;
            }
        }

        private Vector3 ComputeOrigin(WfcTopology topology)
        {
            if (!centerOnTransform) return transform.position;

            float cell = moduleSet.CellSize;

            return transform.position - new Vector3(
                (topology.SizeX - 1) * 0.5f,
                0f,
                (topology.SizeZ - 1) * 0.5f) * cell;
        }

        /// <summary>Un paso de solver mas su sincronizacion.</summary>
        public WfcSolverState Step()
        {
            if (!IsPrepared && !Prepare()) return WfcSolverState.Exhausted;

            var state = Solver.Step();
            SyncInstances();
            return state;
        }

        public WfcSolverState RunToCompletion()
        {
            if (!IsPrepared && !Prepare()) return WfcSolverState.Exhausted;

            var state = Solver.Solve();
            fullResync = true;
            SyncInstances();
            return state;
        }

        public void Regenerate()
        {
            if (Prepare() && !animate) RunToCompletion();
        }

        /// <summary>
        /// Configuracion programatica, para quien crea runners en caliente (el distrito de
        /// la sesion 10). Deja la ejecucion en manual: quien configura decide cuando resolver.
        /// </summary>
        public void Configure(
            WfcModuleSet set,
            WfcExclusionSet exclusionSet,
            Vector3Int volumeSize,
            WfcHeuristic heuristicMode,
            int backtrackLimit)
        {
            moduleSet = set;
            exclusions = exclusionSet;

            size = new Vector3Int(
                Mathf.Max(1, volumeSize.x),
                Mathf.Max(1, volumeSize.y),
                Mathf.Max(1, volumeSize.z));

            heuristic = heuristicMode;
            maxBacktracks = Mathf.Max(0, backtrackLimit);

            // El origen lo fija quien coloca: centrar aqui desalinearia la costura, que
            // razona en celdas de ciudad y da por hecho que la celda local (0,0,0) esta
            // en la posicion del transform.
            centerOnTransform = false;

            solveOnStart = false;
            animate = false;
        }

        public void RandomizeSeed()
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        public void Clear()
        {
            if (Solver != null)
            {
                Solver.CellCollapsed -= OnCellCollapsed;
                Solver.StateReverted -= OnStateReverted;
            }

            Solver = null;

            pool?.Dispose();
            pool = null;

            instances = null;
            instanceVariant = null;
            dirty.Clear();
            fullResync = false;

            DestroyContainer();
        }

        // ------------------------------------------------------------------ sincronizacion

        private void OnCellCollapsed(int cell, int variant) => dirty.Add(cell);

        /// <summary>
        /// Tras deshacer una rama no se sabe que celdas cambiaron, asi que se revisan todas.
        /// Es O(celdas) pero solo ocurre en backtracking, no en el caso normal.
        /// </summary>
        private void OnStateReverted() => fullResync = true;

        public void SyncInstances()
        {
            if (!IsPrepared) return;

            if (fullResync)
            {
                fullResync = false;
                dirty.Clear();

                for (int cell = 0; cell < instanceVariant.Length; cell++) ApplyCell(cell);
                return;
            }

            if (dirty.Count == 0) return;

            foreach (int cell in dirty) ApplyCell(cell);
            dirty.Clear();
        }

        private void ApplyCell(int cell)
        {
            int desired = Solver.GetCollapsed(cell);
            if (instanceVariant[cell] == desired) return;

            if (instances[cell] != null)
            {
                if (pool != null) pool.Release(instances[cell]);
                else DestroyObject(instances[cell]);

                instances[cell] = null;
            }

            instanceVariant[cell] = desired;
            if (desired < 0) return;

            var view = Adjacency.Baked.GetView(desired);
            if (view.IsAir) return;

            var parent = EnsureContainer();

            var instance = pool != null
                ? pool.Get(view.Prefab, Space.CellToWorld(cell), view.Rotation, parent)
                : Instantiate(view.Prefab, Space.CellToWorld(cell), view.Rotation, parent);

            Space.Topology.CoordsOf(cell, out int x, out int y, out int z);
            instance.name = $"{x},{y},{z} {view.DisplayName}";
            instance.hideFlags = HideFlags.DontSave;

            instances[cell] = instance;
            CellInstantiated?.Invoke(cell, desired, instance);
        }

        public WfcInstancePool Pool => pool;

        private const string ParkingName = "WFC Pool";

        private Transform EnsureParking()
        {
            if (parking != null) return parking;

            var existing = transform.Find(ParkingName);
            if (existing != null)
            {
                parking = existing;
                return parking;
            }

            var go = new GameObject(ParkingName);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            go.hideFlags = HideFlags.DontSave;

            parking = go.transform;
            return parking;
        }

        private Transform EnsureContainer()
        {
            if (container != null) return container;

            var existing = transform.Find(ContainerName);
            if (existing != null)
            {
                container = existing;
                return container;
            }

            var go = new GameObject(ContainerName);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSave;

            container = go.transform;
            return container;
        }

        private void DestroyContainer()
        {
            var existing = container != null ? container : transform.Find(ContainerName);
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
                container = null;
            }

            var park = parking != null ? parking : transform.Find(ParkingName);
            if (park != null)
            {
                DestroyObject(park.gameObject);
                parking = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        // ------------------------------------------------------------------ gizmos

        private void OnDrawGizmos()
        {
            if (!drawVolume) return;

            float cell = moduleSet != null ? moduleSet.CellSize : 1f;

            var topology = IsPrepared
                ? Solver.Topology
                : new WfcTopology(size.x, size.y, size.z);

            var space = IsPrepared ? Space : new WfcVolumeSpace(topology, cell, ComputeOrigin(topology));

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(space.Bounds.center, space.Bounds.size);

            if (!drawOpenCells || !IsPrepared) return;

            // Celdas todavia abiertas: cuanto mas opaca, menos opciones le quedan.
            for (int index = 0; index < topology.CellCount; index++)
            {
                if (Solver.GetCollapsed(index) >= 0) continue;

                int options = Solver.CountOptions(index);
                float t = Adjacency.VariantCount > 1
                    ? 1f - Mathf.Clamp01((float)(options - 1) / (Adjacency.VariantCount - 1))
                    : 1f;

                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.1f + t * 0.5f);
                Gizmos.DrawWireCube(space.CellToWorld(index), Vector3.one * cell * 0.85f);
            }
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            size.z = Mathf.Max(1, size.z);
        }
    }
}