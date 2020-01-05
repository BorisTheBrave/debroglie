﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Order;
using DeBroglie.Constraints;
using DeBroglie.Models;
using DeBroglie.Rot;
using DeBroglie.Topo;
using System.Collections.Generic;
using System.Linq;

namespace DeBroglie.Benchmark
{
    //[EtwProfiler] // Or use -p ETW on command line
    //[ClrJob, MonoJob, CoreJob, CoreRtJob]
    //[LegacyJitX86Job, LegacyJitX64Job, RyuJitX64Job]
    /*
    [HardwareCounters(
        HardwareCounter.Timer,
        HardwareCounter.TotalIssues,
        HardwareCounter.BranchInstructions,
        HardwareCounter.CacheMisses,
        HardwareCounter.BranchMispredictions
        //HardwareCounter.TotalCycles,
        //HardwareCounter.UnhaltedCoreCycles,
        //HardwareCounter.InstructionRetired,
        //HardwareCounter.UnhaltedReferenceCycles,
        //HardwareCounter.LlcReference,
        //HardwareCounter.LlcMisses,
        //HardwareCounter.BranchInstructionRetired,
        //HardwareCounter.BranchMispredictsRetired
    )]
    */
    //[DisassemblyDiagnoser(printAsm: true, printSource: true)]
    [Orderer(SummaryOrderPolicy.Declared, MethodOrderPolicy.Declared)]
    public class Benchmarks
    {
        private TilePropagator propagator1;
        private TilePropagator propagator2;
        private TilePropagator propagator3;
        private TilePropagator propagator4;
        private TilePropagator propagator5;
        private TilePropagator propagator6;

        [GlobalSetup]
        public void Setup()
        {
            FreeSetup();
        }

        private void Check(TilePropagator p)
        {
            if (p.Status != Resolution.Decided)
                throw new System.Exception("Propagator contradiction");
        }

        public void FreeSetup()
        {
            var tileCount = 10;
            var topology = new Topology(10, 10, 10, false);

            var model = new AdjacentModel(DirectionSet.Cartesian3d);

            var tiles = Enumerable.Range(0, tileCount).Select(x => new Tile(x)).ToList(); ;

            model.AddAdjacency(tiles, tiles, Direction.XPlus);
            model.AddAdjacency(tiles, tiles, Direction.YPlus);
            model.AddAdjacency(tiles, tiles, Direction.ZPlus);

            model.SetUniformFrequency();

            propagator1 = new TilePropagator(model, topology, new TilePropagatorOptions { });
        }

        [Benchmark]
        public void Free()
        {
            propagator1.Clear();
            propagator1.Run();
        }

        public void ChessSetup()
        {
            var topology = new Topology(10, 10, 10, false);

            var model = new AdjacentModel(DirectionSet.Cartesian3d);

            var t1 = new Tile(1);
            var t2 = new Tile(2);

            model.AddAdjacency(t1, t2, Direction.XPlus);
            model.AddAdjacency(t2, t1, Direction.XPlus);
            model.AddAdjacency(t1, t2, Direction.YPlus);
            model.AddAdjacency(t2, t1, Direction.YPlus);
            model.AddAdjacency(t1, t2, Direction.ZPlus);
            model.AddAdjacency(t2, t1, Direction.ZPlus);

            model.SetUniformFrequency();

            propagator2 = new TilePropagator(model, topology, new TilePropagatorOptions { });
        }

        //[Benchmark]
        public void Chess()
        {
            propagator2.Clear();
            propagator2.Run();
        }

        // Inspired by Tessera's Castle scene
        public void CastleSetup()
        {
            var topology = new Topology(10, 10, 10, false);

            var model = CastleModel.Get();

            propagator3 = new TilePropagator(model, topology, new TilePropagatorOptions { });
        }


        //[Benchmark]
        public void Castle()
        {
            propagator3.Clear();
            propagator3.Run();
        }

