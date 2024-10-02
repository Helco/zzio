using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using zzio.rwbs;
using zzio;

namespace zzre;

public sealed class MergedCollider : TreeCollider
{
    public readonly Triangle[] triangles;
    public readonly WorldTriangleId[] triangleIds;

    public override ReadOnlySpan<Triangle> Triangles => triangles;
    public override ReadOnlySpan<WorldTriangleId> WorldTriangleIds => triangleIds;

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
            (acc, section) => Vector3.Max(acc, Vector3.Max(section.bbox1, section.bbox2)));
        var box = Box.FromMinMax(boxMin, boxMax);

        var triangleCount = atomicCollisions.Sum(s => s.map.Length);
        var triangles = new Triangle[triangleCount];
        var triangleIds = new WorldTriangleId[triangleCount];
        var baseMapIndices = new int[atomicSections.Length];
        var triangleCounts = new int[atomicSections.Length]; // that is original count but without the degenerated ones
        var map = new int[triangleCount];
        var triangleI = 0;
        for (int sectionI = 0; sectionI < atomicSections.Length; sectionI++)
        {
            var section = atomicCollisions[sectionI];
            var localTriangles = atomicSections[sectionI].triangles;
            var vertices = atomicSections[sectionI].vertices;
            baseMapIndices[sectionI] = triangleI;
            for (int localTriangleI = 0; localTriangleI < section.map.Length; localTriangleI++)
            {
                var t = localTriangles[section.map[localTriangleI]];
                triangles[triangleI] = new(vertices[t.v1], vertices[t.v2], vertices[t.v3]);
                if (triangles[triangleI].IsDegenerated)
                    continue;
                triangleIds[triangleI] = new(sectionI, section.map[localTriangleI]);
                map[triangleI] = triangleI;
                triangleI++;
            }
            triangleCounts[sectionI] = triangleI - baseMapIndices[sectionI];
        }

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

            splits[planeSplitI] = new()
            {
                left = ProcessSubSection(splitType, plane.leftValue, leftSection),
                right = ProcessSubSection(splitType, plane.rightValue, rightSection)
            };
        }

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

                default: throw new ArgumentException("Unexpected section type", nameof(section));
            }
        }
    }

    /*

    private static readonly Vector256<int> NegateRightMask256 = Vector256
            .Create(uint.MinValue, uint.MaxValue, uint.MinValue, uint.MaxValue,
                    uint.MinValue, uint.MaxValue, uint.MinValue, uint.MaxValue)
            .AsInt32();

    void abc512(Stack<int> splitStack, in Collision3Split split, in Sphere sphere)
    {
        Vector512<float> sphereCenterV = sphere.Center.AsVector128().ToVector256().ToVector512();

        var compareValues = Vector512.Shuffle(sphereCenterV, Vector512.Create(
            split.leftLeftType, split.leftLeftType, split.leftRightType, split.leftRightType,
            split.rightLeftType, split.rightLeftType, split.rightRightType, split.rightRightType,
            split.leftType, split.leftType, split.rightType, split.rightType,
            split.topType, split.topType, split.topType, split.topType
        ));

        var diff = Vector512.Abs(compareValues - split.limits);
        var compareFull = Vector512.GreaterThan(diff, Vector512.Create(sphere.Radius)).AsInt32();
        var compareHalf = compareFull.GetUpper();
        var compareQuarter = Vector128.BitwiseAnd(compareHalf.GetUpper(), compareHalf.GetLower());
        compareHalf = Vector256.Create(compareQuarter, compareQuarter);
        var compare = Vector256.BitwiseAnd(compareFull.GetLower(), compareHalf);

        compare = Vector256.Xor(compare, NegateRightMask256);
        var compareBits = Vector256.ExtractMostSignificantBits(compare);
        var childIndices = Vector256.Shuffle(Indices256, ShuffleControls256[compareBits]);
        var childCount = BitOperations.PopCount(compareBits);

        for (int i = 0; i < childCount; i++)
        {
            var (index, count) = split.children[childIndices[i]];
            if (count == RWCollision.SplitCount)
                splitStack.Push(index);
            else if (count > 0)
                IntersectionListLeaf(sphere, index, count);
        }
    }*/
}
