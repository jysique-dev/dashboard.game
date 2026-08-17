using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    public enum WfcOverlayMode
    {
        /// <summary>Calor por entropia: cuanto mas rojo, menos opciones le quedan a la celda.</summary>
        Entropy = 0,

        /// <summary>Solo las celdas ya fijadas, con el color del socket de su cara superior.</summary>
        Collapsed = 1,

        /// <summary>Solo celdas problematicas: sin opciones, o con una sola sin fijar.</summary>
        Problems = 2
    }

    /// <summary>
    /// Superposicion de depuracion sobre el volumen del runner. Lee el estado del solver
    /// en cada gizmo, no guarda copia: lo que se ve es el estado actual, incluso a mitad
    /// de una animacion o despues de un backtracking.
    ///
    /// Sirve sobre todo para entender por que un colapso se atasca: la mancha roja senala
    /// donde se estrecha el problema, que casi siempre esta lejos de donde acaba fallando.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Debug View")]
    [RequireComponent(typeof(WfcRunner))]
    public class WfcDebugView : MonoBehaviour
    {
        [SerializeField] private WfcOverlayMode mode = WfcOverlayMode.Entropy;

        [Tooltip("-1 = todas las capas.")]
        [SerializeField] private int onlyLayer = -1;

        [SerializeField, Range(0.1f, 1f)] private float cubeScale = 0.8f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;
        [SerializeField] private bool drawWhenNotSelected = true;

        private WfcRunner runner;

        public WfcOverlayMode Mode => mode;
        public int OnlyLayer => onlyLayer;

        private WfcRunner Runner => runner != null ? runner : (runner = GetComponent<WfcRunner>());

        private void OnDrawGizmos()
        {
            if (drawWhenNotSelected) Draw();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawWhenNotSelected) Draw();
        }

        private void Draw()
        {
            var target = Runner;
            if (target == null || !target.IsPrepared) return;

            var solver = target.Solver;
            var space = target.Space;
            var topology = solver.Topology;

            float cell = space.CellSize * cubeScale;
            int variants = solver.VariantCount;

            for (int index = 0; index < topology.CellCount; index++)
            {
                topology.CoordsOf(index, out _, out int y, out _);
                if (onlyLayer >= 0 && y != onlyLayer) continue;

                if (!TryColor(solver, index, variants, out var color)) continue;

                Gizmos.color = color;
                Gizmos.DrawWireCube(space.CellToWorld(index), Vector3.one * cell);
            }
        }

        private bool TryColor(WfcSolver solver, int index, int variants, out Color color)
        {
            int options = solver.CountOptions(index);
            bool isCollapsed = solver.GetCollapsed(index) >= 0;

            switch (mode)
            {
                case WfcOverlayMode.Collapsed:
                    if (!isCollapsed)
                    {
                        color = default;
                        return false;
                    }

                    color = new Color(0.35f, 0.8f, 0.5f, opacity);
                    return true;

                case WfcOverlayMode.Problems:
                    if (options == 0)
                    {
                        color = new Color(1f, 0.2f, 0.2f, Mathf.Max(opacity, 0.8f));
                        return true;
                    }

                    if (options == 1 && !isCollapsed)
                    {
                        color = new Color(1f, 0.65f, 0.1f, opacity);
                        return true;
                    }

                    color = default;
                    return false;

                default:
                    if (isCollapsed)
                    {
                        color = default;
                        return false;
                    }

                    // 1 opcion = rojo, todas las opciones = azul.
                    float t = variants > 1
                        ? 1f - Mathf.Clamp01((float)(options - 1) / (variants - 1))
                        : 1f;

                    color = new Color(t, 0.4f + 0.3f * (1f - t), 1f - t, opacity);
                    return true;
            }
        }
    }
}