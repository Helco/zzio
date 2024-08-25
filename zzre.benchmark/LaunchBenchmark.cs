using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace zzre.benchmark;

public static class LaunchBenchmark
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(LaunchBenchmark).Assembly).Run(args);
    }
}
