using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace zzre.benchmark;

public static class LaunchBenchmark
{
    public static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var b = new IntersectionsBenchmark();
        var c = new RaycastBenchmark();

        Console.Write("Running tests...");
        b.RunTests();
        c.RunTests();
        Console.WriteLine("done.");

        BenchmarkSwitcher.FromAssembly(typeof(LaunchBenchmark).Assembly).Run(args);
    }
}
