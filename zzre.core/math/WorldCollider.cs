using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;
using zzio;
using System.Diagnostics;
using System.Numerics;
using System;

namespace zzre;

/*
 * For worlds we merge the atomic sections and the underlying RWCollisions together
 * This brings a bit more performance and a whole lot less complexity at runtime
 * Unfortunately also some amount of memory
 * We still have a sub-type to be able to query atomic sections and triangles by id
 */

public sealed class WorldCollider : TreeCollider
{
    private readonly RWAtomicSection[] atomicSections;

    private WorldCollider(
        Box coarse,
        ReadOnlyMemory<CollisionSplit> splits,
        ReadOnlyMemory<Triangle> triangles,
        ReadOnlyMemory<WorldTriangleId> triangleIds,
        RWAtomicSection[] atomicSections)
        : base(coarse, splits, triangles, triangleIds)
    {
        this.atomicSections = atomicSections;
    }

    public static WorldCollider Create(RWWorld world)
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

        // figure out AABB
        var boxMin = atomicSections.Aggregate(Vector3.One * float.PositiveInfinity,
            (acc, section) => Vector3.Min(acc, Vector3.Min(section.bbox1, section.bbox2)));
        var boxMax = atomicSections.Aggregate(Vector3.One * float.NegativeInfinity,
            (acc, section) => Vector3.Max(acc, Vector3.Max(section.bbox1, section.bbox2)));
        var box = Box.FromMinMax(boxMin, boxMax);

        // unmap triangles and remove degenerated ones
        var triangleCount = atomicCollisions.Sum(s => s.map.Length);
        var triangles = new Triangle[triangleCount];
        var triangleIds = new WorldTriangleId[triangleCount];
        var baseMapIndices = new int[atomicSections.Length];
        var triangleCounts = new int[atomicSections.Length]; // that is original count but without the degenerated ones
        var triangleI = 0;
        for (int sectionI = 0; sectionI < atomicSections.Length; sectionI++)
        {
            var section = atomicCollisions[sectionI];
            var localTriangles = atomicSections[sectionI].triangles;
            var vertices = atomicSections[sectionI].vertices;
            baseMapIndices[sectionI] = triangleI;
            for (int localTriangleI = 0; localTriangleI < section.map.Length; localTriangleI++)
            {
                var mappedTriangleI = section.map[localTriangleI];
                if (mappedTriangleI >= 0 && mappedTriangleI < localTriangles.Length)
                {
                    var t = localTriangles[mappedTriangleI];
                    triangles[triangleI] = new(vertices[t.v1], vertices[t.v2], vertices[t.v3]);
                }
                else
                    triangles[triangleI] = new(Vector3.Zero, Vector3.Zero, Vector3.Zero);
                if (triangles[triangleI].IsDegenerated)
                    triangles[triangleI] = new(MathEx.Vector3NaN, MathEx.Vector3NaN, MathEx.Vector3NaN);
                triangleIds[triangleI] = new(sectionI, section.map[localTriangleI]);
                triangleI++;
            }
            triangleCounts[sectionI] = triangleI - baseMapIndices[sectionI];
        }

        int splitCount =
            atomicCollisions.Sum(c => c.splits.Length) +
            Math.Max(1, world.FindAllChildrenById(SectionId.PlaneSection, true).Count());
        var rootPlane = world.FindChildById(SectionId.PlaneSection, false) as RWPlaneSection;
        var rootAtomic = world.FindChildById(SectionId.AtomicSection, false);
        var rootSection = rootPlane ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry");

        // add dummy plane for root atomic worlds
        rootPlane ??= new RWPlaneSection()
        {
            sectorType = RWPlaneSectionType.XPlane,
            leftValue = float.PositiveInfinity,
            rightValue = float.PositiveInfinity,
            children = [rootAtomic!, new RWString()]
        };

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

            splits[planeSplitI] = new()
            {
                left = ProcessSubSection(splitType, plane.leftValue, leftSection),
                right = ProcessSubSection(splitType, plane.rightValue, rightSection)
            };
        }

        return new WorldCollider(box, splits, triangles, triangleIds, atomicSections);

        CollisionSector ProcessSubSection(CollisionSectorType splitType, float value, Section section) => section switch
        {
            RWPlaneSection plane => ProcessSubPlane(splitType, value, plane),
            RWAtomicSection atomic => ProcessSubAtomic(splitType, value, atomic),
            RWString => new() // dummy atomic
            {
                type = splitType,
                value = value,
                count = 0,
                index = -1
            },
            _ => throw new ArgumentException("Unexpected section type", nameof(section))
        };

        CollisionSector ProcessSubPlane(CollisionSectorType splitType, float value, RWPlaneSection subPlane)
        {
            var subPlaneSplitI = splitI++;
            planeStack.Push((subPlane, subPlaneSplitI));
            return new()
            {
                type = splitType,
                value = value,
                count = RWCollision.SplitCount,
                index = subPlaneSplitI
            };
        }

        CollisionSector ProcessSubAtomic(CollisionSectorType splitType, float value, RWAtomicSection atomic)
        {
            var atomicId = atomicSections.IndexOf(atomic);
            Debug.Assert(atomicId >= 0);
            var subSplits = atomicCollisions[atomicId].splits;
            if (subSplits.Length == 1 && float.IsInfinity(subSplits[0].left.value))
            {
                // naive collision where we can save the dummy split
                return new()
                {
                    type = splitType,
                    value = value,
                    index = baseMapIndices[atomicId],
                    count = triangleCounts[atomicId],
                };
            }

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
        }
    }

    public WorldTriangleInfo GetTriangleInfo(WorldTriangleId id) =>
        new(atomicSections[id.AtomicIdx], atomicSections[id.AtomicIdx].triangles[id.TriangleIdx]);
}
