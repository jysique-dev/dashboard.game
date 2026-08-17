using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 7: botones de Paso, Ejecutar y Animar sobre el runner,
    /// con barra de progreso y estadisticas.
    ///
    /// La animacion en editor no usa corrutinas (no existen fuera de play mode): se engancha
    /// a EditorApplication.update y gasta un presupuesto de milisegundos por tick, para que
    /// el editor siga respondiendo aunque el volumen sea grande.
    /// </summary>
    [CustomEditor(typeof(WfcRunner))]
    public class WfcRunnerEditor : UnityEditor.Editor
    {
        private static WfcRunner animating;
        private static double budgetMilliseconds = 8.0;

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (animating == (WfcRunner)target) animating = null;
        }

        private static void OnEditorUpdate()
        {
            if (animating == null) return;

            if (!animating.IsPrepared || animating.State != WfcSolverState.Running)
            {
                animating = null;
                SceneView.RepaintAll();
                return;
            }

            double deadline = EditorApplication.timeSinceStartup + budgetMilliseconds / 1000.0;

            while (EditorApplication.timeSinceStartup < deadline)
            {
                if (animating.Solver.Step() != WfcSolverState.Running) break;
            }

            animating.SyncInstances();
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var runner = (WfcRunner)target;

            EditorGUILayout.Space(8);
            DrawControls(runner);

            EditorGUILayout.Space(6);
            DrawStatus(runner);

            EditorGUILayout.Space(4);
            DrawIssues(runner);
        }

        private void DrawControls(WfcRunner runner)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preparar"))
                {
                    animating = null;
                    if (!runner.Prepare())
                    {
                        Debug.LogWarning("No se pudo preparar: revisa el Module Set.", runner);
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Limpiar"))
                {
                    animating = null;
                    runner.Clear();
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!runner.IsPrepared))
                {
                    if (GUILayout.Button("Paso"))
                    {
                        animating = null;
                        runner.Step();
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Ejecutar"))
                    {
                        animating = null;
                        runner.RunToCompletion();
                        SceneView.RepaintAll();
                    }

                    if (animating == runner)
                    {
                        if (GUILayout.Button("Detener")) animating = null;
                    }
                    else if (GUILayout.Button("Animar"))
                    {
                        animating = runner;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Semilla aleatoria y regenerar"))
                {
                    Undo.RecordObject(runner, "Randomize WFC seed");
                    runner.RandomizeSeed();
                    animating = null;
                    runner.Prepare();
                    runner.RunToCompletion();
                    EditorUtility.SetDirty(runner);
                    SceneView.RepaintAll();
                }
            }

            budgetMilliseconds = EditorGUILayout.Slider(
                "Presupuesto por tick (ms)", (float)budgetMilliseconds, 1f, 33f);
        }

        private void DrawStatus(WfcRunner runner)
        {
            if (!runner.IsPrepared)
            {
                EditorGUILayout.HelpBox("Sin preparar. Pulsa Preparar.", MessageType.Info);
                return;
            }

            var solver = runner.Solver;

            var rect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(rect, runner.Progress, $"{runner.Progress * 100f:0}%");

            EditorGUILayout.LabelField(WfcAsciiRenderer.RenderStats(solver), EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                $"{runner.Adjacency.VariantCount} variantes · " +
                $"{runner.Adjacency.AverageOptionsPerFace():0.0} opciones por cara",
                EditorStyles.miniLabel);

            switch (solver.State)
            {
                case WfcSolverState.Solved:
                    EditorGUILayout.HelpBox("Resuelto.", MessageType.Info);
                    break;

                case WfcSolverState.Exhausted:
                    EditorGUILayout.HelpBox(
                        "Sin solucion: se agotaron las ramas. La propagacion solo descarta " +
                        "opciones imposibles, asi que esto significa que el set no admite " +
                        "ninguna configuracion para este volumen. Revisa la ventana Neighbor Matrix.",
                        MessageType.Error);
                    break;

                case WfcSolverState.LimitReached:
                    EditorGUILayout.HelpBox(
                        "Se alcanzo el tope de backtracks. Puede que exista solucion pero cueste " +
                        "encontrarla: sube el tope, o afloja el set anadiendo modulos de transicion.",
                        MessageType.Warning);
                    break;
            }
        }

        private void DrawIssues(WfcRunner runner)
        {
            if (runner.Adjacency == null) return;

            int shown = 0;

            foreach (var issue in runner.Adjacency.Issues)
            {
                if (issue.Severity == WfcIssueSeverity.Info) continue;
                if (shown++ >= 6) break;

                EditorGUILayout.HelpBox(
                    issue.Message, WfcModuleDefinitionEditor.ToMessageType(issue.Severity));
            }

            if (shown > 0 && GUILayout.Button("Abrir Neighbor Matrix"))
            {
                WfcMatrixWindow.Open();
            }
        }
    }
}