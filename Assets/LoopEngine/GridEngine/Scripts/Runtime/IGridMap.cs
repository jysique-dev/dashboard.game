namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Read-only view over per-cell data. Depend on this when a system only
    /// consumes the grid (renderers, queries) so it cannot mutate it.
    /// </summary>
    public interface IReadOnlyGridMap<T>
    {
        GridBounds Bounds { get; }

        /// <summary>Value stored at the cell. Throws when the cell is outside the bounds.</summary>
        T this[GridCoord coord] { get; }

        /// <summary>Safe read. Returns false and <c>default</c> when the cell is outside the bounds.</summary>
        bool TryGetValue(GridCoord coord, out T value);
    }

    /// <summary>Mutable per-cell storage.</summary>
    public interface IGridMap<T> : IReadOnlyGridMap<T>
    {
        new T this[GridCoord coord] { get; set; }

        /// <summary>Safe write. Returns false when the cell is outside the bounds.</summary>
        bool TrySetValue(GridCoord coord, T value);

        /// <summary>Writes the same value into every cell.</summary>
        void Fill(T value);
    }
}