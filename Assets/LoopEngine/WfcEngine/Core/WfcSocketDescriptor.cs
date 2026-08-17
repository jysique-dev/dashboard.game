namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Un socket ya resuelto: identidad estable + tipo + modificador aplicado.
    /// Es el dato que consume el solver. Inmutable y sin dependencias de Unity.
    ///
    /// El Id proviene de la libreria de sockets y es estable frente a borrados
    /// y reordenamientos (nunca es un indice de lista).
    /// </summary>
    public readonly struct WfcSocketDescriptor : System.IEquatable<WfcSocketDescriptor>
    {
        /// <summary>Id reservado para "sin socket". Las librerias asignan ids desde 1.</summary>
        public const int InvalidId = 0;

        public readonly int Id;
        public readonly WfcSocketKind Kind;

        /// <summary>Solo significativo si Kind es HorizontalAsymmetric.</summary>
        public readonly bool Flipped;

        /// <summary>0..3. Solo significativo si Kind es VerticalRotational.</summary>
        public readonly int RotationIndex;

        public WfcSocketDescriptor(int id, WfcSocketKind kind, bool flipped = false, int rotationIndex = 0)
        {
            Id = id;
            Kind = kind;

            // Normalizacion: los modificadores irrelevantes se anulan para que
            // dos descriptores equivalentes sean siempre iguales y hasheen igual.
            Flipped = WfcSocketKinds.UsesFlipped(kind) && flipped;
            RotationIndex = WfcSocketKinds.UsesRotationIndex(kind)
                ? ((rotationIndex % 4) + 4) % 4
                : 0;
        }

        public static readonly WfcSocketDescriptor Invalid =
            new WfcSocketDescriptor(InvalidId, WfcSocketKind.HorizontalSymmetric);

        public bool IsValid => Id != InvalidId;

        public bool IsHorizontal => WfcSocketKinds.IsHorizontal(Kind);

        public bool IsVertical => WfcSocketKinds.IsVertical(Kind);

        /// <summary>
        /// El mismo socket visto tras girar el modulo alrededor de Y en pasos de 90 grados.
        /// Los horizontales no cambian de identidad (cambian de cara, eso lo maneja el modulo);
        /// los verticales orientados avanzan su indice de rotacion.
        /// </summary>
        public WfcSocketDescriptor RotatedAroundY(int steps)
        {
            if (Kind != WfcSocketKind.VerticalRotational) return this;
            return new WfcSocketDescriptor(Id, Kind, Flipped, RotationIndex + steps);
        }

        /// <summary>La pareja espejada. Solo cambia algo en horizontales asimetricos.</summary>
        public WfcSocketDescriptor Mirrored()
        {
            if (Kind != WfcSocketKind.HorizontalAsymmetric) return this;
            return new WfcSocketDescriptor(Id, Kind, !Flipped, RotationIndex);
        }

        /// <summary>
        /// Notacion compacta al estilo marian42: "3" / "3f" (asimetrico y su espejo),
        /// "3s" (simetrico), "5i" (vertical invariante), "5_2" (vertical rotacional).
        /// </summary>
        public string Notation()
        {
            if (!IsValid) return "-";

            switch (Kind)
            {
                case WfcSocketKind.HorizontalSymmetric: return Id + "s";
                case WfcSocketKind.HorizontalAsymmetric: return Flipped ? Id + "f" : Id.ToString();
                case WfcSocketKind.VerticalInvariant: return Id + "i";
                case WfcSocketKind.VerticalRotational: return Id + "_" + RotationIndex;
                default: return Id.ToString();
            }
        }

        public bool Equals(WfcSocketDescriptor other)
            => Id == other.Id && Kind == other.Kind && Flipped == other.Flipped && RotationIndex == other.RotationIndex;

        public override bool Equals(object obj) => obj is WfcSocketDescriptor other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (Flipped ? 1 : 0);
                hash = (hash * 397) ^ RotationIndex;
                return hash;
            }
        }

        public static bool operator ==(WfcSocketDescriptor a, WfcSocketDescriptor b) => a.Equals(b);
        public static bool operator !=(WfcSocketDescriptor a, WfcSocketDescriptor b) => !a.Equals(b);

        public override string ToString() => Notation();
    }
}