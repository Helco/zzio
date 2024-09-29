using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using zzio.rwbs;

namespace zzre;

public abstract partial class TreeCollider<TCoarse> : BaseGeometryCollider
    where TCoarse : struct, IIntersectable, IRaycastable
{
    private const int MaxTreeDepth = 64;
    private readonly bool hasSpans;
    public readonly TCoarse Coarse;

    public RWCollision Collision { get; }
    protected sealed override IRaycastable CoarseCastable => Coarse;
    protected sealed override IIntersectable CoarseIntersectable => Coarse;

    protected abstract int TriangleCount { get; }
    public abstract (Triangle Triangle, WorldTriangleId TriangleId) GetTriangle(int i);
    public virtual ReadOnlySpan<Triangle> Triangles => throw new NotSupportedException();
    public virtual ReadOnlySpan<WorldTriangleId> WorldTriangleIds => throw new NotSupportedException();

    protected TreeCollider(TCoarse coarse, RWCollision collision)
    {
        Collision = collision;
        this.Coarse = coarse;
        try
        {
            _ = Triangles;
            hasSpans = true;
        }
        catch(NotSupportedException)
        {
            hasSpans = false;
        }
    }

    protected static RWCollision CreateNaiveCollision(int triangleCount)
    {
        var split = new CollisionSplit()
        {
            left =
            {
                value = float.NegativeInfinity,
                type = CollisionSectorType.X,
                index = 0,
                count = 0
            },
            right =
            {
                value = float.NegativeInfinity,
                type = CollisionSectorType.X,
                index = 0,
                count = triangleCount
            }
        };
        return new RWCollision
        {
            splits = [split],
            map = Enumerable.Range(0, triangleCount).ToArray()
        };
    }

    public override Raycast? Cast(Ray ray, float maxDistTotal = float.PositiveInfinity) =>
        hasSpans ? CastNewInterface(ray, maxDistTotal) : CastLegacyInterface(ray, maxDistTotal);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Raycast? CastNewInterface(Ray ray, float maxDistTotal = float.PositiveInfinity)
    {
        Box aabb = Coarse switch
        {
            Sphere sphere => new Box(sphere.Center, Vector3.One * sphere.Radius * 2),
            Box box => box,
            _ => throw new NotImplementedException("Unsupported coarse primitive")
        };
        if (!ray.Cast(aabb, out var minDistBox, out var maxDistBox))
            return null;
        maxDistTotal = Math.Min(maxDistTotal, maxDistBox);

        var splits = Collision.splits;
        var map = Collision.map;
        var triangles = Triangles;
        var triangleIds = WorldTriangleIds;
        Raycast bestHit = new(maxDistTotal, Vector3.Zero, Vector3.One);

        var direction = ray.Direction;
        var invDirection = MathEx.Reciprocal(direction);

        var stackArray = ArrayPool<(CollisionSector, float, float)>.Shared.Rent(MaxTreeDepth);
        var stack = new StackOverSpan<(CollisionSector sector, float minDist, float maxDist)>(stackArray);
        stack.Push((new() { index = 0, count = RWCollision.SplitCount }, 0f, maxDistTotal));
        while (stack.TryPop(out var t))
        {
            var (sector, minDist, maxDist) = t;

            if (sector.count == RWCollision.SplitCount)
            {
                var split = splits[sector.index];
                var compIndex = split.left.type.ToIndex();
                var dir = direction.Component(compIndex);
                var start = ray.Start.Component(compIndex) + dir * minDist;
                var end = ray.Start.Component(compIndex) + dir * maxDist;
                var invDir = invDirection.Component(compIndex);
                if (dir > 0)
                {
                    // left -> right
                    if (split.right.value > end)
                        stack.Push().sector = split.left; // min/maxDist are reused
                    else if (split.left.value < start)
                        stack.Push().sector = split.right;
                    else
                    {
                        var rightMinDist = minDist + invDir * MathF.Max(0f, split.right.value - start);
                        stack.Push((split.right, rightMinDist, maxDist));
                        var leftMaxDist = maxDist + invDir * MathF.Min(0f, split.left.value - end);
                        stack.Push((split.left, minDist, leftMaxDist));
                    }
                }
                else
                {
                    // right -> left
                    if (split.left.value < end)
                        stack.Push().sector = split.right;
                    else if (split.right.value > start)
                        stack.Push().sector = split.left;
                    else
                    {
                        var leftMinDist = minDist + invDir * MathF.Max(0f, split.left.value - start);
                        stack.Push((split.left, leftMinDist, maxDist));
                        var rightMaxDist = maxDist + invDir * MathF.Min(0f, split.right.value - end);
                        stack.Push((split.right, minDist, rightMaxDist));
                    }
                }
            }
            else
            {
                for (int i = 0; i < sector.count; i++)
                {
                    var triI = sector.index + i;
                    var newHit = ray.TryCastUnsafe(triangles[triI]);
                    if (newHit.Distance < bestHit.Distance) // for misses: NaN < bestDistance is always false
                        bestHit = newHit with { TriangleId = triangleIds[triI] };
                }
            }
        }

        ArrayPool<(CollisionSector, float, float)>.Shared.Return(stackArray);
        return bestHit.Distance < maxDistTotal ? bestHit : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Raycast? CastLegacyInterface(Ray ray, float maxDistTotal = float.PositiveInfinity)
    {
        Box aabb = Coarse switch
        {
            Sphere sphere => new Box(sphere.Center, Vector3.One * sphere.Radius * 2),
            Box box => box,
            _ => throw new NotImplementedException("Unsupported coarse primitive")
        };
        if (!ray.Cast(aabb, out var minDistBox, out var maxDistBox))
            return null;
        maxDistTotal = Math.Min(maxDistTotal, maxDistBox);

        var splits = Collision.splits;
        var map = Collision.map;
        Raycast bestHit = new(maxDistTotal, Vector3.Zero, Vector3.One);

        var direction = ray.Direction;
        var invDirection = MathEx.Reciprocal(direction);

        var stackArray = ArrayPool<(CollisionSector, float, float)>.Shared.Rent(MaxTreeDepth);
        var stack = new StackOverSpan<(CollisionSector sector, float minDist, float maxDist)>(stackArray);
        stack.Push((new() { index = 0, count = RWCollision.SplitCount }, 0f, maxDistTotal));
        while (stack.TryPop(out var t))
        {
            var (sector, minDist, maxDist) = t;

            if (sector.count == RWCollision.SplitCount)
            {
                var split = splits[sector.index];
                var compIndex = split.left.type.ToIndex();
                var dir = direction.Component(compIndex);
                var start = ray.Start.Component(compIndex) + dir * minDist;
                var end = ray.Start.Component(compIndex) + dir * maxDist;
                var invDir = invDirection.Component(compIndex);
                if (dir > 0)
                {
                    // left -> right
                    if (split.right.value > end)
                        stack.Push().sector = split.left; // min/maxDist are reused
                    else if (split.left.value < start)
                        stack.Push().sector = split.right;
                    else
                    {
                        var rightMinDist = minDist + invDir * MathF.Max(0f, split.right.value - start);
                        stack.Push((split.right, rightMinDist, maxDist));
                        var leftMaxDist = maxDist + invDir * MathF.Min(0f, split.left.value - end);
                        stack.Push((split.left, minDist, leftMaxDist));
                    }
                }
                else
                {
                    // right -> left
                    if (split.left.value < end)
                        stack.Push().sector = split.right;
                    else if (split.right.value > start)
                        stack.Push().sector = split.left;
                    else
                    {
                        var leftMinDist = minDist + invDir * MathF.Max(0f, split.left.value - start);
                        stack.Push((split.left, leftMinDist, maxDist));
                        var rightMaxDist = maxDist + invDir * MathF.Min(0f, split.right.value - end);
                        stack.Push((split.right, minDist, rightMaxDist));
                    }
                }
            }
            else
            {
                for (int i = 0; i < sector.count; i++)
                {
                    var triI = sector.index + i;
                    var (triangle, triangleId) = GetTriangle(Collision.map[triI]);
                    var newHit = ray.TryCastUnsafe(triangle);
                    if (newHit.Distance < bestHit.Distance) // for misses: NaN < bestDistance is always false
                        bestHit = newHit with { TriangleId = triangleId };
                }
            }
        }

        ArrayPool<(CollisionSector, float, float)>.Shared.Return(stackArray);
        return bestHit.Distance < maxDistTotal ? bestHit : null;
    }

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive)
    {
        var l = new List<Intersection>(32);
        IntersectionsList<T, TQueries>(primitive, l);
        return l;
    }

    public void IntersectionsList<T, TQueries>(in T primitive, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var stackArray = ArrayPool<CollisionSplit>.Shared.Rent(MaxTreeDepth);
        var stack = new StackOverSpan<CollisionSplit>(stackArray);
        stack.Push(Collision.splits[0]);
        while (stack.TryPop(out var curSplit))
        {
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    stack.Push(Collision.splits[curSplit.right.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.right, intersections);
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    stack.Push(Collision.splits[curSplit.left.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.left, intersections);
            }
        }
        ArrayPool<CollisionSplit>.Shared.Return(stackArray);
    }

    private void  IntersectionsListLeaf<T, TQueries>(in T primitive, CollisionSector sector, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        for (int i = 0; i < sector.count; i++)
        {
            var t = GetTriangle(Collision.map[i + sector.index]);
            var intersection = TQueries.Intersect(t.Triangle, primitive);
            if (intersection != null)
                intersections.Add(intersection.Value with { TriangleId = t.TriangleId });
        }
    }
}