        public void EdgedPathSetup()
        {
            var topology = new Topology(15, 15, false);

            var model = new AdjacentModel(DirectionSet.Cartesian2d);

            var empty = new Tile(" ");
            var straight1 = new Tile("║");
            var straight2 = new Tile("═");
            var corner1 = new Tile("╚");
            var corner2 = new Tile("╔");
            var corner3 = new Tile("╗");
            var corner4 = new Tile("╝");
            var fork1 = new Tile("╠");
            var fork2 = new Tile("╦");
            var fork3 = new Tile("╣");
            var fork4 = new Tile("╩");

            model.AddAdjacency(
                new[] { empty, straight1, corner3, corner4, fork3 },
                new[] { empty, straight1, corner1, corner2, fork1 },
                Direction.XPlus);

            model.AddAdjacency(
                new[] { straight2, corner1, corner2, fork1, fork2, fork4 },
                new[] { straight2, corner3, corner4, fork2, fork3, fork4 },
                Direction.XPlus);

            model.AddAdjacency(
                new[] { empty, straight2, corner1, corner4, fork4 },
                new[] { empty, straight2, corner2, corner3, fork2 },
                Direction.YPlus);

            model.AddAdjacency(
                new[] { straight1, corner2, corner3, fork1, fork2, fork3 },
                new[] { straight1, corner1, corner4, fork1, fork3, fork4 },
                Direction.YPlus);

            model.SetUniformFrequency();

            var exits = new Dictionary<Tile, ISet<Direction>>
            {
                {straight1, new []{Direction.YMinus, Direction.YPlus}.ToHashSet() },
                {straight2, new []{Direction.XMinus, Direction.XPlus}.ToHashSet() },
                {corner1, new []{Direction.YMinus, Direction.XPlus}.ToHashSet() },
                {corner2, new []{Direction.YPlus, Direction.XPlus}.ToHashSet() },
                {corner3, new []{Direction.YPlus, Direction.XMinus}.ToHashSet() },
                {corner4, new []{Direction.YMinus, Direction.XMinus}.ToHashSet() },
                {fork1, new []{ Direction.YMinus, Direction.XPlus, Direction.YPlus}.ToHashSet() },
                {fork2, new []{ Direction.XPlus, Direction.YPlus, Direction.XMinus}.ToHashSet() },
                {fork3, new []{ Direction.YPlus, Direction.XMinus, Direction.YMinus}.ToHashSet() },
                {fork4, new []{ Direction.XMinus, Direction.YMinus, Direction.XMinus}.ToHashSet() },
            };

            var pathConstraint = new EdgedPathConstraint(exits);

            propagator4 = new TilePropagator(model, topology, new TilePropagatorOptions
            {
                BackTrackDepth = -1,
                Constraints = new[] { pathConstraint },
            });
        }

        //[Benchmark]
        public void EdgedPath()
        {
            propagator4.Clear();
            propagator4.Run();

            if (false)
            {
                var v = propagator4.ToValueArray<string>();
                for (var y = 0; y < v.Topology.Height; y++)
                {
                    for (var x = 0; x < v.Topology.Width; x++)
                    {
                        System.Console.Write(v.Get(x, y));
                    }
                    System.Console.WriteLine();
                }
            }

            Check(propagator4);
        }

        public void PathSetup()
        {

            var tileCount = 10;
            var topology = new Topology(20, 20, false);

            var model = new AdjacentModel(DirectionSet.Cartesian2d);

            var tiles = Enumerable.Range(0, tileCount).Select(x => new Tile(x)).ToList(); ;

            model.AddAdjacency(tiles, tiles, Direction.XPlus);
            model.AddAdjacency(tiles, tiles, Direction.YPlus);

            model.SetUniformFrequency();
            var pathConstraint = new PathConstraint(tiles.Skip(1).ToHashSet());

            propagator5 = new TilePropagator(model, topology, new TilePropagatorOptions
            {
                BackTrackDepth = -1,
                Constraints = new[] { pathConstraint },
            });
        }

        //[Benchmark]
        public void Path()
        {
            propagator5.Clear();
            propagator5.Run();

            Check(propagator5);

            if (false)
            {
                var v = propagator5.ToValueArray<string>();
                for (var y = 0; y < v.Topology.Height; y++)
                {
                    for (var x = 0; x < v.Topology.Width; x++)
                    {
                        System.Console.Write(v.Get(x, y));
                    }
                    System.Console.WriteLine();
                }
            }
        }


        public void CountSetup()
        {
            var model = new AdjacentModel(DirectionSet.Cartesian2d);
            var tile1 = new Tile(1);
            var tile2 = new Tile(2);
            var tiles = new[] { tile1, tile2 };
            model.AddAdjacency(tiles, tiles, Direction.XPlus);
            model.AddAdjacency(tiles, tiles, Direction.YPlus);
            model.SetUniformFrequency();

            var topology = new Topology(100, 100, false);

            var count = 30;

            var options = new TilePropagatorOptions
            {
                Constraints = new[]
                {
                    new CountConstraint
                    {
                        Tiles = new[]{tile1 }.ToHashSet(),
                        Count = count,
                        Comparison = CountComparison.AtMost,
                        Eager = false,
                    }
                }
            };
            propagator6 = new TilePropagator(model, topology, options);
        }

        //[Benchmark]
        public void Count()
        {
            propagator6.Clear();
            propagator6.Run();

            Check(propagator6);
        }
    }
}
