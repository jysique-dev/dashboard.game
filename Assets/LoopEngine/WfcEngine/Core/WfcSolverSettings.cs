namespace LoopEngine.WfcEngine.Core
{
    public enum WfcHeuristic
    {
        /// <summary>Entropia de Shannon con pesos. La eleccion habitual.</summary>
        ShannonEntropy = 0,

        /// <summary>Simplemente la celda con menos opciones. Mas rapida, ignora los pesos.</summary>
        FewestOptions = 1,

        /// <summary>Orden de indice. Determinista y trivial; util para depurar.</summary>
        Scanline = 2
    }

    public enum WfcSolverState
    {
        Running = 0,
        Solved = 1,

        /// <summary>Se agotaron las ramas: el problema no tiene solucion con estos datos.</summary>
        Exhausted = 2,

        /// <summary>Se alcanzo el limite de backtracks o de pasos antes de terminar.</summary>
        LimitReached = 3
    }

    public sealed class WfcSolverSettings
    {
        public uint Seed = 1u;

        public WfcHeuristic Heuristic = WfcHeuristic.ShannonEntropy;

        /// <summary>0 = sin limite. Un tope evita que un set mal autorado cuelgue el editor.</summary>
        public int MaxBacktracks = 10000;

        public int MaxSteps = 0;

        /// <summary>
        /// Amplitud del ruido de desempate. Se calcula una vez por celda al inicializar,
        /// no en cada comparacion: asi el orden de visita no depende de cuantas veces se
        /// haya evaluado una celda, que es una fuente clasica de no determinismo.
        /// </summary>
        public float TieBreakNoise = 1e-3f;

        public WfcSolverSettings Clone() => new WfcSolverSettings
        {
            Seed = Seed,
            Heuristic = Heuristic,
            MaxBacktracks = MaxBacktracks,
            MaxSteps = MaxSteps,
            TieBreakNoise = TieBreakNoise
        };
    }
}