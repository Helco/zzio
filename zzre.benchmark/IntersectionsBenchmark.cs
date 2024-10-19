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
        worldCollider = WorldCollider.Create(rwWorld);
        worldColliderBL = new(rwWorld);

        var random = new Random(Seed);
        cases = new Vector3[CaseCount];
        var atomicSections = rwWorld
            .FindAllChildrenById(SectionId.AtomicSection, recursive: true)
            .Cast<RWAtomicSection>()
            .ToArray();
        var intersections = new List<Intersection>(64);
        for (int i = 0; i < cases.Length; i++)
        {
            while(true)
            {
                var atomic = random.NextOf(atomicSections);
                var bboxmin = Vector3.Min(atomic.bbox1, atomic.bbox2);
                var bboxmax = Vector3.Max(atomic.bbox1, atomic.bbox2);
                var pos = bboxmin + random.InPositiveCube() * (bboxmax - bboxmin);
                intersections.Clear();
                if (worldCollider.Intersections(new Sphere(pos, SphereRadius), intersections) > 0)
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

    [Benchmark]
    public float IntersectionsList()
    {
        float f = 0f;
        foreach (var pos in cases)
        {
            intersections.Clear();
            worldCollider.Intersections<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
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

            intersections.Clear();
            worldCollider.Intersections<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius), intersections);
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
