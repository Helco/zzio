using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using zzio.rwbs;

namespace zzre;

public abstract class ThreeSplitCollider
{
    protected struct Collision3Split
    {
        [InlineArray(8)]
        public struct Children
        {
            public (int index, int count) _element0;
        }

        public Vector512<float> limits; // 8xSub 4xCenter 2xTop 2xZero
        public Children children;
        public int
            leftLeftType, leftRightType, rightLeftType, rightRightType,
            leftType, rightType,
            topType;
    }

    protected class Data
    {
        public readonly List<Collision3Split> splits;
        public readonly int[] map;
        public readonly Triangle[] triangles;
        public readonly WorldTriangleId[] triangleIds;
        public readonly Box coarse;

        public Data(Box coarse,
            Triangle[] triangles, WorldTriangleId[] triangleIds,
            List<Collision3Split> splits, int[] map)
        {
            this.coarse = coarse;
            this.triangles = triangles;
            this.triangleIds = triangleIds;
            this.splits = splits;
            this.map = map;
        }
    }

    protected readonly Data data;

    protected ThreeSplitCollider(Data data)
    {
        this.data = data;
    }

    // TODO: Maybe there is a good way to generate 2splits from RWWorld directly? (maybe look into abstraction)

    protected static Data Create(
        Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds)
    {
        var splits = new List<Collision3Split>(collision.splits.Length * 2 / 3); // TODO: Check the heuristic
        ProcessSplit(0);

        int ProcessSplit(int topSplitI)
        {
            var threeSplitI = splits.Count;
            splits.Add(new Collision3Split { topType = 9999 }); // a split that will definitely break something visibly
            var threeSplit = new Collision3Split();

            var topSplit = collision.splits[topSplitI];
            threeSplit.topType = (int)topSplit.left.type / 4;
            
            Span<(int index, int count)> children = threeSplit.children;
            var (leftSubSubValues, leftSubValues) = ProcessSubSplit(topSplit.left,
                ref threeSplit.leftType, ref threeSplit.leftLeftType, ref threeSplit.leftRightType,
                children[..4]);
            var (rightSubSubValues, rightSubValues) = ProcessSubSplit(topSplit.right,
                ref threeSplit.rightType, ref threeSplit.rightLeftType, ref threeSplit.rightRightType,
                children[4..]);
            threeSplit.limits = Vector512.Create(
                Vector256.Create(leftSubSubValues, rightSubSubValues),
                Vector256.Create(
                    Vector128.Create(leftSubValues, rightSubValues),
                    Vector128.Create(topSplit.left.value, topSplit.left.value, topSplit.right.value, topSplit.right.value)
                )
            );
            
            splits[threeSplitI] = threeSplit;
            return threeSplitI;
        }

        (Vector128<float> subSubValues, Vector64<float> subValues) ProcessSubSplit(CollisionSector sector,
            ref int subType, ref int subSubLeftType, ref int subSubRightType,
            Span<(int index, int count)> children)
        {
            if (sector.count != RWCollision.SplitCount)
            {
                // the second layer is unfortunately an atomic, so we add a pseudo two-split
                subType = subSubLeftType = subSubRightType = 0;
                children[0] = (sector.index, sector.count);
                children[1..].Fill((-1, 0));
                return (Vector128.Create(float.PositiveInfinity), Vector64.Create(float.PositiveInfinity)); // always just left sector used
            }

            var subSplit = collision.splits[sector.index];
            subType = (int)subSplit.left.type / 4;
            var subSubLeftValues = ProcessSubSubSplit(subSplit.left, ref subSubLeftType, children[..2]);
            var subSubRightValues = ProcessSubSubSplit(subSplit.right, ref subSubRightType, children[2..]);

            return (Vector128.Create(subSubLeftValues, subSubRightValues),
                Vector64.Create(subSplit.left.value, subSplit.right.value));
        }

        Vector64<float> ProcessSubSubSplit(CollisionSector sector, ref int subSubType, Span<(int index, int count)> children)
        {
            if (sector.count != RWCollision.SplitCount)
            {
                // the third layer is unfortunately an atomic so we add a pseudo split
                subSubType = 0;
                children[0] = (sector.index, sector.count);
                children[1] = (-1, 0);
                return Vector64.Create(float.PositiveInfinity); // always just left sector used
            }

            var subSubSplit = collision.splits[sector.index];
            subSubType = (int)subSubSplit.left.type / 4;
            children[0] = ProcessSubSubSubSplit(subSubSplit.left);
            children[1] = ProcessSubSubSubSplit(subSubSplit.right);
            return Vector64.Create(subSubSplit.left.value, subSubSplit.right.value);
        }

        (int index, int count) ProcessSubSubSubSplit(CollisionSector subSector) =>
            subSector.count == RWCollision.SplitCount
            ? (ProcessSplit(subSector.index), RWCollision.SplitCount)
            : (subSector.index, subSector.count);

        return new(coarse, triangles, triangleIds, splits, collision.map);
    }

    protected static Stack<int> splitStack = new Stack<int>(64);

    protected void IntersectionListLeaf(in Sphere sphere, int index, int count, List<Intersection> intersections)
    {
        for (int i = 0; i < count; i++)
        {
            var triangleI = data.map[i + index];
            var triangle = data.triangles[triangleI];
            var intersection = IntersectionQueries.Intersect(triangle, sphere);
            if (intersection != null)
                intersections.Add(intersection.Value with { TriangleId = data.triangleIds[triangleI] });
        }
    }
}

