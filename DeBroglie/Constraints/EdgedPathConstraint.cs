﻿using DeBroglie.Topo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeBroglie.Constraints
{
    public class EdgedPathConstraint : ITileConstraint
    {
        private TilePropogatorTileSet pathTileSet;

        private PathConstraintUtils.SimpleGraph graph;

        private IDictionary<int, TilePropogatorTileSet> tilesByExit;

        /// <summary>
        /// For each tile on the path, the set of direction values that paths exit out of this tile.
        /// </summary>
        public IDictionary<Tile, ISet<int>> Exits { get; set; }

        /// <summary>
        /// Set of points that must be connected by paths.
        /// If null, then PathConstraint ensures that all path cells
        /// are connected.
        /// </summary>
        public Point[] EndPoints { get; set; }

        public EdgedPathConstraint(IDictionary<Tile, ISet<int>> exits, Point[] endPoints = null)
        {
            this.Exits = exits;
            this.EndPoints = endPoints;
        }


        public Resolution Init(TilePropagator propagator)
        {
            pathTileSet = propagator.CreateTileSet(Exits.Keys);
            graph = CreateEdgedGraph(propagator.Topology);
            tilesByExit = Exits
                .SelectMany(kv => kv.Value.Select(e => Tuple.Create(kv.Key, e)))
                .GroupBy(x => x.Item2, x => x.Item1)
                .ToDictionary(g => g.Key, propagator.CreateTileSet);
            return Resolution.Undecided;
        }

        public Resolution Check(TilePropagator propagator)
        {

            var topology = propagator.Topology;
            var indices = topology.Width * topology.Height * topology.Depth;

            // TODO: This shouldn't be too hard to implement
            if (topology.Directions.Type != Topo.DirectionsType.Cartesian2d)
                throw new Exception("EdgedPathConstraint only supported for Cartesiant2d");

            var nodesPerIndex = topology.Directions.Count + 1;

            // Initialize couldBePath and mustBePath based on wave possibilities
            var couldBePath = new bool[indices * nodesPerIndex];
            var mustBePath = new bool[indices * nodesPerIndex];
            for (int i = 0; i < indices; i++)
            {
                topology.GetCoord(i, out var x, out var y, out var z);

                couldBePath[i * nodesPerIndex] = false;
                foreach (var kv in Exits)
                {
                    var tile = kv.Key;
                    var exits = kv.Value;

                    propagator.GetBannedSelected(x, y, z, tile, out var isBanned, out var isSelected);

                    if (!isBanned)
                    {
                        couldBePath[i * nodesPerIndex] = true;
                        foreach (var exit in exits)
                        {
                            couldBePath[i * nodesPerIndex + 1 + exit] = true;
                        }
                    }
                }
                // TODO: There's probably a more efficient way to do this
                propagator.GetBannedSelected(x, y, z, pathTileSet, out var allIsBanned, out var allIsSelected);
                mustBePath[i * nodesPerIndex] = allIsSelected;


            }

            // Select relevant cells, i.e. those that must be connected.
            bool[] relevant;
            if (EndPoints == null)
            {
                relevant = mustBePath;
            }
            else
            {
                relevant = new bool[indices * nodesPerIndex];
                if (EndPoints.Length == 0)
                    return Resolution.Undecided;
                foreach (var endPoint in EndPoints)
                {
                    var index = topology.GetIndex(endPoint.X, endPoint.Y, endPoint.Z);
                    relevant[index * nodesPerIndex] = true;
                }
            }
            var walkable = couldBePath;

            var isArticulation = PathConstraintUtils.GetArticulationPoints(graph, walkable, relevant);

            if (isArticulation == null)
            {
                return Resolution.Contradiction;
            }


            // All articulation points must be paths,
            // So ban any other possibilities
            for (var i = 0; i < indices; i++)
            {
                topology.GetCoord(i, out var x, out var y, out var z);
                if (isArticulation[i * nodesPerIndex])
                {
                    propagator.Select(x, y, z, pathTileSet);
                }
                for (var d = 0; d < topology.Directions.Count; d++)
                {
                    if(isArticulation[i * nodesPerIndex + 1 + d])
                    {
                        propagator.Select(x, y, z, tilesByExit[d]);
                    }
                }
            }

            return Resolution.Undecided;
        }

        private static readonly int[] Empty = { };

        /// <summary>
        /// Creates a grpah where each index in the original topology
        /// has 1+n nodes in the graph - one for the initial index
        /// and one for each direction leading out of it.
        /// </summary>
        private static PathConstraintUtils.SimpleGraph CreateEdgedGraph(Topology topology)
        {
            var nodesPerIndex = topology.Directions.Count + 1;

            var nodeCount = topology.IndexCount * nodesPerIndex;

            var neighbours = new int[nodeCount][];

            int GetNodeId(int index) => index * nodesPerIndex;

            int GetDirNodeId(int index, int direction) => index * nodesPerIndex + 1 + direction;

            foreach (var i in topology.Indicies)
            {
                var n = new List<int>();
                for (int d = 0; d < topology.Directions.Count; d++)
                {
                    if (topology.TryMove(i, d, out var dest))
                    {
                        // The central node connects to the direction node
                        n.Add(GetDirNodeId(i, d));
                        var inverseDir = topology.Directions.Inverse(d);
                        // The diction node connects to the central node
                        // and the opposing direction node
                        neighbours[GetDirNodeId(i, d)] =
                            new[] { GetNodeId(i), GetDirNodeId(dest, inverseDir) };
                    }
                    else
                    {
                        neighbours[GetDirNodeId(i, d)] = Empty;
                    }
                }
                neighbours[GetNodeId(i)] = n.ToArray();
            }

            return new PathConstraintUtils.SimpleGraph
            {
                NodeCount = nodeCount,
                Neighbours = neighbours,
            };
        }
    }
}