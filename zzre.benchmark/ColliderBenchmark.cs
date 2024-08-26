using System;
using System.Collections.Generic;
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

using MyJobAttribute = BenchmarkDotNet.Attributes.LongRunJobAttribute;

namespace zzre.benchmark;

[MemoryDiagnoser]
[MyJob]
[MinIterationCount(10), MaxIterationCount(1000)]
public class ColliderBenchmark
{
    private const int Seed = 12345;
    private const string ArchivePath = @"C:\dev\zanzarah\Resources\DATA_0.PAK";
    private const string WorldPath = "Resources/Worlds/sc_1243.bsp";
    private const float SphereRadius = 2.0f;
    private const int CaseCount = 1000;
    private readonly WorldCollider worldCollider;
    private readonly Vector3[] cases;
    private List<Intersection> intersections = new(256);

    public ColliderBenchmark()
    {
        var archive = new PAKParallelResourcePool(ArchivePath);
        using var worldStream = archive.FindAndOpen(WorldPath)
            ?? throw new FileNotFoundException($"Could not open world geometry: " + WorldPath);
        var rwWorld = Section.ReadNew(worldStream) as RWWorld
            ?? throw new IOException("Could not read world geometry: " + WorldPath);
        worldCollider = new(rwWorld);

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
            foreach (var intersection in worldCollider.IntersectionsGeneratorOld<Sphere, IntersectionQueries>(new Sphere(pos, SphereRadius)))
                f += intersection.Point.X;
        }
        return f;
    }

    [Benchmark]
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

    [Benchmark]
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

    [Benchmark]
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
}