public sealed class SIMD512Collider : ThreeSplitCollider
{
    public SIMD512Collider(
        Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds)
        : base(Create(coarse, collision, triangles, triangleIds))
    {}

    private static readonly Vector512<float> RadiusFactors = Vector512.Create(
        +1f, -1f, +1f, -1f,
        +1f, -1f, +1f, -1f,
        +1f, -1f, +1f, -1f,
        +1f, +1f, -1f, -1f
    );

    private static readonly byte[] Duplicated4Bits =
    [
        0b00_00_00_00,
        0b00_00_00_11,
        0b00_00_11_00,
        0b00_00_11_11,
        0b00_11_00_00,
        0b00_11_00_11,
        0b00_11_11_00,
        0b00_11_11_11,
        0b11_00_00_00,
        0b11_00_00_11,
        0b11_00_11_00,
        0b11_00_11_11,
        0b11_11_00_00,
        0b11_11_00_11,
        0b11_11_11_00,
        0b11_11_11_11
    ];

    [MethodImpl(MathEx.MIOptions)]
    public void IntersectionsList(in Sphere sphere, List<Intersection> intersections)
    {
        if (!data.coarse.Intersects(sphere))
            return;
        var sphereCenterV = sphere.Center.AsVector128().ToVector256().ToVector512();
        var sphereRadiusV = Vector512.Create(sphere.Radius) * RadiusFactors;

        splitStack.Clear();
        splitStack.Push(0);
        while (splitStack.TryPop(out var splitI))
        {
            var split = data.splits[splitI];

            var compareValues = Vector512.Shuffle(sphereCenterV, Vector512.Create(
                split.leftLeftType, split.leftLeftType, split.leftRightType, split.leftRightType,
                split.rightLeftType, split.rightLeftType, split.rightRightType, split.rightRightType,
                split.leftType, split.leftType, split.rightType, split.rightType,
                split.topType, split.topType, split.topType, split.topType
            ));
            
            var diff = compareValues - split.limits;

            var compareMask = Vector512.GreaterThan(diff, sphereRadiusV).AsInt32();
            var compareFull = Vector512.ExtractMostSignificantBits(compareMask);
            compareFull ^= 0b0011010101010101; // finish sideOf queries
            var compareCenter = (compareFull >> 8) & (compareFull >> 12) & 0b1111; // top->center layer
            compareCenter = Duplicated4Bits[compareCenter];
            var compare = compareFull & compareCenter & 255; // center->bottom layer

            for (int i = 0; i < 8; i++)
            {
                if ((compare & (1UL << i)) == 0)
                    continue;
                int count = split.children[i].count;
                if (count == RWCollision.SplitCount)
                    splitStack.Push(split.children[i].index);
                else
                    IntersectionListLeaf(sphere, split.children[i].index, count, intersections);
            }
        }
    }

    private static readonly Vector256<int> Indices256 = Vector256.Create(
        0, 1, 2, 3, 4, 5, 6, 7);
    private static readonly Vector256<int>[] ShuffleControls256 = Enumerable
        .Range(0, 1 << 8)
        .Select(i =>
        {
            var control = Vector256<int>.Zero;
            var index = 0;
            for (int bit = 0; bit < 8 && i > 0; bit++, i >>= 1)
            {
                if ((i & 1) != 0)
                    control = Vector256.WithElement(control, index++, bit);
            }
            return control;
        })
        .ToArray();

    [MethodImpl(MathEx.MIOptions)]
    public void IntersectionsListLB(in Sphere sphere, List<Intersection> intersections)
    {
        if (!data.coarse.Intersects(sphere))
            return;
        var sphereCenterV = sphere.Center.AsVector128().ToVector256().ToVector512();
        var sphereRadiusV = Vector512.Create(sphere.Radius) * RadiusFactors;

        splitStack.Clear();
        splitStack.Push(0);
        while (splitStack.TryPop(out var splitI))
        {
            var split = data.splits[splitI];

            var compareValues = Vector512.Shuffle(sphereCenterV, Vector512.Create(
                split.leftLeftType, split.leftLeftType, split.leftRightType, split.leftRightType,
                split.rightLeftType, split.rightLeftType, split.rightRightType, split.rightRightType,
                split.leftType, split.leftType, split.rightType, split.rightType,
                split.topType, split.topType, split.topType, split.topType
            ));
            
            var diff = compareValues - split.limits;

            var compareMask = Vector512.GreaterThan(diff, sphereRadiusV).AsInt32();
            var compareFull = Vector512.ExtractMostSignificantBits(compareMask);
            compareFull ^= 0b0011010101010101; // finish sideOf queries
            var compareCenter = (compareFull >> 8) & (compareFull >> 12) & 0b1111; // top->center layer
            compareCenter = Duplicated4Bits[compareCenter];
            var compare = compareFull & compareCenter & 255; // center->bottom layer
            var childIndices = Vector256.Shuffle(Indices256, ShuffleControls256[compare]);
            var childCount = BitOperations.PopCount((uint)compare);

            for (int i = 0; i < childCount; i++)
            {
                var (index, count) = split.children[childIndices[i]];
                if (count == RWCollision.SplitCount)
                    splitStack.Push(index);
                else
                    IntersectionListLeaf(sphere, index, count, intersections);
            }
        }
    }
}
