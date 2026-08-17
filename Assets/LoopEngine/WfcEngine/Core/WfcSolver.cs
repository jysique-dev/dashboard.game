using System;
using System.Collections.Generic;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Solver de Wave Function Collapse. C# puro: sin UnityEngine, sin corrutinas,
    /// sin logging. Se avanza con Step() para animarlo o con Solve() para resolverlo entero.
    ///
    /// Propagacion: cuando el dominio de una celda cambia, para cada direccion se calcula
    /// la union de los vecinos permitidos por todas sus opciones vivas y se intersecta con
    /// el dominio del vecino. Es propagacion sana en el sentido estricto: solo elimina
    /// opciones demostrablemente imposibles. Por eso, si un dominio se vacia, la rama no
    /// tiene solucion y hay que retroceder de verdad, no reintentar.
    ///
    /// Retroceso por diario (WfcTrail) desde la sesion 9: cada decision anota una marca y
    /// deshacer repone los valores anteriores. Regla que sostiene todo: ninguna escritura
    /// sobre domains o collapsed ocurre fuera de WriteWord y MarkCollapsed.
    /// </summary>
    public sealed class WfcSolver
    {
        private sealed class Decision
        {
            public int Cell;
            public int Chosen;
            public int Remaining;
            public int WordMark;
            public int CollapseMark;
            public ulong[] Tried;
        }

        private readonly WfcNeighborMatrix matrix;
        private readonly WfcTopology topology;
        private readonly float[] weights;
        private readonly WfcSolverSettings settings;

        private readonly int variantCount;
        private readonly int words;

        private readonly ulong[] domains;
        private readonly int[] collapsed;
        private readonly float[] noise;
        private readonly float[] logWeights;

        private readonly ulong[] scratch;
        private readonly WfcTrail trail = new WfcTrail();
        private readonly Stack<int> pending = new Stack<int>();
        private readonly List<Decision> stack = new List<Decision>();
        private readonly Stack<Decision> pool = new Stack<Decision>();

        private WfcRandom random;
        private int remaining;

        public WfcSolverState State { get; private set; }
        public int Steps { get; private set; }
        public int Backtracks { get; private set; }
        public int Contradictions { get; private set; }

        /// <summary>Celdas sacadas de la cola de propagacion. La medida real del trabajo hecho.</summary>
        public int Propagations { get; private set; }

        public int MaxDepth { get; private set; }
        public int TrailPeak => trail.PeakWords;

        public WfcTopology Topology => topology;
        public int VariantCount => variantCount;
        public int RemainingCells => remaining;
        public int DecisionDepth => stack.Count;

        /// <summary>
        /// Se dispara al fijar una celda. Es optimista: si luego hay backtracking, esa
        /// celda puede dejar de estar fijada. Quien lo escuche debe atender tambien a
        /// StateReverted y resincronizar leyendo GetCollapsed, no acumular a ciegas.
        /// </summary>
        public event Action<int, int> CellCollapsed;

        /// <summary>Se dispara tras deshacer una rama. El estado visible ha retrocedido.</summary>
        public event Action StateReverted;

        public WfcSolver(
            WfcNeighborMatrix matrix,
            WfcTopology topology,
            float[] weights,
            WfcSolverSettings settings = null)
        {
            this.matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            this.topology = topology ?? throw new ArgumentNullException(nameof(topology));
            this.settings = settings ?? new WfcSolverSettings();

            variantCount = matrix.VariantCount;
            words = WfcBitSet.WordsFor(variantCount);

            this.weights = new float[variantCount];
            logWeights = new float[variantCount];

            for (int i = 0; i < variantCount; i++)
            {
                float w = (weights != null && i < weights.Length) ? weights[i] : 1f;
                if (w < 0f) w = 0f;

                this.weights[i] = w;
                logWeights[i] = w > 0f ? (float)Math.Log(w) : 0f;
            }

            domains = new ulong[topology.CellCount * words];
            collapsed = new int[topology.CellCount];
            noise = new float[topology.CellCount];
            scratch = new ulong[words];

            Reset();
        }

        public void Reset()
        {
            random = new WfcRandom(settings.Seed);
            trail.Clear();

            for (int cell = 0; cell < topology.CellCount; cell++)
            {
                WfcBitSet.FillRange(domains, cell * words, words, variantCount);
                collapsed[cell] = -1;
                noise[cell] = random.NextFloat() * settings.TieBreakNoise;
            }

            remaining = topology.CellCount;
            Steps = 0;
            Backtracks = 0;
            Contradictions = 0;
            Propagations = 0;
            MaxDepth = 0;
            State = WfcSolverState.Running;

            stack.Clear();
            pending.Clear();
        }

        // ------------------------------------------------------------------ consulta

        public int DomainOffset(int cell) => cell * words;

        public int GetCollapsed(int cell) => collapsed[cell];

        public int CountOptions(int cell) => WfcBitSet.PopCount(domains, cell * words, words);

        public bool IsPossible(int cell, int variant) => WfcBitSet.Get(domains, cell * words, variant);

        public int FirstOption(int cell) => WfcBitSet.FirstSetBit(domains, cell * words, words);

        public int NextOption(int cell, int from)
            => WfcBitSet.NextSetBit(domains, cell * words, words, from);

        // ------------------------------------------------------------------ escritura vigilada

        /// <summary>Unica puerta de escritura sobre domains. Anota el valor previo en el diario.</summary>
        private void WriteWord(int wordIndex, ulong value)
        {
            ulong previous = domains[wordIndex];
            if (previous == value) return;

            trail.RecordWord(wordIndex, previous);
            domains[wordIndex] = value;
        }

        private bool AndIntoDomain(int cellOffset, ulong[] source, int sourceOffset)
        {
            bool changed = false;

            for (int i = 0; i < words; i++)
            {
                ulong before = domains[cellOffset + i];
                ulong after = before & source[sourceOffset + i];

                if (after == before) continue;

                trail.RecordWord(cellOffset + i, before);
                domains[cellOffset + i] = after;
                changed = true;
            }

            return changed;
        }

        private void CollapseDomainTo(int cellOffset, int variant)
        {
            for (int i = 0; i < words; i++)
            {
                ulong value = i == (variant >> 6) ? 1UL << (variant & 63) : 0UL;
                WriteWord(cellOffset + i, value);
            }
        }

        // ------------------------------------------------------------------ restricciones previas

        /// <summary>Fija una celda antes de resolver. false si genera contradiccion.</summary>
        public bool Fix(int cell, int variant)
        {
            if (!IsPossible(cell, variant)) return false;

            CollapseDomainTo(cell * words, variant);
            MarkCollapsed(cell, variant);

            pending.Clear();
            pending.Push(cell);
            return Propagate();
        }

        /// <summary>Prohibe una opcion en una celda. false si la deja sin opciones.</summary>
        public bool Ban(int cell, int variant)
        {
            int offset = cell * words;
            if (!WfcBitSet.Get(domains, offset, variant)) return true;

            int wordIndex = offset + (variant >> 6);
            WriteWord(wordIndex, domains[wordIndex] & ~(1UL << (variant & 63)));

            if (WfcBitSet.IsEmpty(domains, offset, words)) return false;

            CheckSettled(cell);

            pending.Clear();
            pending.Push(cell);
            return Propagate();
        }

        /// <summary>Intersecta el dominio de una celda con una mascara. false si la vacia.</summary>
        public bool Restrict(int cell, ulong[] mask, int maskOffset)
        {
            int offset = cell * words;

            if (!AndIntoDomain(offset, mask, maskOffset)) return true;
            if (WfcBitSet.IsEmpty(domains, offset, words)) return false;

            CheckSettled(cell);

            pending.Clear();
            pending.Push(cell);
            return Propagate();
        }

        // ------------------------------------------------------------------ resolucion

        /// <summary>Un colapso con exito y toda su propagacion. Devuelve el estado resultante.</summary>
        public WfcSolverState Step()
        {
            if (State != WfcSolverState.Running) return State;

            if (remaining == 0)
            {
                State = WfcSolverState.Solved;
                return State;
            }

            int cell = SelectCell();
            if (cell < 0)
            {
                State = WfcSolverState.Solved;
                return State;
            }

            PushDecision(cell);

            while (stack.Count > 0)
            {
                var decision = stack[stack.Count - 1];

                // Cada intento arranca del estado exacto previo a esta decision.
                Restore(decision);

                int variant = PickUntried(decision);

                if (variant < 0)
                {
                    PopDecision();

                    if (stack.Count == 0)
                    {
                        State = WfcSolverState.Exhausted;
                        return State;
                    }

                    var parent = stack[stack.Count - 1];
                    WfcBitSet.Set(parent.Tried, 0, parent.Chosen);

                    if (!CountBacktrack()) return State;
                    continue;
                }

                decision.Chosen = variant;
                WfcBitSet.Set(decision.Tried, 0, variant);

                CollapseDomainTo(decision.Cell * words, variant);
                MarkCollapsed(decision.Cell, variant);

                pending.Clear();
                pending.Push(decision.Cell);

                if (Propagate())
                {
                    Steps++;
                    return State;
                }

                Contradictions++;
                if (!CountBacktrack()) return State;
            }

            State = WfcSolverState.Exhausted;
            return State;
        }

        public WfcSolverState Solve()
        {
            while (State == WfcSolverState.Running)
            {
                if (settings.MaxSteps > 0 && Steps >= settings.MaxSteps)
                {
                    State = WfcSolverState.LimitReached;
                    break;
                }

                Step();
            }

            return State;
        }

        /// <summary>
        /// Avanza mientras el predicado lo permita. Sirve para trocear el trabajo por
        /// presupuesto de tiempo sin que el nucleo sepa que es un frame ni un Stopwatch.
        /// </summary>
        public WfcSolverState SolveWhile(Func<bool> canContinue)
        {
            if (canContinue == null) return Solve();

            while (State == WfcSolverState.Running && canContinue())
            {
                if (settings.MaxSteps > 0 && Steps >= settings.MaxSteps)
                {
                    State = WfcSolverState.LimitReached;
                    break;
                }

                Step();
            }

            return State;
        }

        private bool CountBacktrack()
        {
            Backtracks++;

            if (settings.MaxBacktracks > 0 && Backtracks > settings.MaxBacktracks)
            {
                State = WfcSolverState.LimitReached;
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ seleccion

        private int SelectCell()
        {
            int best = -1;
            float bestScore = float.MaxValue;
            int fallback = -1;

            for (int cell = 0; cell < topology.CellCount; cell++)
            {
                if (collapsed[cell] >= 0) continue;

                int options = CountOptions(cell);

                // Una celda con una sola opcion no es una decision: se asienta sola.
                if (options <= 1)
                {
                    if (fallback < 0) fallback = cell;
                    continue;
                }

                if (settings.Heuristic == WfcHeuristic.Scanline) return cell;

                float score = settings.Heuristic == WfcHeuristic.FewestOptions
                    ? options
                    : Entropy(cell);

                score += noise[cell];

                if (score < bestScore)
                {
                    bestScore = score;
                    best = cell;
                }
            }

            return best >= 0 ? best : fallback;
        }

        /// <summary>Entropia de Shannon ponderada del dominio de la celda.</summary>
        private float Entropy(int cell)
        {
            int offset = cell * words;

            float sum = 0f;
            float sumLog = 0f;

            for (int v = WfcBitSet.NextSetBit(domains, offset, words, 0);
                 v >= 0;
                 v = WfcBitSet.NextSetBit(domains, offset, words, v + 1))
            {
                float w = weights[v];
                if (w <= 0f) continue;

                sum += w;
                sumLog += w * logWeights[v];
            }

            if (sum <= 0f) return 0f;

            return (float)Math.Log(sum) - sumLog / sum;
        }

        private int PickUntried(Decision decision)
        {
            int offset = decision.Cell * words;

            float total = 0f;

            for (int v = WfcBitSet.NextSetBit(domains, offset, words, 0);
                 v >= 0;
                 v = WfcBitSet.NextSetBit(domains, offset, words, v + 1))
            {
                if (WfcBitSet.Get(decision.Tried, 0, v)) continue;
                total += weights[v];
            }

            if (total <= 0f)
            {
                // Todos los candidatos restantes tienen peso 0: se elige el primero,
                // porque "peso 0" significa poco probable, no prohibido.
                for (int v = WfcBitSet.NextSetBit(domains, offset, words, 0);
                     v >= 0;
                     v = WfcBitSet.NextSetBit(domains, offset, words, v + 1))
                {
                    if (!WfcBitSet.Get(decision.Tried, 0, v)) return v;
                }

                return -1;
            }

            float pick = random.NextFloat() * total;

            int last = -1;
            for (int v = WfcBitSet.NextSetBit(domains, offset, words, 0);
                 v >= 0;
                 v = WfcBitSet.NextSetBit(domains, offset, words, v + 1))
            {
                if (WfcBitSet.Get(decision.Tried, 0, v)) continue;

                last = v;
                pick -= weights[v];
                if (pick <= 0f) return v;
            }

            return last;
        }

        // ------------------------------------------------------------------ propagacion

        private bool Propagate()
        {
            while (pending.Count > 0)
            {
                int cell = pending.Pop();
                int cellOffset = cell * words;
                Propagations++;

                // Atajo del caso dominante: casi toda la propagacion sale de celdas ya
                // fijadas, y ahi la union es una sola fila de la matriz. Evita recorrer
                // bits y escribir en scratch para nada.
                int single = collapsed[cell] >= 0 ? collapsed[cell] : -1;

                for (int d = 0; d < WfcDirections.Count; d++)
                {
                    var direction = (WfcDirection)d;

                    if (!topology.TryNeighbor(cell, direction, out int neighbor)) continue;

                    ulong[] source;
                    int sourceOffset;

                    if (single >= 0)
                    {
                        source = matrix.Data;
                        sourceOffset = matrix.RowOffset(direction, single);
                    }
                    else
                    {
                        WfcBitSet.ClearAll(scratch, 0, words);

                        for (int v = WfcBitSet.NextSetBit(domains, cellOffset, words, 0);
                             v >= 0;
                             v = WfcBitSet.NextSetBit(domains, cellOffset, words, v + 1))
                        {
                            WfcBitSet.OrInto(scratch, 0, matrix.Data, matrix.RowOffset(direction, v), words);
                        }

                        source = scratch;
                        sourceOffset = 0;
                    }

                    int neighborOffset = neighbor * words;

                    if (!AndIntoDomain(neighborOffset, source, sourceOffset)) continue;

                    if (WfcBitSet.IsEmpty(domains, neighborOffset, words)) return false;

                    CheckSettled(neighbor);
                    pending.Push(neighbor);
                }
            }

            return true;
        }

        private void CheckSettled(int cell)
        {
            if (collapsed[cell] >= 0) return;
            if (CountOptions(cell) != 1) return;

            MarkCollapsed(cell, FirstOption(cell));
        }

        private void MarkCollapsed(int cell, int variant)
        {
            if (collapsed[cell] >= 0) return;

            trail.RecordCollapse(cell);
            collapsed[cell] = variant;
            remaining--;

            CellCollapsed?.Invoke(cell, variant);
        }

        // ------------------------------------------------------------------ pila de decisiones

        private void PushDecision(int cell)
        {
            var decision = pool.Count > 0
                ? pool.Pop()
                : new Decision { Tried = new ulong[words] };

            decision.Cell = cell;
            decision.Chosen = -1;
            decision.Remaining = remaining;
            decision.WordMark = trail.WordMark;
            decision.CollapseMark = trail.CollapseMark;

            WfcBitSet.ClearAll(decision.Tried, 0, words);

            stack.Add(decision);
            if (stack.Count > MaxDepth) MaxDepth = stack.Count;
        }

        private void PopDecision()
        {
            var decision = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            pool.Push(decision);
        }

        private void Restore(Decision decision)
        {
            trail.UndoTo(domains, collapsed, decision.WordMark, decision.CollapseMark);
            remaining = decision.Remaining;

            StateReverted?.Invoke();
        }
    }
}