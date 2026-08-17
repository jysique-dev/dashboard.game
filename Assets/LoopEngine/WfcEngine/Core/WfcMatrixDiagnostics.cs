using System.Collections.Generic;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>Una variante que no puede colocarse porque alguna cara no encuentra vecino.</summary>
    public readonly struct WfcDeadVariant
    {
        public readonly int VariantIndex;
        public readonly WfcDirection Direction;

        public WfcDeadVariant(int variantIndex, WfcDirection direction)
        {
            VariantIndex = variantIndex;
            Direction = direction;
        }
    }

    /// <summary>Un socket que aparece en alguna cara pero cuya contraparte no existe en el set.</summary>
    public readonly struct WfcUnmatchedSocket
    {
        public readonly WfcSocketDescriptor Socket;
        public readonly WfcSocketDescriptor RequiredCounterpart;
        public readonly int UsageCount;

        public WfcUnmatchedSocket(
            WfcSocketDescriptor socket, WfcSocketDescriptor requiredCounterpart, int usageCount)
        {
            Socket = socket;
            RequiredCounterpart = requiredCounterpart;
            UsageCount = usageCount;
        }
    }

    /// <summary>
    /// Analisis de la matriz antes de escribir una sola linea de solver.
    ///
    /// El sintoma clasico de un set mal autorado es un solver que falla mucho y del que
    /// no se sabe por que. Casi siempre la causa es un socket sin contraparte: alguien
    /// declaro simetrico lo que deberia ser asimetrico, o al reves. Eso se ve aqui en un
    /// segundo, y no despues de veinte colapsos fallidos.
    /// </summary>
    public static class WfcMatrixDiagnostics
    {
        /// <summary>Variantes con al menos una direccion sin ningun vecino posible.</summary>
        public static void FindDeadVariants(
            WfcNeighborMatrix matrix, List<WfcDeadVariant> results)
        {
            for (int variant = 0; variant < matrix.VariantCount; variant++)
            {
                for (int d = 0; d < WfcDirections.Count; d++)
                {
                    var direction = (WfcDirection)d;

                    if (matrix.CountAllowed(direction, variant) == 0)
                    {
                        results.Add(new WfcDeadVariant(variant, direction));
                    }
                }
            }
        }

        /// <summary>
        /// Sockets usados en alguna cara cuya contraparte requerida no aparece en ninguna
        /// cara opuesta del set. Es la causa raiz de la mayoria de variantes muertas.
        /// </summary>
        public static void FindUnmatchedSockets(
            WfcBakedModules modules, List<WfcUnmatchedSocket> results)
        {
            // Que sockets existen en cada cara, y cuantas veces.
            var present = new HashSet<WfcSocketDescriptor>[WfcDirections.Count];
            var usage = new Dictionary<WfcSocketDescriptor, int>();

            for (int d = 0; d < WfcDirections.Count; d++) present[d] = new HashSet<WfcSocketDescriptor>();

            for (int variant = 0; variant < modules.Count; variant++)
            {
                for (int d = 0; d < WfcDirections.Count; d++)
                {
                    var socket = modules.GetSocket(variant, (WfcDirection)d);
                    if (!socket.IsValid) continue;

                    present[d].Add(socket);
                    usage.TryGetValue(socket, out int count);
                    usage[socket] = count + 1;
                }
            }

            var reported = new HashSet<WfcSocketDescriptor>();

            for (int d = 0; d < WfcDirections.Count; d++)
            {
                var direction = (WfcDirection)d;
                int opposite = (int)WfcDirections.Opposite(direction);

                foreach (var socket in present[d])
                {
                    var required = WfcSocketCompatibility.RequiredCounterpart(socket);

                    if (present[opposite].Contains(required)) continue;
                    if (!reported.Add(socket)) continue;

                    usage.TryGetValue(socket, out int count);
                    results.Add(new WfcUnmatchedSocket(socket, required, count));
                }
            }
        }

        /// <summary>Numero medio de vecinos posibles por cara. Indicador de holgura del set.</summary>
        public static float AverageOptionsPerFace(WfcNeighborMatrix matrix)
        {
            if (matrix.VariantCount == 0) return 0f;

            long total = 0;
            for (int d = 0; d < WfcDirections.Count; d++)
            {
                for (int variant = 0; variant < matrix.VariantCount; variant++)
                {
                    total += matrix.CountAllowed((WfcDirection)d, variant);
                }
            }

            return (float)((double)total / (matrix.VariantCount * WfcDirections.Count));
        }
    }
}