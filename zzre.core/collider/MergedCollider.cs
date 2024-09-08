using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using zzio.rwbs;
using zzio;
using Silk.NET.Maths;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

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

    private static readonly Vector128<int> NegateRightMask = Vector128
            .Create(uint.MinValue, uint.MaxValue, uint.MinValue, uint.MaxValue)
            .AsInt32();

    struct Collision2Split
    {
        [InlineArray(4)]
        public struct Children
        {
            public (int index, int count) _element0;
        }

        public Vector128<float> subLimits;
        public float topLeftValue, topRightValue;
        public int topType, leftType, rightType;
        public Children children;
    }

    [MethodImpl(MathEx.MIOptions)]
    void abc(Stack<int> splitStack, in Collision2Split split, in Sphere sphere)
    {
        Vector128<float> sphereCenterV = sphere.Center.AsVector128();

        var topCompareValues = Vector128.Shuffle(sphereCenterV, Vector128.Create(split.topType));
        var subCompareValues = Vector128.Shuffle(sphereCenterV, Vector128.Create(
            split.leftType, split.leftType, split.rightType, split.rightType));

        var topLimits = Vector128.Create(split.topLeftValue, split.topLeftValue, split.topRightValue, split.topRightValue);

        var topDiff = Vector128.Abs(topCompareValues - topLimits);
        var subDiff = Vector128.Abs(subCompareValues - split.subLimits);

        var topCompare = Vector128.GreaterThan(topDiff, Vector128.Create(sphere.Radius));
        var subCompare = Vector128.GreaterThan(topDiff, Vector128.Create(sphere.Radius));

        var compare = Vector128.BitwiseAnd(topCompare, subCompare).AsInt32();
        compare = Vector128.Xor(compare, NegateRightMask);

        for (int i = 0; i < 4; i++)
        {
            if (compare.GetElement(i) != 0)
                continue;
            int count = split.children[i].count;
            if (count == RWCollision.SplitCount)
                splitStack.Push(split.children[i].index);
            else if (count > 0)
                IntersectionListLeaf(sphere, split.children[i].index, count);
        }
    }

    [MethodImpl(MathEx.MIOptions)]
    void abc256(Stack<int> splitStack, in Collision2Split split, in Sphere sphere)
    {
        var sphereCenterV = sphere.Center.AsVector128().ToVector256();

        var compareValues = Vector256.Shuffle(sphereCenterV, Vector256.Create(
            split.leftType, split.leftType, split.rightType, split.rightType,
            split.topType, split.topType, split.topType, split.topType));

        var limits = Vector256.Create(
            split.subLimits,
            Vector128.Create(split.topLeftValue, split.topLeftValue, split.topRightValue, split.topRightValue));

        var diff = Vector256.Abs(compareValues - limits);
        var compareFull = Vector256.GreaterThan(diff, Vector256.Create(sphere.Radius));
        var compare = Vector128.BitwiseAnd(compareFull.GetLower(), compareFull.GetUpper()).AsInt32();

        compare = Vector128.Xor(compare, NegateRightMask);

        for (int i = 0; i < 4; i++)
        {
            if (compare.GetElement(i) != 0)
                continue;
            int count = split.children[i].count;
            if (count == RWCollision.SplitCount)
                splitStack.Push(split.children[i].index);
            else if (count > 0)
                IntersectionListLeaf(sphere, split.children[i].index, count);
        }
    }

    void IntersectionListLeaf(in Sphere sphere, int index, int count)
    {

    }
}
