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
using BLIntersectionQueries = Baseline::zzre.IntersectionQueries;
using BLIntersection = Baseline::zzre.Intersection;
using BLAnyIntersectionable = Baseline::zzre.AnyIntersectionable;

using MyJobAttribute = BenchmarkDotNet.Attributes.MediumRunJobAttribute;
using Perfolizer.Horology;
using System.Globalization;

namespace zzre.benchmark;

[MemoryDiagnoser]
[MyJob]
[MinIterationCount(10), MaxIterationCount(1000)]
public class IntersectionsBenchmark
{
    private const int Seed = 12345;
    private const string ArchivePath = @"C:\dev\zanzarah\Resources\DATA_0.PAK";
    private const string WorldPath = "Resources/Worlds/sc_1243.bsp";
    private const float SphereRadius = 2.0f;
    private const int CaseCount = 1000;
    private readonly WorldCollider worldCollider;
    private readonly BLWorldCollider worldColliderBL;
    private readonly MergedCollider mergedCollider;
    private readonly SIMD128Collider simd128Collider;
    private readonly SIMD256Collider simd256Collider;
    private readonly SIMD512Collider simd512Collider;
    private readonly Vector3[] cases;
    private List<Intersection> intersections = new(256);
    private List<BLIntersection> blintersections = new(256);

    public IntersectionsBenchmark()
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
        simd128Collider = new(mergedCollider.Coarse, mergedCollider.Collision, mergedCollider.triangles, mergedCollider.triangleIds);
        simd256Collider = new(mergedCollider.Coarse, mergedCollider.Collision, mergedCollider.triangles, mergedCollider.triangleIds);
        simd512Collider = new(mergedCollider.Coarse, mergedCollider.Collision, mergedCollider.triangles, mergedCollider.triangleIds);
        stopwatch.Stop();
        Console.WriteLine($"Merging four trees took {stopwatch.Elapsed.ToFormattedTotalTime(CultureInfo.InvariantCulture)}");

