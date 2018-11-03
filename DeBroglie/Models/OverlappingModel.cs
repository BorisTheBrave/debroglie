using DeBroglie.Models;
using DeBroglie.Rot;
using DeBroglie.Topo;
using DeBroglie.Wfc;
using System.Collections.Generic;
using System.Linq;

namespace DeBroglie.Models
{

    /// <summary>
    /// OverlappingModel constrains that every n by n rectangle in the output is a copy of a rectangle taken from the sample.
    /// </summary>
    public class OverlappingModel : TileModel
    {
        private int nx;
        private int ny;
        private int nz;

        private Dictionary<PatternArray, int> patternIndices;
        private List<PatternArray> patternArrays;
        private List<double> frequencies;
        private List<HashSet<int>[]> propagator;

        private IReadOnlyDictionary<int, Tile> patternsToTiles;
        private ILookup<Tile, int> tilesToPatterns;

        public static OverlappingModel Create<T>(T[,] sample, int n, bool periodic, int symmetries)
        {
            var topArray = new TopoArray2D<T>(sample, periodic).ToTiles();

            return new OverlappingModel(topArray, n, symmetries > 1 ? symmetries / 2 : 1, symmetries > 1);
        }


        public OverlappingModel(ITopoArray<Tile> sample, int n, int rotationalSymmetry, bool reflectionalSymmetry)
            :this(n)
        {
            AddSample(sample, new TileRotation(rotationalSymmetry, reflectionalSymmetry));
        }

        /// <summary>
        /// Shorthand for constructing an Overlapping model with an n by n square or n by n by cuboid.
        /// </summary>
        /// <param name="n"></param>
        public OverlappingModel(int n)
            :this(n, n, n)
        {

        }

        public OverlappingModel(int nx, int ny, int nz)
        {
            this.nx = nx;
            this.ny = ny;
            this.nz = nz;
            patternIndices = new Dictionary<PatternArray, int>(new PatternArrayComparer());
            frequencies = new List<double>();
            patternArrays = new List<PatternArray>();
            propagator = new List<HashSet<int>[]>();
        }

        public void AddSample(ITopoArray<Tile> sample, TileRotation tileRotation = null)
        {
            if (sample.Topology.Depth == 1)
                nz = 1;

            var periodicX = sample.Topology.PeriodicX;
            var periodicY = sample.Topology.PeriodicY;
            var periodicZ = sample.Topology.PeriodicZ;

            foreach(var s in OverlappingAnalysis.GetRotatedSamples(sample, tileRotation))
            {
                OverlappingAnalysis.GetPatterns(s, nx, ny, nz, periodicX, periodicY, periodicZ, patternIndices, patternArrays, frequencies);
            }

            // Update the model based on the collected data
            var directions = sample.Topology.Directions;

            // TODO: Don't regenerate this from scratch every time
            propagator = new List<HashSet<int>[]>(patternArrays.Count);
            for (var p = 0; p < patternArrays.Count; p++)
            {
                propagator.Add(new HashSet<int>[directions.Count]);
                for (var d = 0; d < directions.Count; d++)
                {
                    var l = new HashSet<int>();
                    for (var p2 = 0; p2 < patternArrays.Count; p2++)
                    {
                        var dx = directions.DX[d];
                        var dy = directions.DY[d];
                        var dz = directions.DZ[d];
                        if (Aggrees(patternArrays[p], patternArrays[p2], dx, dy, dz))
                        {
                            l.Add(p2);
                        }
                    }
                    propagator[p][d] = l;
                }
            }

            patternsToTiles = patternArrays
                .Select((x, i) => new KeyValuePair<int, Tile>(i, x.Values[0, 0, 0]))
                .ToDictionary(x => x.Key, x => x.Value);

            tilesToPatterns = patternsToTiles.ToLookup(x => x.Value, x => x.Key);
        }

        public int NX => nx;
        public int NY => ny;
        public int NZ => nz;

        internal IReadOnlyList<PatternArray> PatternArrays => patternArrays;

        /**
          * Return true if the pattern1 is compatible with pattern2
          * when pattern2 is at a distance (dy,dx) from pattern1.
          */
        private bool Aggrees(PatternArray a, PatternArray b, int dx, int dy, int dz)
        {
            var xmin = dx < 0 ? 0 : dx;
            var xmax = dx < 0 ? dx + b.Width : a.Width;
            var ymin = dy < 0 ? 0 : dy;
            var ymax = dy < 0 ? dy + b.Height : a.Height;
            var zmin = dz < 0 ? 0 : dz;
            var zmax = dz < 0 ? dz + b.Depth : a.Depth;
            for (var x = xmin; x < xmax; x++)
            {
                for (var y = ymin; y < ymax; y++)
                {
                    for (var z = zmin; z < zmax; z++)
                    {
                        if (a.Values[x, y, z] != b.Values[x - dx, y - dy, z - dz])
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        internal override PatternModel GetPatternModel()
        {
            return new PatternModel
            {
                Propagator = propagator.Select(x => x.Select(y => y.ToArray()).ToArray()).ToArray(),
                Frequencies = frequencies.ToArray(),
            };
        }

        public override IReadOnlyDictionary<int, Tile> PatternsToTiles => patternsToTiles;
        public override ILookup<Tile, int> TilesToPatterns => tilesToPatterns;

        public override void MultiplyFrequency(Tile tile, double multiplier, bool includeRotatedTiles = false)
        {
            Tile GetTile(Tile _tile)
            {
                return includeRotatedTiles ? GetUnrotatedTile(_tile) : _tile;
            }

            for (var p = 0; p < patternArrays.Count; p++)
            {
                var patternArray = patternArrays[p];
                for (var x = 0; x < patternArray.Width; x++)
                {
                    for (var y = 0; y < patternArray.Height; y++)
                    {
                        for (var z = 0; z < patternArray.Depth; z++)
                        {
                            if (GetTile(patternArray.Values[x, y, z]) == tile)
                            {
                                frequencies[p] *= multiplier;
                            }
                        }
                    }
                }
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
