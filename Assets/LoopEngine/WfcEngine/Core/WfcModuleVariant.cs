using System;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Un modulo ya girado: la unidad real con la que trabaja el solver.
    /// Un modulo de autoria con 4 rotaciones permitidas produce hasta 4 variantes.
    ///
    /// Inmutable y sin dependencias de Unity. El prefab y el Quaternion asociados
    /// viven en la capa Unity (WfcBakedVariant), no aqui.
    /// </summary>
    public readonly struct WfcModuleVariant : IEquatable<WfcModuleVariant>
    {
        /// <summary>Indice del modulo de autoria del que sale esta variante.</summary>
        public readonly int SourceIndex;

        /// <summary>Pasos de 90 grados aplicados alrededor de Y. 0..3.</summary>
        public readonly int RotationSteps;

        public readonly float Weight;

        private readonly WfcSocketDescriptor[] faces;

        public WfcModuleVariant(int sourceIndex, int rotationSteps, float weight, WfcSocketDescriptor[] faces)
        {
            if (faces == null || faces.Length != WfcDirections.Count)
            {
                throw new ArgumentException(
                    $"Una variante necesita exactamente {WfcDirections.Count} caras.", nameof(faces));
            }

            SourceIndex = sourceIndex;
            RotationSteps = ((rotationSteps % 4) + 4) % 4;
            Weight = weight;
            this.faces = faces;
        }

        public WfcSocketDescriptor GetSocket(WfcDirection direction) => faces[(int)direction];

        public WfcSocketDescriptor GetSocket(int directionIndex) => faces[directionIndex];

        /// <summary>
        /// Gira un juego de caras alrededor de Y.
        ///
        /// La cara que acaba mirando a 'd' es la que originalmente estaba en
        /// RotateAroundY(d, -steps). Los sockets verticales rotacionales avanzan su
        /// indice; los horizontales conservan identidad (girar no espeja, asi que
        /// 'flipped' nunca cambia aqui).
        /// </summary>
        public static WfcSocketDescriptor[] RotateFaces(WfcSocketDescriptor[] source, int steps)
        {
            var result = new WfcSocketDescriptor[WfcDirections.Count];

            for (int i = 0; i < WfcDirections.Count; i++)
            {
                var target = (WfcDirection)i;
                var origin = WfcDirections.RotateAroundY(target, -steps);
                result[i] = source[(int)origin].RotatedAroundY(steps);
            }

            return result;
        }

        public bool HasSameFaces(WfcModuleVariant other)
        {
            for (int i = 0; i < WfcDirections.Count; i++)
            {
                if (!faces[i].Equals(other.faces[i])) return false;
            }
            return true;
        }

        public bool Equals(WfcModuleVariant other)
            => SourceIndex == other.SourceIndex
               && RotationSteps == other.RotationSteps
               && HasSameFaces(other);

        public override bool Equals(object obj) => obj is WfcModuleVariant other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceIndex;
                hash = (hash * 397) ^ RotationSteps;
                for (int i = 0; i < WfcDirections.Count; i++)
                {
                    hash = (hash * 397) ^ faces[i].GetHashCode();
                }
                return hash;
            }
        }

        public string Describe()
        {
            var parts = new string[WfcDirections.Count];
            for (int i = 0; i < WfcDirections.Count; i++)
            {
                parts[i] = WfcDirections.ShortName((WfcDirection)i) + ":" + faces[i].Notation();
            }
            return $"m{SourceIndex}/r{RotationSteps} [{string.Join(" ", parts)}]";
        }

        public override string ToString() => Describe();
    }
}