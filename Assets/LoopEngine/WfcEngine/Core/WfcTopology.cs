namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Volumen de celdas y su vecindad. Sin coordenadas de mundo ni tamano de celda:
    /// eso es cosa de la capa Unity. Aqui solo hay indices.
    ///
    /// El indice es x + SizeX * (y + SizeY * z), de modo que recorrer indices consecutivos
    /// avanza primero en X. Importa al depurar: el render ASCII de una capa sale contiguo.
    /// </summary>
    public sealed class WfcTopology
    {
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int CellCount { get; }

        public WfcTopology(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX < 1 ? 1 : sizeX;
            SizeY = sizeY < 1 ? 1 : sizeY;
            SizeZ = sizeZ < 1 ? 1 : sizeZ;
            CellCount = SizeX * SizeY * SizeZ;
        }

        public int Index(int x, int y, int z) => x + SizeX * (y + SizeY * z);

        public void CoordsOf(int index, out int x, out int y, out int z)
        {
            x = index % SizeX;
            y = (index / SizeX) % SizeY;
            z = index / (SizeX * SizeY);
        }

        public bool Contains(int x, int y, int z)
            => x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

        /// <summary>Vecino en una direccion. false si cae fuera del volumen.</summary>
        public bool TryNeighbor(int index, WfcDirection direction, out int neighborIndex)
        {
            CoordsOf(index, out int x, out int y, out int z);

            var offset = WfcDirections.Offset(direction);
            x += offset.X;
            y += offset.Y;
            z += offset.Z;

            if (!Contains(x, y, z))
            {
                neighborIndex = -1;
                return false;
            }

            neighborIndex = Index(x, y, z);
            return true;
        }

        public bool IsBoundary(int index, WfcDirection direction)
            => !TryNeighbor(index, direction, out _);

        public override string ToString() => $"{SizeX}x{SizeY}x{SizeZ} ({CellCount} celdas)";
    }
}