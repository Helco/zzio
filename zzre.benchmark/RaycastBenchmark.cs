extern alias Proj;
extern alias Baseline;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using Proj::zzre;

using BLWorldCollider = Baseline::zzre.WorldCollider;
using BLSphere = Baseline::zzre.Sphere;
using BLRay = Baseline::zzre.Ray;
using BLRaycast = Baseline::zzre.Raycast;
using BLIntersectionQueries = Baseline::zzre.IntersectionQueries;
using BLIntersection = Baseline::zzre.Intersection;
using BLAnyIntersectionable = Baseline::zzre.AnyIntersectionable;

using MyJobAttribute = BenchmarkDotNet.Attributes.LongRunJobAttribute;
using Perfolizer.Horology;
using System.Globalization;

namespace zzre.benchmark;

[MemoryDiagnoser]
[MyJob]
[MinIterationCount(10), MaxIterationCount(1000)]
public class RaycastBenchmark
{
    private const int Seed = 12345;
    private const string ArchivePath = @"C:\dev\zanzarah\Resources\DATA_0.PAK";
    private const string WorldPath = "Resources/Worlds/sc_1243.bsp";
    private const int CaseCount = 1000;
    private readonly WorldCollider worldCollider;
    private readonly BLWorldCollider worldColliderBL;
    private readonly MergedCollider mergedCollider;
    private readonly Ray[] cases;
    private readonly List<Intersection> intersections = new(128);

    public RaycastBenchmark()
    {
        var archive = new PAKParallelResourcePool(ArchivePath);
        using var worldStream = archive.FindAndOpen(WorldPath)
            ?? throw new FileNotFoundException($"Could not open world geometry: " + WorldPath);
        var rwWorld = Section.ReadNew(worldStream) as RWWorld
            ?? throw new IOException("Could not read world geometry: " + WorldPath);
        worldCollider = new(rwWorld);
        worldColliderBL = new(rwWorld);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        mergedCollider = MergedCollider.Create(rwWorld);
        stopwatch.Stop();
        Console.WriteLine($"Merging one tree took {stopwatch.Elapsed.ToFormattedTotalTime(CultureInfo.InvariantCulture)}");

        var random = new Random(Seed);
        cases = new Ray[CaseCount];
        var atomicSections = rwWorld
            .FindAllChildrenById(SectionId.AtomicSection, recursive: true)
            .Cast<RWAtomicSection>()
            .ToArray();
        for (int i = 0; i < cases.Length; i++)
        {
            while(true)
            {
                var atomic = random.NextOf(atomicSections);
                var bboxmin = Vector3.Min(atomic.bbox1, atomic.bbox2);
                var bboxmax = Vector3.Max(atomic.bbox1, atomic.bbox2);
                var pos = bboxmin + random.InPositiveCube() * (bboxmax - bboxmin);
                var ray = new Ray(pos, random.OnSphere());
                if (worldCollider.Cast(ray) is not null)
                {
                    cases[i] = ray;
                    break;
                }
            }
        }
    }

    [Benchmark(Baseline = true)]
    public float Baseline()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = worldColliderBL.Cast(new BLRay(ray.Start, ray.Direction));
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    [Benchmark]
    public float SimpleOptimizations()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = worldCollider.Cast(ray);
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    //[Benchmark]
    public float Merged()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = mergedCollider.Cast(ray);
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    //[Benchmark]
    public float MergedIterative()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = mergedCollider.CastIterative(ray, float.PositiveInfinity);
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    [Benchmark]
    public float MergedRWPrevious()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = mergedCollider.CastRWPrevious(ray);
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    [Benchmark]
    public float MergedRWNext()
    {
        float f = 0f;
        foreach (var ray in cases)
        {
            var c = mergedCollider.CastRWNext(ray);
            f += c?.Distance ?? 0f;
        }
        return f;
    }

    private static Raycast? ConvertBLCast(BLRaycast? blOpt)
    {
        if (blOpt is not BLRaycast bl)
            return null;
        WorldTriangleId? tid = null;
        if (bl.TriangleId is not null)
            tid = new(bl.TriangleId.Value.AtomicIdx, bl.TriangleId.Value.TriangleIdx);
        return new Raycast(bl.Distance, bl.Point, bl.Normal, tid);
    }

    public unsafe void RunTests()
    {
        int caseI = -1;
        foreach (var ray in cases)
        {
            caseI++;
            var results = new List<Raycast?>()
            {
                ConvertBLCast(worldColliderBL.Cast(new BLRay(ray.Start, ray.Direction))),
                worldCollider.Cast(ray),
                mergedCollider.Cast(ray),
                mergedCollider.CastIterative(ray, float.PositiveInfinity),
                mergedCollider.CastRWPrevious(ray),
                mergedCollider.CastRWNext(ray),
            };

            var i = results.IndexOf(c => (c is null && results.First() is not null));
            if (i < 0)
                i = results.IndexOf(c => c is not null && c.Value.TriangleId != results.First().Value.TriangleId);
            if (i >= 0)
            {
                throw new Exception($"NOPE case {caseI} {i}\nRAY: {ray.Start} in {ray.Direction}\nEXP: {results.First()}\nACT: {results[i]}");
            }
        }
    }

    private static bool AreEqualEnough(Raycast a, Raycast b)
    {
        var diffDist = MathF.Abs(a.Distance - b.Distance);
        var diffPoint = Vector3.Distance(a.Point, b.Point);
        var diffNormal = Vector3.Distance(a.Normal, b.Normal);
        var diffNormalAlt = Vector3.Distance(a.Normal, -b.Normal);
        Console.WriteLine($"{diffDist}  {diffPoint}  {diffNormal} {diffNormalAlt}");
        return diffDist < 0.005f && diffPoint < 0.005f &&
            (diffNormal < 0.001f || diffNormalAlt < 0.001f);
    }

}
