using System;
using System.Text;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Render de texto del estado del solver. Es la prueba visual de la sesion 6: se puede
    /// ver colapsar un volumen sin tener una sola escena de Unity montada, y sirve igual
    /// dentro de un test que en la consola.
    ///
    /// Celda colapsada: su glifo. Celda abierta: el numero de opciones (1-9, '+' si son mas).
    /// Celda sin opciones: '!'.
    /// </summary>
    public static class WfcAsciiRenderer
    {
        private const string DefaultGlyphs =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static char GlyphOf(int variantIndex)
            => variantIndex < 0 || variantIndex >= DefaultGlyphs.Length
                ? '?'
                : DefaultGlyphs[variantIndex];

        /// <summary>Una capa horizontal (Y constante) vista desde arriba. +X a la derecha, +Z hacia abajo.</summary>
        public static string RenderLayer(WfcSolver solver, int y, Func<int, char> glyphOf = null)
        {
            if (solver == null) return string.Empty;

            glyphOf = glyphOf ?? GlyphOf;

            var topology = solver.Topology;
            var builder = new StringBuilder();

            for (int z = 0; z < topology.SizeZ; z++)
            {
                for (int x = 0; x < topology.SizeX; x++)
                {
                    builder.Append(CellChar(solver, topology.Index(x, y, z), glyphOf));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>Todas las capas, de arriba abajo, que es como se lee un edificio.</summary>
        public static string RenderVolume(WfcSolver solver, Func<int, char> glyphOf = null)
        {
            if (solver == null) return string.Empty;

            var builder = new StringBuilder();

            for (int y = solver.Topology.SizeY - 1; y >= 0; y--)
            {
                builder.AppendLine($"--- y = {y} ---");
                builder.Append(RenderLayer(solver, y, glyphOf));
            }

            return builder.ToString();
        }

        public static string RenderStats(WfcSolver solver)
        {
            if (solver == null) return string.Empty;

            return $"{solver.State} · {solver.Steps} pasos · " +
                   $"{solver.Backtracks} backtracks · {solver.Contradictions} contradicciones · " +
                   $"{solver.RemainingCells} celdas abiertas · profundidad {solver.DecisionDepth}";
        }

        private static char CellChar(WfcSolver solver, int cell, Func<int, char> glyphOf)
        {
            int variant = solver.GetCollapsed(cell);
            if (variant >= 0) return glyphOf(variant);

            int options = solver.CountOptions(cell);
            if (options == 0) return '!';
            if (options <= 9) return (char)('0' + options);

            return '+';
        }
    }
}