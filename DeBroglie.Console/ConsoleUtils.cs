﻿using System;

namespace DeBroglie.Console
{
    public static class ConsoleUtils
    {
        public static void Write(TilePropagator<int> propagator)
        {
            var results = propagator.ToTopArray(-1, -2).ToArray2d();

            for (var y = 0; y < results.GetLength(1); y++)
            {
                for (var x = 0; x < results.GetLength(0); x++)
                {
                    var r = results[x, y];
                    string c;
                    switch (r)
                    {
                        case -1: c = "?"; break;
                        case -2: c = "*"; break;
                        case 0: c = " "; break;
                        default: c = r.ToString(); break;
                    }
                    System.Console.Write(c);
                }
                System.Console.WriteLine();
            }
        }

        public static void WriteSteps(TilePropagator<int> propagator)
        {
            Write(propagator);
            System.Console.WriteLine();

            while (true)
            {
                var prevBacktrackCount = propagator.BacktrackCount;
                var status = propagator.Step();
                Write(propagator);
                if (propagator.BacktrackCount != prevBacktrackCount)
                {
                    System.Console.WriteLine("Backtracked!");
                }
                System.Console.WriteLine();

                if (status != CellStatus.Undecided)
                {
                    System.Console.WriteLine(status);
                    break;
                }
            }
        }

        public static CellStatus Run(WavePropagator propagator, int retries)
        {
            CellStatus status = CellStatus.Undecided;
            for (var retry = 0; retry < retries; retry++)
            {
                status = propagator.Run();
                if (status == CellStatus.Decided)
                {
                    break;
                }
            }
            return status;
        }
    }
}
