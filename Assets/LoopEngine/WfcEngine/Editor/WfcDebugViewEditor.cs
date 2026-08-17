using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 9: etiquetas de opciones sobre la superposicion, y un
    /// banco de medida que resuelve varias veces y da la media.
    ///
    /// Una sola medicion no dice nada: el numero de backtracks varia mucho de una semilla
    /// a otra. Se promedian varias corridas y se informa tambien del peor caso, que es lo
    /// que va a determinar si el sistema sirve en runtime.
    /// </summary>
    [CustomEditor(typeof(WfcDebugView))]
    public class WfcDebugViewEditor : UnityEditor.Editor
    {
        private int sampleCount = 10;
        private string report;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (WfcDebugView)target;
            var runner = view.GetComponent<WfcRunner>();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Medida", EditorStyles.boldLabel);

            sampleCount = EditorGUILayout.IntSlider("Corridas", sampleCount, 1, 100);

            using (new EditorGUI.DisabledScope(runner == null || runner.ModuleSet == null))
            {
                if (GUILayout.Button("Medir con semillas consecutivas"))
                {
                    report = Benchmark(runner, sampleCount);
                }
            }

            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.HelpBox(report, MessageType.None);
            }

            if (runner != null && runner.IsPrepared)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Estado actual", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(WfcAsciiRenderer.RenderStats(runner.Solver), EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    $"propagaciones {runner.Solver.Propagations} · " +
                    $"profundidad max {runner.Solver.MaxDepth} · " +
                    $"pico de diario {runner.Solver.TrailPeak} palabras",
                    EditorStyles.miniLabel);

                if (runner.Pool != null)
                {
                    EditorGUILayout.LabelField(runner.Pool.Describe(), EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>
        /// Mide sobre el solver puro, sin instanciar nada: mezclar el coste de Unity con el
        /// del algoritmo haria imposible saber cual de los dos hay que arreglar.
        /// </summary>
        private static string Benchmark(WfcRunner runner, int samples)
        {
            var adjacency = WfcAdjacencyBuilder.Build(runner.ModuleSet, null);
            if (adjacency.Matrix == null) return "No hay matriz que medir.";

            int originalSeed = runner.Seed;

            var topology = runner.IsPrepared
                ? runner.Solver.Topology
                : new WfcTopology(6, 4, 6);

            var weights = new float[adjacency.VariantCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = adjacency.Baked.Core.GetWeight(i);

            int solved = 0;
            long totalTicks = 0;
            long worstTicks = 0;
            int totalBacktracks = 0;
            int worstBacktracks = 0;
            int totalPropagations = 0;
            int peakTrail = 0;

            for (int i = 0; i < samples; i++)
            {
                var solver = new WfcSolver(adjacency.Matrix, topology, weights, new WfcSolverSettings
                {
                    Seed = unchecked((uint)(originalSeed + i)),
                    MaxBacktracks = 20000
                });

                var watch = Stopwatch.StartNew();
                var state = solver.Solve();
                watch.Stop();

                if (state == WfcSolverState.Solved) solved++;

                totalTicks += watch.ElapsedTicks;
                if (watch.ElapsedTicks > worstTicks) worstTicks = watch.ElapsedTicks;

                totalBacktracks += solver.Backtracks;
                if (solver.Backtracks > worstBacktracks) worstBacktracks = solver.Backtracks;

                totalPropagations += solver.Propagations;
                if (solver.TrailPeak > peakTrail) peakTrail = solver.TrailPeak;
            }

            double msPerTick = 1000.0 / Stopwatch.Frequency;

            return
                $"{topology} · {adjacency.VariantCount} variantes · {samples} corridas\n" +
                $"exito: {solved}/{samples}\n" +
                $"tiempo: {totalTicks * msPerTick / samples:0.00} ms de media, " +
                $"{worstTicks * msPerTick:0.00} ms el peor\n" +
                $"backtracks: {(float)totalBacktracks / samples:0.0} de media, {worstBacktracks} el peor\n" +
                $"propagaciones: {totalPropagations / samples} por corrida\n" +
                $"pico de diario: {peakTrail} palabras ({peakTrail * 8 / 1024f:0.0} KB)";
        }

        private void OnSceneGUI()
        {
            var view = (WfcDebugView)target;
            var runner = view.GetComponent<WfcRunner>();

            if (runner == null || !runner.IsPrepared) return;
            if (view.Mode != WfcOverlayMode.Entropy) return;

            var solver = runner.Solver;
            var space = runner.Space;
            var topology = solver.Topology;

            // Con volumenes grandes las etiquetas se vuelven ilegibles y caras de dibujar.
            if (topology.CellCount > 600) return;

            Handles.color = Color.white;

            for (int index = 0; index < topology.CellCount; index++)
            {
                if (solver.GetCollapsed(index) >= 0) continue;

                topology.CoordsOf(index, out _, out int y, out _);
                if (view.OnlyLayer >= 0 && y != view.OnlyLayer) continue;

                Handles.Label(space.CellToWorld(index), solver.CountOptions(index).ToString());
            }
        }
    }
}