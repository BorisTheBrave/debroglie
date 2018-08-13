﻿namespace DeBroglie.Constraints
{
    /// <summary>
    /// Interface for specifying non-local constraints to be respected during generation.
    /// </summary>
    public interface ITileConstraint
    {
        /// <summary>
        /// Called once when the propogator first initializes.
        /// </summary>
        /// <param name="propagator">The propagator to constraint</param>
        /// <returns>Contradiction if something is wrong, Undecided if generation should continue</returns>
        CellStatus Init(TilePropagator propagator);

        /// <summary>
        /// Called frequently during generation to help maintain the constraint.
        /// </summary>
        /// <param name="propagator">The propagator to constraint</param>
        /// <returns>Contradiction if something is wrong, Undecided if generation should continue</returns>
        CellStatus Check(TilePropagator propagator);
    }
}
