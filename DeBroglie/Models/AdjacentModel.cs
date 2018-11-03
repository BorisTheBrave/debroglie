using DeBroglie.Rot;
using DeBroglie.Topo;
using DeBroglie.Wfc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeBroglie.Models
{

    /// <summary>
    /// AdjacentModel constrains which tiles can be placed adjacent to which other ones. 
    /// It does so by maintaining for each tile, a list of tiles that can be placed next to it in each direction. 
    /// The list is always symmetric, i.e. if it is legal to place tile B directly above tile A, then it is legal to place A directly below B.
    /// </summary>
    public class AdjacentModel : TileModel
    {
        private Directions directions;
        private IReadOnlyDictionary<int, Tile> patternsToTiles;
        private Dictionary<Tile, int> tilesToPatterns;
        private List<double> frequencies;
        private List<HashSet<int>[]> propagator;

        /// <summary>
        /// Constructs an AdjacentModel and initializees it with a given sample.
        /// </summary>
        public static AdjacentModel Create<T>(T[,] sample, bool periodic)
        {
            return Create(new TopoArray2D<T>(sample, periodic));
        }

        /// <summary>
        /// Constructs an AdjacentModel and initializees it with a given sample.
        /// </summary>
        public static AdjacentModel Create<T>(ITopoArray<T> sample)
        {
            return new AdjacentModel(sample.ToTiles());
        }


        /// <summary>
        /// Constructs an AdjacentModel.
        /// </summary>
        public AdjacentModel()
        {
            // Tiles map 1:1 with patterns
            tilesToPatterns = new Dictionary<Tile, int>();
            frequencies = new List<double>();
            propagator = new List<HashSet<int>[]>();
        }

        /// <summary>
        /// Constructs an AdjacentModel.
        /// </summary>
        public AdjacentModel(Directions directions)
            :this()
        {
            SetDirections(directions);
        }



        /// <summary>
        /// Constructs an AdjacentModel and initializees it with a given sample.
        /// </summary>
        public AdjacentModel(ITopoArray<Tile> sample)
            :this()
        {
            AddSample(sample);
        }

        /// <summary>
        /// Sets the directions of the Adjacent model, if it has not been set at construction.
        /// This specifies how many neighbours each tile has.
        /// Once set, it cannot be changed.
        /// </summary>
        /// <param name="directions"></param>
        public void SetDirections(Directions directions)
        {
            if(this.directions.Type != DirectionsType.Unknown && this.directions.Type != directions.Type)
            {
                throw new Exception($"Cannot set directions to {directions.Type}, it has already been set to {this.directions.Type}");
            }

            this.directions = directions;
        }

        /// <summary>
        /// Finds a tile and all its rotations, and sets their total frequency.
        /// </summary>
        public void SetFrequency(Tile tile, double frequency, TileRotation tileRotation)
        {
            var rotatedTiles = new List<Tile>();
            foreach(var rotation in tileRotation.RotationGroup)
            {
                if(tileRotation.Rotate(tile, rotation, out var result))
                {
                    // Note this deliberatly can count tiles twice
                    rotatedTiles.Add(result);
                }
            }
            foreach(var rt in rotatedTiles)
            {
                int pattern = GetPattern(rt);
                frequencies[pattern] = 0.0;
            }
            var incrementalFrequency = frequency / rotatedTiles.Count;
            foreach (var rt in rotatedTiles)
            {
                int pattern = GetPattern(rt);
                frequencies[pattern] += incrementalFrequency;
            }
        }


        /// <summary>
        /// Sets the frequency of a given tile.
        /// </summary>
        public void SetFrequency(Tile tile, double frequency, bool includeRotatedTiles = false)
        {
            Tile GetTile(Tile _tile)
            {
                return includeRotatedTiles ? GetUnrotatedTile(_tile) : _tile;
            }

            foreach (var kvp in PatternsToTiles)
            {
                if (GetTile(kvp.Value) == tile)
                {
                    frequencies[kvp.Key] = frequency;
                }
            }
        }

        /// <summary>
        /// Declares that the tiles in dest can be placed adjacent to the tiles in src, in the direction specified by (x, y, z).
        /// Then it adds similar declarations for other rotations and reflections, as specified by rotations.
        /// </summary>
        public void AddAdjacency(IList<Tile> src, IList<Tile> dest, int x, int y, int z, TileRotation tileRotation = null)
        {
            tileRotation = tileRotation ?? new TileRotation();

            foreach (var rotation in tileRotation.RotationGroup)
            {
                int x2, y2;
                if (directions.Type == DirectionsType.Hexagonal2d)
                {
                    (x2, y2) = TopoArrayUtils.HexRotateVector(x, y, rotation);
                }
                else
                {
                    (x2, y2) = TopoArrayUtils.RotateVector(x, y, rotation);
                }

                AddAdjacency(
                    tileRotation.Rotate(src, rotation).ToList(),
                    tileRotation.Rotate(dest, rotation).ToList(),
                    x2, y2, z);
            }
        }

        /// <summary>
        /// Declares that the tiles in dest can be placed adjacent to the tiles in src, in the direction specified by (x, y, z).
        /// (x, y, z) must be a valid direction, which usually means a unit vector.
        /// </summary>
        public void AddAdjacency(IList<Tile> src, IList<Tile> dest, int x, int y, int z)
        {
            var dir = directions.GetDirection(x, y, z);

            foreach (var s in src)
            {
                foreach(var d in dest)
                {
                    AddAdjacency(s, d, dir);
                }
            }
        }

        /// <summary>
        /// Declares that dest can be placed adjacent to src, in the direction specified by (x, y, z).
        /// (x, y, z) must be a valid direction, which usually means a unit vector.
        /// </summary>
        public void AddAdjacency(Tile src, Tile dest, int x, int y, int z)
        {
            var d = directions.GetDirection(x, y, z);
            AddAdjacency(src, dest, d);
        }

        private void AddAdjacency(Tile src, Tile dest, int d)
        {
            var id = directions.Inverse(d);
            var srcPattern = GetPattern(src);
            var destPattern = GetPattern(dest);
            propagator[srcPattern][d].Add(destPattern);
            propagator[destPattern][id].Add(srcPattern);
        }

        public void AddSample(ITopoArray<Tile> sample, TileRotation tileRotation = null)
        {
            foreach (var s in OverlappingAnalysis.GetRotatedSamples(sample, tileRotation))
            {
                AddSample(s);
            }
        }

        public void AddSample(ITopoArray<Tile> sample)
        {
            SetDirections(sample.Topology.Directions);

            var topology = sample.Topology;
            var width = topology.Width;
            var height = topology.Height;
            var depth = topology.Depth;
            var directionCount = topology.Directions.Count;

            for (var z = 0; z < depth; z++)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var index = topology.GetIndex(x, y, z);
                        if (!topology.ContainsIndex(index))
                            continue;

                        // Find the pattern and update the frequency
                        var pattern = GetPattern(sample.Get(x, y, z));

                        frequencies[pattern] += 1;

                        // Update propogator
                        for (var d = 0; d < directionCount; d++)
                        {
                            int x2, y2, z2;
                            if (topology.TryMove(x, y, z, d, out x2, out y2, out z2))
                            {
                                var pattern2 = GetPattern(sample.Get(x2, y2, z2));
                                propagator[pattern][d].Add(pattern2);
                            }
                        }
                    }
                }
            }
        }

        internal override PatternModel GetPatternModel()
        {

            return new PatternModel
            {
                Propagator = propagator.Select(x => x.Select(y => y.ToArray()).ToArray()).ToArray(),
                Frequencies = frequencies.ToArray(),
            };
        }

        public override IReadOnlyDictionary<int, Tile> PatternsToTiles
        {
            get
            {
                // Lazily evaluated
                return patternsToTiles = patternsToTiles ?? tilesToPatterns.ToDictionary(x => x.Value, x => x.Key); ;
            }
        }
        public override ILookup<Tile, int> TilesToPatterns  => tilesToPatterns.ToLookup(x=>x.Key, x=>x.Value);

        public override void MultiplyFrequency(Tile tile, double multiplier, bool includeRotatedTiles = false)
        {
            Tile GetTile(Tile _tile)
            {
                return includeRotatedTiles ? GetUnrotatedTile(_tile) : _tile;
            }

            foreach (var kvp in PatternsToTiles)
            {
                if (GetTile(kvp.Value) == tile)
                {
                    frequencies[kvp.Key] *= multiplier;
                }
            }
        }

        private int GetPattern(Tile tile)
        {
            var directionCount = directions.Count;

            int pattern;
            if (!tilesToPatterns.TryGetValue(tile, out pattern))
            {
                pattern = tilesToPatterns[tile] = tilesToPatterns.Count;
                frequencies.Add(0);
                propagator.Add(new HashSet<int>[directionCount]);
                for (var d = 0; d < directionCount; d++)
                {
                    propagator[pattern][d] = new HashSet<int>();
                }
                patternsToTiles = null;
            }
            return pattern;
        }

        public void NormalizeFrequencies(bool combineRotatedTiles = false)
        {
            Tile GetTile(Tile _tile)
            {
                return combineRotatedTiles ? GetUnrotatedTile(_tile) : _tile;
            }

            Dictionary<Tile, double> uniqueTileFrequencies = new Dictionary<Tile, double>();

            foreach (var kvp in PatternsToTiles)
            {
                var tile = GetTile(kvp.Value);

                uniqueTileFrequencies.TryGetValue(tile, out var sum);
                uniqueTileFrequencies[tile] = sum + frequencies[kvp.Key];
            }

            foreach (var kvp in PatternsToTiles)
            {
                frequencies[kvp.Key] /= uniqueTileFrequencies[GetTile(kvp.Value)];
            }

            var totalFrequency = 0.0;
            foreach (var f in frequencies)
            {
                totalFrequency += f;
            }

            for (int i = 0; i < frequencies.Count; i++) 
            {
                frequencies[i] = (frequencies[i] / totalFrequency);
            }
        }

        private Tile GetUnrotatedTile(Tile tile)
        {
            if (tile.Value is RotatedTile rotatedTile)
            {
                return rotatedTile.Tile;
            }
            return tile;
        }
    }
}
