using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using zzio.rwbs;

namespace zzre;

public abstract class TwoSplitCollider
{
    protected struct Collision2Split
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

    protected class Data
    {
        public readonly List<Collision2Split> splits;
        public readonly int[] map;
        public readonly Triangle[] triangles;
        public readonly WorldTriangleId[] triangleIds;
        public readonly Box coarse;

        public Data(Box coarse,
            Triangle[] triangles, WorldTriangleId[] triangleIds,
            List<Collision2Split> splits, int[] map)
        {
            this.coarse = coarse;
            this.triangles = triangles;
            this.triangleIds = triangleIds;
            this.splits = splits;
            this.map = map;
        }
    }

    protected readonly Data data;

    protected TwoSplitCollider(Data data)
    {
        this.data = data;
    }

    // TODO: Maybe there is a good way to generate 2splits from RWWorld directly? (maybe look into abstraction)

    protected static Data Create(
        Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds)
    {
        var splits = new List<Collision2Split>(collision.splits.Length * 2 / 3); // TODO: Check the heuristic
        ProcessSplit(0);

        int ProcessSplit(int topSplitI)
        {
            var twoSplitI = splits.Count;
            splits.Add(new Collision2Split { topType = 9999 }); // a split that will definitely break something visibly
            var twoSplit = new Collision2Split();

            var topSplit = collision.splits[topSplitI];
            twoSplit.topType = (int)topSplit.left.type / 4;
            twoSplit.topLeftValue = topSplit.left.value;
            twoSplit.topRightValue = topSplit.right.value;
            
            Span<(int index, int count)> children = twoSplit.children;
            var (leftLeft, leftRight) = ProcessSubSplit(topSplitI, topSplit.left, ref twoSplit.leftType, children[..2]);
            var (rightLeft, rightRight) = ProcessSubSplit(topSplitI, topSplit.right, ref twoSplit.rightType, children[2..]);
            twoSplit.subLimits = Vector128.Create(leftLeft, leftRight, rightLeft, rightRight);
            
            splits[twoSplitI] = twoSplit;
            return twoSplitI;
        }

        (float left, float right) ProcessSubSplit(int t, CollisionSector sector, ref int type, Span<(int index, int count)> children)
        {
            if (sector.count != RWCollision.SplitCount)
            {
                // the second layer is unfortunately an atomic, so we add a pseudo split
                type = 0; // W dimension will always be zero
                children[0] = (sector.index, sector.count);
                children[1] = (-1, 0);
                return (float.PositiveInfinity, float.PositiveInfinity); // always just left sector used
            }

            var subSplit = collision.splits[sector.index];
            type = (int)subSplit.left.type / 4;
            children[0] = ProcessSubSubSplit(subSplit.left);
            children[1] = ProcessSubSubSplit(subSplit.right);
            return (subSplit.left.value, subSplit.right.value);
        }

        (int index, int count) ProcessSubSubSplit(CollisionSector subSector) =>
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

public sealed class SIMD128Collider : TwoSplitCollider
{
    public SIMD128Collider(
        Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds)
        : base(Create(coarse, collision, triangles, triangleIds))
    {}

    private static readonly Vector128<float> RadiusFactorsTop = Vector128
            .Create(+1f, +1f, -1f, -1f);

    private static readonly Vector128<float> RadiusFactorsSub = Vector128
            .Create(+1f, -1f, +1f, -1f);

    [MethodImpl(MathEx.MIOptions)]
    public void IntersectionsList(in Sphere sphere, List<Intersection> intersections)
    {
        if (!data.coarse.Intersects(sphere))
            return;
        var sphereCenterV = sphere.Center.AsVector128();
        var sphereRadiusTopV = Vector128.Create(sphere.Radius) * RadiusFactorsTop;
        var sphereRadiusSubV = Vector128.Create(sphere.Radius) * RadiusFactorsSub;

        splitStack.Clear();
        splitStack.Push(0);
        while (splitStack.TryPop(out var splitI))
        {
            var split = data.splits[splitI];

            var topCompareValues = Vector128.Shuffle(sphereCenterV, Vector128.Create(split.topType));
            var subCompareValues = Vector128.Shuffle(sphereCenterV, Vector128.Create(
                split.leftType, split.leftType, split.rightType, split.rightType));

            var topLimits = Vector128.Create(split.topLeftValue, split.topLeftValue, split.topRightValue, split.topRightValue);

            var topDiff = topCompareValues - topLimits;
            var subDiff = subCompareValues - split.subLimits;

            var topCompareMask = Vector128.GreaterThan(topDiff, sphereRadiusTopV).AsInt32();
            var subCompareMask = Vector128.GreaterThan(subDiff, sphereRadiusSubV).AsInt32();
            var topCompare = Vector128.ExtractMostSignificantBits(topCompareMask) ^ 0b0011;
            var subCompare = Vector128.ExtractMostSignificantBits(subCompareMask) ^ 0b0101;
            var compare = topCompare & subCompare;

            for (int i = 0; i < 4; i++)
            {
                if ((compare & (1 << i)) == 0)
                    continue;
                int count = split.children[i].count;
                if (count == RWCollision.SplitCount)
                    splitStack.Push(split.children[i].index);
                else
                    IntersectionListLeaf(sphere, split.children[i].index, count, intersections);
            }
        }
    }
}

public sealed class SIMD256Collider : TwoSplitCollider
{
    public SIMD256Collider(
        Box coarse, RWCollision collision,
        Triangle[] triangles, WorldTriangleId[] triangleIds)
        : base(Create(coarse, collision, triangles, triangleIds))
    {}

    private static readonly Vector256<float> RadiusFactors = Vector256
            .Create(+1f, -1f, +1f, -1f, +1f, +1f, -1f, -1f);

    [MethodImpl(MathEx.MIOptions)]
    public void IntersectionsList(in Sphere sphere, List<Intersection> intersections)
    {
        if (!data.coarse.Intersects(sphere))
            return;
        var sphereCenterV = sphere.Center.AsVector128().ToVector256();
        var sphereRadiusV = Vector256.Create(sphere.Radius) * RadiusFactors; 

        splitStack.Clear();
        splitStack.Push(0);
        while (splitStack.TryPop(out var splitI))
        {
            var split = data.splits[splitI];

            var compareValues = Vector256.Shuffle(sphereCenterV, Vector256.Create(
                split.leftType, split.leftType, split.rightType, split.rightType,
                split.topType, split.topType, split.topType, split.topType));

            var limits = Vector256.Create(
                split.subLimits,
                Vector128.Create(split.topLeftValue, split.topLeftValue, split.topRightValue, split.topRightValue));

            var diff = compareValues - limits;
            var compareMask = Vector256.GreaterThan(diff, sphereRadiusV);
            var compare = Vector256.ExtractMostSignificantBits(compareMask) ^ 0b00110101;
            compare = compare & (compare >> 4) & 0b1111;

            for (int i = 0; i < 4; i++)
            {
                if ((compare & (1 << i)) == 0)
                    continue;
                var (index, count) = split.children[i];
                if (count == RWCollision.SplitCount)
                    splitStack.Push(index);
                else if (count > 0)
                    IntersectionListLeaf(sphere, index, count, intersections);
            }
        }
    }
}
