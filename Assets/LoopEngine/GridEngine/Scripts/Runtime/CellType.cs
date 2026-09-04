namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Default cell classification, provided as a ready-made payload for
    /// <see cref="GridMap{T}"/>.
    ///
    /// Using it is optional: GridMap stays generic, so a project with different needs
    /// can declare its own enum or struct and every API here keeps working. This one
    /// exists so the common case does not force you to write the enum yourself.
    /// </summary>
    public enum CellType
    {
        /// <summary>Nothing here. The default value of the enum, so a new map starts empty.</summary>
        Empty = 0,
        /// <summary>Walkable, buildable terrain.</summary>
        Ground = 1,
        /// <summary>Impassable. Walls, cliffs, scenery.</summary>
        Blocked = 2,
        /// <summary>Passable under special rules: water, swamp, hazard.</summary>
        Difficult = 3,
        /// <summary>Reserved for spawning units or objects.</summary>
        Spawn = 4,
        /// <summary>Objective, capture point, goal tile.</summary>
        Objective = 5
    }
}