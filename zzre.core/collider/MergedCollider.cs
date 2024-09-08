using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using zzio.rwbs;
using zzio;
using Silk.NET.Maths;

namespace zzre;

public sealed class MergedCollider : TreeCollider<Box>
{
    private readonly Triangle[] triangles;
    private readonly WorldTriangleId[] triangleIds;

    protected override int TriangleCount => triangles.Length;

    private MergedCollider(Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds) : base(coarse, collision)
    {
        this.triangles = triangles;
        this.triangleIds = triangleIds;
    }

    public override (Triangle Triangle, WorldTriangleId TriangleId) GetTriangle(int i) =>
        (triangles[i], triangleIds[i]);

    public static MergedCollider Create(RWWorld world)
    {
        var atomicSections = world
            .FindAllChildrenById(SectionId.AtomicSection, recursive: true)
            .Cast<RWAtomicSection>()
            .ToArray();
        var atomicCollisions = atomicSections
            .Select(section => section.FindChildById(SectionId.CollisionPLG, true) ??
                               CreateNaiveCollision(section.triangles.Length))
            .Cast<RWCollision>()
            .ToArray();

        var boxMin = atomicSections.Aggregate(Vector3.One * float.PositiveInfinity,
            (acc, section) => Vector3.Min(acc, Vector3.Min(section.bbox1, section.bbox2)));
        var boxMax = atomicSections.Aggregate(Vector3.One * float.NegativeInfinity,
            (acc, section) => Vector3.Min(acc, Vector3.Min(section.bbox1, section.bbox2)));
        var box = Box.FromMinMax(boxMin, boxMax);

        var triangles = atomicSections
            .SelectMany(zip => zip.triangles
                .Select(t => new Triangle(zip.vertices[t.v1], zip.vertices[t.v2], zip.vertices[t.v3])))
            .ToArray();
        var triangleIds = atomicSections
            .SelectMany((zip, i) => Enumerable.Range(0, zip.triangles.Length).Select(j => new WorldTriangleId(i, j)))
            .ToArray();
        var baseMapIndices = atomicCollisions
            .SubSums(0, (prev, col) => prev + col.map.Length)
            .ToArray();
        var baseTriangleIndices = atomicSections
            .SubSums(0, (prev, section) => prev + section.triangles.Length)
            .ToArray();
        var map = atomicCollisions
            .SelectMany((s, si) => s.map.Select(mi => mi + baseTriangleIndices[si]))
            .ToArray();

        int splitCount =
            atomicCollisions.Sum(c => c.splits.Length) +
            world.FindAllChildrenById(SectionId.PlaneSection, true).Count();
        var rootPlane = world.FindChildById(SectionId.PlaneSection, false) as RWPlaneSection;
        var rootAtomic = world.FindChildById(SectionId.AtomicSection, false);
        var rootSection = rootPlane ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry");

        if (rootSection == rootAtomic)
            throw new NotImplementedException("I have not yet implemented the single-atomic case");

        var splits = new CollisionSplit[splitCount];
        var splitI = 1;
        var planeStack = new Stack<(RWPlaneSection, int)>();
        planeStack.Push((rootPlane!, 0));
        while (planeStack.TryPop(out var tuple))
        {
            var (plane, planeSplitI) = tuple;
            var leftSection = plane.children[0];
            var rightSection = plane.children[1];
            var splitType = plane.sectorType switch
            {
                RWPlaneSectionType.XPlane => CollisionSectorType.X,
                RWPlaneSectionType.YPlane => CollisionSectorType.Y,
                RWPlaneSectionType.ZPlane => CollisionSectorType.Z,
                _ => throw new InvalidDataException()
            };

            // TODO: Check performance when creating depth-first splits
            splits[planeSplitI] = new()
            {
                left = ProcessSubSection(splitType, plane.leftValue, leftSection),
                right = ProcessSubSection(splitType, plane.rightValue, rightSection)
            };
        }
        Debug.Assert(splitI == splits.Length);
        return new MergedCollider(box, new()
        {
            map = map,
            splits = splits
        }, triangles, triangleIds);

        CollisionSector ProcessSubSection(CollisionSectorType splitType, float value, Section section)
        {
            switch (section)
            {
                case RWPlaneSection subPlane:
                    var subPlaneSplitI = splitI++;
                    planeStack.Push((subPlane, subPlaneSplitI));
                    return new()
                    {
                        type = splitType,
                        value = value,
                        count = RWCollision.SplitCount,
                        index = subPlaneSplitI
                    };

                case RWAtomicSection atomic:
                    var atomicId = atomicSections.IndexOf(atomic);
                    Debug.Assert(atomicId >= 0);
                    var subSplits = atomicCollisions[atomicId].splits;
                    var startSplitI = splitI;
                    splitI += subSplits.Length;

                    subSplits.CopyTo(splits, startSplitI);
                    foreach (ref var subSplit in splits.AsSpan(startSplitI, subSplits.Length))
                    {
                        subSplit.left.index += subSplit.left.count == RWCollision.SplitCount
                            ? startSplitI
                            : baseMapIndices[atomicId];
                        subSplit.right.index += subSplit.right.count == RWCollision.SplitCount
                            ? startSplitI
                            : baseMapIndices[atomicId];
                    }

                    return new()
                    {
                        type = splitType,
                        value = value,
                        count = RWCollision.SplitCount,
                        index = startSplitI
                    };

                default: throw new ArgumentException("Unexpected section type", nameof(section));
            }
        }
    }
}
