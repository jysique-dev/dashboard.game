using System;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// The two facts the movement system cares about for one cell: whether you may
    /// stand on it, and what it costs to enter it.
    ///
    /// Kept separate from CellType on purpose. CellType describes what a cell *is*
    /// (terrain authoring); MovementCell describes what a cell *does* to an agent.
    /// The adapter translates one into the other, so the movement system never has
    /// to grow a switch over terrain types.
    /// </summary>
    [Serializable]
    public struct MovementCell : IEquatable<MovementCell>
    {
        [SerializeField] private bool walkable;
        [SerializeField] private float cost;

        /// <summary>An impassable cell. Also the value of <c>default(MovementCell)</c>.</summary>
        public static readonly MovementCell Blocked = new MovementCell(false, Mathf.Infinity);

        /// <summary>A passable cell with the baseline cost of 1.</summary>
        public static readonly MovementCell Open = new MovementCell(true, 1f);

        public MovementCell(bool walkable, float cost)
        {
            this.walkable = walkable;
            // An impassable cell has no finite cost, whatever the caller passed in.
            // Normalising here means no consumer has to check both fields.
            this.cost = walkable ? Mathf.Max(0f, cost) : Mathf.Infinity;
        }

        /// <summary>A passable cell with the given cost. Negative costs are clamped to zero.</summary>
        public static MovementCell Walkable(float cost) => new MovementCell(true, cost);

        public bool IsWalkable => walkable && !float.IsInfinity(cost);

        /// <summary>Cost of entering. <see cref="Mathf.Infinity"/> when impassable.</summary>
        public float Cost => IsWalkable ? cost : Mathf.Infinity;

        public bool Equals(MovementCell other) => walkable == other.walkable && cost.Equals(other.cost);

        public override bool Equals(object obj) => obj is MovementCell other && Equals(other);

        public override int GetHashCode() => (walkable ? 397 : 0) ^ cost.GetHashCode();

        public override string ToString() => IsWalkable ? $"Walkable({cost})" : "Blocked";
    }
}