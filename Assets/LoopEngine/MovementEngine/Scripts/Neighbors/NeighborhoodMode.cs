namespace LoopEngine.GridMovement
{
    /// <summary>Which directions count as one step.</summary>
    public enum NeighborhoodMode
    {
        /// <summary>North, south, east and west. Manhattan movement.</summary>
        FourWay,

        /// <summary>The four cardinals plus the four diagonals.</summary>
        EightWay
    }
}