        var random = new Random(Seed);
        cases = new Vector3[CaseCount];
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
                if (worldCollider.Intersections(new Sphere(pos, SphereRadius)).Any())
                {
                    cases[i] = pos;
                    break;
                }
            }
        }
    }

    [Benchmark(Baseline = true)]
    public float IntersectionsBaseline()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            foreach (var intersection in worldColliderBL.IntersectionsGeneratorOld<BLSphere, BLIntersectionQueries>(new BLSphere(pos, SphereRadius)))
                f += intersection.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsGenerator()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            foreach (var intersection in worldCollider.IntersectionsGenerator<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius)))
                f += intersection.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsBaselineList()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            blintersections.Clear();
            worldColliderBL.IntersectionsList<BLSphere, BLIntersectionQueries>(new BLSphere(pos, SphereRadius), blintersections);
            f += blintersections.Count;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsList()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            worldCollider.IntersectionsList<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    [Benchmark]
    public float IntersectionsListKDMerged()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            mergedCollider.IntersectionsList<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    [Benchmark]
    public float IntersectionsStructMerged()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            var enumerator = new MergedCollider.IntersectionsEnumerator<Sphere, IntersectionQueries>(mergedCollider, new Sphere(pos, SphereRadius));
            while (enumerator.MoveNext())
                f += enumerator.Current.Point.X;
        }
        return f;
    }

    [Benchmark]
    public float IntersectionsTaggedUnionMerged()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            var enumerator = new MergedCollider.IntersectionsEnumeratorVirtCall(mergedCollider, new Sphere(pos, SphereRadius));
            while (enumerator.MoveNext())
                f += enumerator.Current.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public unsafe float IntersectionsStruct()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            var enumerator = new WorldCollider.IntersectionsEnumerator<Sphere, IntersectionQueries,
                TreeCollider<Box>.IntersectionsEnumerator<Sphere, IntersectionQueries>>
                (worldCollider, new Sphere(pos, SphereRadius),
                &WorldCollider.IntersectionsEnumerable<Sphere, IntersectionQueries>.AtomicIntersections);
            while (enumerator.MoveNext())
                f += enumerator.Current.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsBaselineTaggedUnion()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            var enumerator = new BLWorldCollider.IntersectionsEnumeratorVirtCall(worldColliderBL, BLAnyIntersectionable.From(new BLSphere(pos, SphereRadius)));
            while (enumerator.MoveNext())
                f += enumerator.Current.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsTaggedUnion()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            var enumerator = new WorldCollider.IntersectionsEnumeratorVirtCall(worldCollider, AnyIntersectionable.From(new Sphere(pos, SphereRadius)));
            while (enumerator.MoveNext())
                f += enumerator.Current.Point.X;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsSIMD128()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            simd128Collider.IntersectionsList(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsSIMD256()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            simd256Collider.IntersectionsList(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsSIMD512()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            simd512Collider.IntersectionsList(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    //[Benchmark]
    public float IntersectionsSIMD512LB()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            simd512Collider.IntersectionsListLB(new Sphere(pos, SphereRadius), intersections);
            f += intersections.Count;
        }
        return f;
    }

    private static Intersection ConvertBLIntersection(BLIntersection i)
    {
        var t = new Triangle(i.Triangle.A, i.Triangle.B, i.Triangle.C);
        var ti = i.TriangleId == null ? null as WorldTriangleId?
            : new WorldTriangleId(i.TriangleId.Value.AtomicIdx, i.TriangleId.Value.TriangleIdx);
        return new Intersection(i.Point, t, ti);
    }

    public unsafe void RunTests()
    {
        int caseI = -1;
        foreach (var pos in cases)
        {
            caseI++;
            var results = new List<List<Intersection>>();

            //results.Add(worldColliderBL
            //    .IntersectionsGeneratorOld<BLSphere, BLIntersectionQueries>(new BLSphere(pos, SphereRadius))
            //    .Select(ConvertBLIntersection)
            //    .ToList());

            results.Add(worldCollider
                .IntersectionsGenerator<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius))
                .ToList());

            //blintersections.Clear();
            //worldColliderBL.IntersectionsList<BLSphere, BLIntersectionQueries>(new BLSphere(pos, SphereRadius), blintersections);
            //results.Add(blintersections.Select(ConvertBLIntersection).ToList());

            intersections.Clear();
            worldCollider.IntersectionsList<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            mergedCollider.IntersectionsList<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            mergedCollider.IntersectionsListInty<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            var e1 = new WorldCollider.IntersectionsEnumerator<Sphere, IntersectionQueries,
                    TreeCollider<Box>.IntersectionsEnumerator<Sphere, IntersectionQueries>>
                    (worldCollider, new Sphere(pos, SphereRadius),
                    &WorldCollider.IntersectionsEnumerable<Sphere, IntersectionQueries>.AtomicIntersections);
            while (e1.MoveNext())
                intersections.Add(e1.Current);
            results.Add(intersections.ToList());

            intersections.Clear();
            var e2 = new WorldCollider.IntersectionsEnumeratorVirtCall(worldCollider, AnyIntersectionable.From(new Sphere(pos, SphereRadius)));
            while (e2.MoveNext())
                intersections.Add(e2.Current);
            results.Add(intersections.ToList());

            intersections.Clear();
            var e3 = new MergedCollider.IntersectionsEnumerator<Sphere, IntersectionQueries>(mergedCollider, new Sphere(pos, SphereRadius));
            while (e3.MoveNext())
                intersections.Add(e3.Current);
            results.Add(intersections.ToList());

            intersections.Clear();
            var e4 = new MergedCollider.IntersectionsEnumeratorVirtCall(mergedCollider, new Sphere(pos, SphereRadius));
            while (e4.MoveNext())
                intersections.Add(e4.Current);
            results.Add(intersections.ToList());

            intersections.Clear();
            simd128Collider.IntersectionsList(new(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            simd256Collider.IntersectionsList(new(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            simd512Collider.IntersectionsList(new(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            intersections.Clear();
            simd512Collider.IntersectionsListLB(new(pos, SphereRadius), intersections);
            results.Add(intersections.ToList());

            for (int j = 0; j < results.Count; j++)
                results[j] = results[j].OrderBy(i => i.Point.X).ThenBy(i => i.Point.Y).ThenBy(i => i.Point.Z).ToList();

            var i = results.IndexOf(l => l.Count != results.First().Count);
            if (i >= 0)
            {
                PrintSet(i.ToString(), results.First(), results[i]);

                throw new Exception($"NOPE case {caseI} {i}: {results[i].Count} != {results.First().Count} ({pos.X:F3} | {pos.Y:F3} | {pos.Z:F3})");
            }
        }
    }

    private static void PrintSet(string name, List<Intersection> a, List<Intersection> b)
    {
        var onlyA = FindUniques(a, b);
        var onlyB = FindUniques(b, a);
        if (onlyA.Any())
        {
            Console.WriteLine("Only in A:");
            onlyA.ForEach(Print);
        }
        if (onlyB.Any())
        {
            Console.WriteLine("Only in B:");
            onlyB.ForEach(Print);
        }

    }

    private static void Print(Intersection i)
    {
        Console.Write($"    ({i.Point.X:F3} | {i.Point.Y:F3} | {i.Point.Z:F3}) ");
        if (i.TriangleId == null)
            Console.WriteLine("null");
        else
            Console.WriteLine($"{i.TriangleId.Value.AtomicIdx}, {i.TriangleId.Value.TriangleIdx}, {i.Triangle.IsDegenerated}");
    }

    private static List<Intersection> FindUniques(List<Intersection> these, List<Intersection> butnotthese) =>
        these.Where(ti => butnotthese.All(ni => !AreEqualEnough(ti, ni))).OrderBy(i => i.TriangleId!.Value.AtomicIdx).ThenBy(i => i.TriangleId!.Value.TriangleIdx).ToList();

    private static bool AreEqualEnough(Intersection a, Intersection b)
    {

        if (a.TriangleId != b.TriangleId)
            return false;
            return true;
        return AreEqualEnough(a.Point, b.Point);
    }

    private static bool AreEqualEnough(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.0001;
}
