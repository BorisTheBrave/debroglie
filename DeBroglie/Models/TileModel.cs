using DeBroglie.Wfc;
using System.Collections.Generic;
using System.Linq;

namespace DeBroglie.Models
{


    /// <summary>
    /// Base class for the models used in generation.
    /// </summary>
    // A TileModel is a model with a well defined mapping from 
    // "tiles" (arbitrary identifiers of distinct tiles)
    // with patterns (dense integers that correspond to particular
    // arrangements of tiles).
    public abstract class TileModel
    {
        /// <summary>
        /// Extracts the actual model of patterns used.
        /// </summary>
        internal abstract PatternModel GetPatternModel();

        /// <summary>
        /// Relates patterns to tiles
        /// </summary>
        public abstract IReadOnlyDictionary<int, Tile> PatternsToTiles { get; }

        /// <summary>
        /// Relates patterns to tiles
        /// </summary>
        public abstract ILookup<Tile, int> TilesToPatterns { get; }

        /// <summary>
        /// Scales the the occurency frequency of a given tile by the given multiplier.
        /// </summary>
        public abstract void MultiplyFrequency(Tile tile, double multiplier, bool includeRotatedTiles = false);
    }
}
