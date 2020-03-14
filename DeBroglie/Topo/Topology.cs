﻿namespace DeBroglie.Topo
{
    /// <summary>
    /// A Topology specifies the type of area or volume, what size it is, and whether it should wrap around at the edges (i.e. is it periodic). 
    /// Topologies do not actually store data, they just specify the dimensions. Actual data is stored in an <see cref="ITopoArray{T}"/>.
    /// </summary>
    public class Topology : ITopology
    {
        /// <summary>
        /// Constructs an <see cref="ITopoArray{T}"/> from an array. <c>result.Get(i) == values[i]</c>
        /// </summary>
        public static ITopoArray<T> Create<T>(T[] values, Topology topology)
        {
            return new TopoArray1D<T>(values, topology);
        }

        /// <summary>
        /// Constructs a 2d square grid topology of given dimensions and periodicity.
        /// </summary>
        public Topology(int width, int height, bool periodic)
            : this(DirectionSet.Cartesian2d, width, height, 1, periodic, periodic, periodic)
        {
        }

        /// <summary>
        /// Constructs a 3d cube grid topology of given dimensions and periodicity.
        /// </summary>
        public Topology(int width, int height, int depth, bool periodic)
            : this(DirectionSet.Cartesian3d, width, height, depth, periodic, periodic, periodic)
        {
        }

        /// <summary>
        /// Constructs a 2d topology.
        /// </summary>
        public Topology(DirectionSet directions, int width, int height, bool periodicX, bool periodicY, bool[] mask = null)
            : this(directions, width, height, 1, periodicX, periodicY, false, mask)
        {
        }

        /// <summary>
        /// Constructs a topology.
        /// </summary>
        public Topology(DirectionSet directions, int width, int height, int depth, bool periodicX, bool periodicY, bool periodicZ, bool[] mask = null)
        {
            Directions = directions;
            Width = width;
            Height = height;
            Depth = depth;
            PeriodicX = periodicX;
            PeriodicY = periodicY;
            PeriodicZ = periodicZ;
            Mask = mask;
        }

        /// <summary>
        /// Returns a <see cref="Topology"/> with the same parameters, but with the specified mask
        /// </summary>
        public Topology WithMask(bool[] mask)
        {
            if (Width * Height * Depth != mask.Length)
                throw new System.Exception("Mask size doesn't fit the topology");

            return new Topology(Directions, Width, Height, Depth, PeriodicX, PeriodicY, PeriodicZ, mask);
        }

        /// <summary>
        /// Returns a <see cref="Topology"/> with the same parameters, but with the specified mask
        /// </summary>
        public Topology WithMask(ITopoArray<bool> mask)
        {
            if (!IsSameSize(mask.Topology.AsGridTopology()))
                throw new System.Exception("Mask size doesn't fit the topology");
            var boolMask = new bool[Width * Height * Depth];
            for (var z = 0; z < Depth; z++)
            {
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        boolMask[x + y * Width + z * Width * Height] = mask.Get(x, y, z);
                    }
                }
            }
            return WithMask(boolMask);
        }

        /// <summary>
        /// Returns a <see cref="Topology"/> with the same parameters, with the dimensions overridden. Any mask is reset.
        /// </summary>
        public Topology WithSize(int width, int height, int depth = 1)
        {
            return new Topology(Directions, width, height, depth, PeriodicX, PeriodicY, PeriodicZ);
        }

        /// <summary>
        /// Returns a <see cref="Topology"/> with the same parameters, with the dimensions overridden.
        /// </summary>
        public Topology WithPeriodic(bool periodicX, bool periodicY, bool periodicZ = false)
        {
            return new Topology(Directions, Width, Height, Depth, periodicX, periodicY, periodicZ, Mask);
        }

        /// <summary>
        /// Characterizes the adjacency relationship between locations.
        /// </summary>
        public DirectionSet Directions { get; set; }

        /// <summary>
        /// Number of unique directions
        /// </summary>
        public int DirectionsCount => Directions.Count;

        /// <summary>
        /// The extent along the x-axis.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The extent along the y-axis.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The extent along the z-axis.
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Does the topology wrap on the x-axis.
        /// </summary>
        public bool PeriodicX { get; set; }

        /// <summary>
        /// Does the topology wrap on the y-axis.
        /// </summary>
        public bool PeriodicY { get; set; }

        /// <summary>
        /// Does the topology wrap on the z-axis.
        /// </summary>
        public bool PeriodicZ { get; set; }

        /// <summary>
        /// A array with one value per index indcating if the value is missing. 
        /// Not all uses of Topology support masks.
        /// </summary>
        public bool[] Mask { get; set; }

        /// <summary>
        /// Number of unique indices (distinct locations) in the topology
        /// </summary>
        public int IndexCount => Width * Height * Depth;

        public bool IsSameSize(Topology other)
        {
            return Width == other.Width && Height == other.Height && Depth == other.Depth;
        }

        /// <summary>
        /// Reduces a three dimensional co-ordinate to a single integer. This is mostly used internally.
        /// </summary>
        public int GetIndex(int x, int y, int z)
        {
            return x + y * Width + z * Width * Height;
        }

        /// <summary>
        /// Inverts <see cref="GetIndex(int, int, int)"/>
        /// </summary>
        public void GetCoord(int index, out int x, out int y, out int z)
        {
            x = index % Width;
            var i = index / Width;
            y = i % Height;
            z = i / Height;
        }

        /// <summary>
        /// Given an index and a direction, gives the index that is one step in that direction,
        /// if it exists and is not masked out. Otherwise, it returns false.
        /// </summary>
        public bool TryMove(int index, Direction direction, out int dest, out Direction inverseDirection)
        {
            inverseDirection = Directions.Inverse(direction);
            return TryMove(index, direction, out dest);
        }

        /// <summary>
        /// Given an index and a direction, gives the index that is one step in that direction,
        /// if it exists and is not masked out. Otherwise, it returns false.
        /// </summary>
        public bool TryMove(int index, Direction direction, out int dest)
        {
            int x, y, z;
            GetCoord(index, out x, out y, out z);
            return TryMove(x, y, z, direction, out dest);
        }

        /// <summary>
        /// Given a co-ordinate and a direction, gives the index that is one step in that direction,
        /// if it exists and is not masked out. Otherwise, it returns false.
        /// </summary>
        public bool TryMove(int x, int y, int z, Direction direction, out int dest)
        {
            if (TryMove(x, y, z, direction, out x, out y, out z))
            {
                dest = GetIndex(x, y, z);
                return true;
            }
            else
            {
                dest = -1;
                return false;
            }
        }

        /// <summary>
        /// Given a co-ordinate and a direction, gives the co-ordinate that is one step in that direction,
        /// if it exists and is not masked out. Otherwise, it returns false.
        /// </summary>
        public bool TryMove(int x, int y, int z, Direction direction, out int destx, out int desty, out int destz)
        {
            var d = (int)direction;
            x += Directions.DX[d];
            y += Directions.DY[d];
            z += Directions.DZ[d];
            if (PeriodicX)
            {
                if (x < 0) x += Width;
                if (x >= Width) x -= Width;
            }
            else if (x < 0 || x >= Width)
            {
                destx = -1;
                desty = -1;
                destz = -1;
                return false;
            }
            if (PeriodicY)
            {
                if (y < 0) y += Height;
                if (y >= Height) y -= Height;
            }
            else if (y < 0 || y >= Height)
            {
                destx = -1;
                desty = -1;
                destz = -1;
                return false;
            }
            if (PeriodicZ)
            {
                if (z < 0) z += Depth;
                if (z >= Depth) z -= Depth;
            }
            else if (z < 0 || z >= Depth)
            {
                destx = -1;
                desty = -1;
                destz = -1;
                return false;
            }
            destx = x;
            desty = y;
            destz = z;
            if (Mask != null)
            {
                var index2 = GetIndex(x, y, z);
                return Mask[index2];
            }
            else
            {
                return true;
            }
        }
    }
}
