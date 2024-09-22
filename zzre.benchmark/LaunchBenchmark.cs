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
        /*var cc = new RaycastBenchmark();
        for (int i = 0; i < 100; i++)
            cc.MergedRWBR();
        return;*/

        var b = new IntersectionsBenchmark();
        //b.IntersectionsGenerator();
        //b.IntersectionsBaseline();
        //b.IntersectionsList();
        //b.IntersectionsListKDMerged();
        //b.IntersectionsStruct();
        //b.IntersectionsTaggedUnion();
        b.RunTests();

        var c = new RaycastBenchmark();
        c.RunTests();

        BenchmarkSwitcher.FromAssembly(typeof(LaunchBenchmark).Assembly).Run(args);
    }
}
