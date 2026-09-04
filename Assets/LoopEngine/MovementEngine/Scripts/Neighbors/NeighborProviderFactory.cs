using System;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Builds the neighbour provider described by a set of options.
    ///
    /// One place that reads the connectivity settings, so no consumer ever writes
    /// "if eight-way then new EightWayNeighbors" for itself.
    /// </summary>
    public static class NeighborProviderFactory
    {
        /// <summary>
        /// Creates a provider. The shared, stateless instances are returned when the
        /// options match their defaults, so the common configurations allocate nothing.
        /// </summary>
        public static INeighborProvider Create(
            NeighborhoodMode mode,
            CornerRule cornerRule = CornerRule.BlockWhenEitherBlocked,
            float diagonalCost = 1.41421356f)
        {
            switch (mode)
            {
                case NeighborhoodMode.FourWay:
                    return FourWayNeighbors.Shared;

                case NeighborhoodMode.EightWay:
                    bool isDefault = cornerRule == EightWayNeighbors.Shared.CornerRule
                                     && Math.Abs(diagonalCost - EightWayNeighbors.Shared.DiagonalCost) < 0.0001f;

                    return isDefault
                        ? EightWayNeighbors.Shared
                        : new EightWayNeighbors(cornerRule, diagonalCost);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode), mode, "No neighbour provider is registered for this mode.");
            }
        }

        /// <summary>
        /// Creates the provider described by a settings asset. Falls back to four-way when
        /// no asset is assigned, which is the safe default: it never produces a diagonal an
        /// agent cannot physically make.
        /// </summary>
        public static INeighborProvider Create(MovementSettings settings)
        {
            if (settings == null) return FourWayNeighbors.Shared;

            return Create(settings.Neighborhood, settings.CornerRule, settings.DiagonalStepCost);
        }
    }
}