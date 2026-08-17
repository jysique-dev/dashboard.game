namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Regla unica de compatibilidad entre dos caras enfrentadas.
    /// Todo el resto del engine (matriz de vecindad, solver, editor visual)
    /// pregunta aqui; no hay una segunda copia de esta logica en ningun sitio.
    ///
    /// Regla (marian42.de/article/wfc): los modulos adyacentes deben tener el mismo
    /// numero de conector y su simetria debe coincidir: mismo indice de rotacion en
    /// vertical, par flipped / not-flipped en horizontal, o bien ser simetricos /
    /// invariantes.
    /// </summary>
    public static class WfcSocketCompatibility
    {
        /// <param name="a">Socket de la cara del modulo A.</param>
        /// <param name="b">Socket de la cara opuesta del modulo B.</param>
        public static bool AreCompatible(WfcSocketDescriptor a, WfcSocketDescriptor b)
        {
            // Un socket invalido no encaja con nada, ni siquiera con otro invalido:
            // asi los datos incompletos fallan visiblemente en vez de generar basura.
            if (!a.IsValid || !b.IsValid) return false;

            if (a.Id != b.Id) return false;
            if (a.Kind != b.Kind) return false;

            switch (a.Kind)
            {
                case WfcSocketKind.HorizontalSymmetric:
                    return true;

                case WfcSocketKind.HorizontalAsymmetric:
                    // Uno espejado y el otro no.
                    return a.Flipped != b.Flipped;

                case WfcSocketKind.VerticalInvariant:
                    return true;

                case WfcSocketKind.VerticalRotational:
                    return a.RotationIndex == b.RotationIndex;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Comprueba ademas que ambos sockets sean del tipo adecuado para el eje
        /// por el que se conectan. Util para validacion de autoria.
        /// </summary>
        public static bool AreCompatibleAlong(WfcSocketDescriptor a, WfcSocketDescriptor b, WfcDirection directionFromAToB)
        {
            if (!WfcSocketKinds.FitsDirection(a.Kind, directionFromAToB)) return false;
            if (!WfcSocketKinds.FitsDirection(b.Kind, WfcDirections.Opposite(directionFromAToB))) return false;
            return AreCompatible(a, b);
        }

        /// <summary>
        /// El socket que debe tener la cara enfrentada para encajar con este.
        /// Existe uno solo por construccion, lo que hace trivial el editor visual
        /// de vecinos de la sesion 5.
        /// </summary>
        public static WfcSocketDescriptor RequiredCounterpart(WfcSocketDescriptor socket)
        {
            if (!socket.IsValid) return WfcSocketDescriptor.Invalid;

            switch (socket.Kind)
            {
                case WfcSocketKind.HorizontalAsymmetric:
                    return socket.Mirrored();
                default:
                    return socket;
            }
        }
    }
}