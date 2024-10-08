using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using zzio;
using zzio.rwbs;

namespace zzre;

public readonly record struct WorldTriangleId(int AtomicIdx, int TriangleIdx);

public readonly record struct WorldTriangleInfo(RWAtomicSection Atomic, VertexTriangle VertexTriangle)
{
    public Triangle Triangle => new(
        Atomic.vertices[VertexTriangle.v1],
        Atomic.vertices[VertexTriangle.v2],
        Atomic.vertices[VertexTriangle.v3]);
}

public partial class TreeCollider : BaseGeometryCollider
{
    private const int MaxTreeDepth = 64;
    private readonly bool hasSpans;
    private readonly ReadOnlyMemory<Triangle> triangles;
    private readonly ReadOnlyMemory<WorldTriangleId> triangleIds;
    public readonly Box Coarse;

    public RWCollision Collision { get; }
    public Location? Location { get; }
    protected sealed override IRaycastable CoarseCastable => Coarse;
    protected sealed override IIntersectable CoarseIntersectable => Coarse;

    protected virtual int TriangleCount { get => 0; }
    public virtual (Triangle Triangle, WorldTriangleId TriangleId) GetTriangle(int i) => throw new NotSupportedException();
    public virtual ReadOnlySpan<Triangle> Triangles =>
        triangles.IsEmpty ? throw new NotSupportedException() : triangles.Span;
    public virtual ReadOnlySpan<WorldTriangleId> TriangleIds =>
        triangleIds.IsEmpty ? throw new NotSupportedException() : triangleIds.Span;

    protected TreeCollider(Box coarse, RWCollision collision, Location? location = null)
    {
        Collision = collision;
        Coarse = coarse;
        Location = location;
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

    protected TreeCollider(Box coarse, RWCollision collision, Location? location,
        ReadOnlyMemory<Triangle> triangles,
        ReadOnlyMemory<WorldTriangleId> triangleIds)
    {
        Collision = collision;
        Coarse = coarse;
        Location = location;
        this.triangles = triangles;
        this.triangleIds = triangleIds;
        hasSpans = true;
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
        if (!ray.Cast(Coarse, out var minDistBox, out var maxDistBox))
            return null;
        maxDistTotal = Math.Min(maxDistTotal, maxDistBox);

        var splits = Collision.splits;
        var map = Collision.map;
        var triangles = Triangles;
        var triangleIds = TriangleIds;
        Raycast bestHit = new(maxDistTotal, Vector3.Zero, Vector3.One);

        var direction = ray.Direction;
        var invDirection = MathEx.Reciprocal(direction);
        static void FiniteOrZero(ref float v) => v = float.IsInfinity(v) ? 0 : v;
        FiniteOrZero(ref invDirection.X); // direction.* == 0 will turn into infinities here
        FiniteOrZero(ref invDirection.Y); // this can turn into NaNs down the line which
        FiniteOrZero(ref invDirection.Z); // will turn into missed hits for axis-aligned casts

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
        return bestHit.Distance >= maxDistTotal ? null
            : Location is not null ? bestHit.TransformToWorld(Location)
            : bestHit;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Raycast? CastLegacyInterface(Ray ray, float maxDistTotal = float.PositiveInfinity)
    {
        if (Location is not null)
            ray = ray.TransformToLocal(Location);
        if (!ray.Cast(Coarse, out var minDistBox, out var maxDistBox))
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
        return bestHit.Distance >= maxDistTotal ? null
            : Location is not null ? bestHit.TransformToWorld(Location)
            : bestHit;
    }

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive)
    {
        var l = new List<Intersection>(32);
        IntersectionsList<T, TQueries>(primitive, l);
        return l;
    }

    public void IntersectionsList<T, TQueries>(in T primitiveWorld, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var prevCount = intersections.Count;
        var primitive = Location is null ? primitiveWorld :
            TQueries.TransformToLocal(primitiveWorld, Location);

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

        if (Location is not null)
        {
            for (int i = prevCount; i < intersections.Count; i++)
                intersections[i] = intersections[i].TransformToWorld(Location);
        }
    }

    private void  IntersectionsListLeaf<T, TQueries>(in T primitive, CollisionSector sector, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (hasSpans)
        {
            var triangles = Triangles;
            var triangleIds = TriangleIds;
            for (int i = 0; i < sector.count; i++)
            {
                var t = triangles[i + sector.index];
                var intersection = TQueries.Intersect(t, primitive);
                if (intersection != null)
                    intersections.Add(intersection.Value with { TriangleId = triangleIds[i + sector.index] });
            }
        }
        else
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
}
