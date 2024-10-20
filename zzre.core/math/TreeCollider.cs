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

public partial class TreeCollider :
    IRaycastable,
    IIntersectable<Box>,
    IIntersectable<OrientedBox>,
    IIntersectable<Sphere>,
    IIntersectable<Triangle>,
    IIntersectable<Line>
{
    private const int MaxTreeDepth = 64;
    private readonly ReadOnlyMemory<CollisionSplit> splits;
    private readonly ReadOnlyMemory<Triangle> triangles;
    private readonly ReadOnlyMemory<WorldTriangleId> triangleIds;
    public readonly Box Coarse;

    public Location? Location { get; set; }
    public ReadOnlySpan<CollisionSplit> Splits => splits.Span;
    public ReadOnlySpan<Triangle> Triangles => triangles.Span;
    public ReadOnlySpan<WorldTriangleId> TriangleIds => triangleIds.Span;

    protected TreeCollider(Box coarse,
        ReadOnlyMemory<CollisionSplit> splits,
        ReadOnlyMemory<Triangle> triangles,
        ReadOnlyMemory<WorldTriangleId> triangleIds)
    {
        Coarse = coarse;
        this.splits = splits;
        this.triangles = triangles;
        this.triangleIds = triangleIds;
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

    public Raycast? Cast(Ray ray) => Cast(ray, float.PositiveInfinity);
    public Raycast? Cast(Line line) => Cast(new(line.Start, line.Direction), line.Length);


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Raycast? Cast(Ray ray, float maxDistTotal = float.PositiveInfinity)
    {
        if (!ray.Cast(Coarse, out var minDistBox, out var maxDistBox))
            return null;
        maxDistTotal = Math.Min(maxDistTotal, maxDistBox);

        var splits = Splits;
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
                    var triangle = triangles[triI];
                    if (float.IsNaN(triangle.A.X))
                        continue;
                    var newHit = ray.TryCastUnsafe(triangle);
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

    public bool Intersects<T, TQueries>(in T primitiveWorld)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        Intersection intersection = default;
        var list = new ListOverSpan<Intersection>(new(ref intersection));
        return Intersections<T, TQueries>(primitiveWorld, ref list) > 0;
    }

    public int Intersections<T, TQueries>(in T primitiveWorld, ref PooledList<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var listOverSpan = new ListOverSpan<Intersection>(intersections.FullSpan, intersections.Count);
        var result = Intersections<T, TQueries>(primitiveWorld, ref listOverSpan);
        intersections.Count = listOverSpan.Count;
        return result;
    }

    public int Intersections<T, TQueries>(in T primitiveWorld, ref ListOverSpan<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (intersections.IsFull)
            return 0;
        var prevCount = intersections.Count;
        var primitive = Location is null ? primitiveWorld :
            TQueries.TransformToLocal(primitiveWorld, Location);

        var splits = Splits;
        var stackArray = ArrayPool<CollisionSplit>.Shared.Rent(MaxTreeDepth);
        var stack = new StackOverSpan<CollisionSplit>(stackArray);
        stack.Push(splits[0]);
        while (stack.TryPop(out var curSplit))
        {
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    stack.Push(splits[curSplit.right.index]);
                else if (!IntersectionsLeaf<T, TQueries>(primitive, curSplit.right, ref intersections))
                    break;
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    stack.Push(splits[curSplit.left.index]);
                else if (!IntersectionsLeaf<T, TQueries>(primitive, curSplit.left, ref intersections))
                    break;
            }
        }
        ArrayPool<CollisionSplit>.Shared.Return(stackArray);

        if (Location is not null)
        {
            for (int i = prevCount; i < intersections.Count; i++)
                intersections[i] = intersections[i].TransformToWorld(Location);
        }
        return intersections.Count - prevCount;
    }

    private bool IntersectionsLeaf<T, TQueries>(in T primitive, CollisionSector sector, ref ListOverSpan<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var triangles = Triangles;
        var triangleIds = TriangleIds;
        for (int i = 0; i < sector.count; i++)
        {
            var triI = sector.index + i;
            var triangle = triangles[triI];
            if (float.IsNaN(triangle.A.X))
                continue;
            var intersection = TQueries.Intersect(triangle, primitive);
            if (intersection is null)
                continue;
            intersections.Add(intersection.Value with { TriangleId = triangleIds[triI] });
            if (intersections.IsFull)
                return false;
        }
        return true;
    }

    public int Intersections<T, TQueries>(in T primitiveWorld, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var prevCount = intersections.Count;
        var primitive = Location is null ? primitiveWorld :
            TQueries.TransformToLocal(primitiveWorld, Location);

        var splits = Splits;
        var stackArray = ArrayPool<CollisionSplit>.Shared.Rent(MaxTreeDepth);
        var stack = new StackOverSpan<CollisionSplit>(stackArray);
        stack.Push(splits[0]);
        while (stack.TryPop(out var curSplit))
        {
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    stack.Push(splits[curSplit.right.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.right, intersections);
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    stack.Push(splits[curSplit.left.index]);
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
        return intersections.Count - prevCount;
    }

    private void IntersectionsListLeaf<T, TQueries>(in T primitive, CollisionSector sector, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var triangles = Triangles;
        var triangleIds = TriangleIds;
        for (int i = 0; i < sector.count; i++)
        {
            var triI = sector.index + i;
            var triangle = triangles[triI];
            if (float.IsNaN(triangle.A.X))
                continue;
            var intersection = TQueries.Intersect(triangle, primitive);
            if (intersection != null)
                intersections.Add(intersection.Value with { TriangleId = triangleIds[triI] });
        }
    }

    public bool Intersects(in Box primitiveWorld) => Intersects<Box, IntersectionQueries>(primitiveWorld);
    public bool Intersects(in OrientedBox primitiveWorld) => Intersects<OrientedBox, IntersectionQueries>(primitiveWorld);
    public bool Intersects(in Sphere primitiveWorld) => Intersects<Sphere, IntersectionQueries>(primitiveWorld);
    public bool Intersects(in Triangle primitiveWorld) => Intersects<Triangle, IntersectionQueries>(primitiveWorld);
    public bool Intersects(in Line primitiveWorld) => Intersects<Line, IntersectionQueries>(primitiveWorld);

    public int Intersections(in Box primitiveWorld, ref ListOverSpan<Intersection> intersections) =>
        Intersections<Box, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in OrientedBox primitiveWorld, ref ListOverSpan<Intersection> intersections) =>
        Intersections<OrientedBox, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Sphere primitiveWorld, ref ListOverSpan<Intersection> intersections) =>
        Intersections<Sphere, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Triangle primitiveWorld, ref ListOverSpan<Intersection> intersections) =>
        Intersections<Triangle, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Line primitiveWorld, ref ListOverSpan<Intersection> intersections) =>
        Intersections<Line, IntersectionQueries>(primitiveWorld, ref intersections);

    public int Intersections(in Box primitiveWorld, ref PooledList<Intersection> intersections) =>
        Intersections<Box, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in OrientedBox primitiveWorld, ref PooledList<Intersection> intersections) =>
        Intersections<OrientedBox, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Sphere primitiveWorld, ref PooledList<Intersection> intersections) =>
        Intersections<Sphere, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Triangle primitiveWorld, ref PooledList<Intersection> intersections) =>
        Intersections<Triangle, IntersectionQueries>(primitiveWorld, ref intersections);
    public int Intersections(in Line primitiveWorld, ref PooledList<Intersection> intersections) =>
        Intersections<Line, IntersectionQueries>(primitiveWorld, ref intersections);

    public int Intersections(in Box primitiveWorld, List<Intersection> intersections) =>
        Intersections<Box, IntersectionQueries>(primitiveWorld, intersections);
    public int Intersections(in OrientedBox primitiveWorld, List<Intersection> intersections) =>
        Intersections<OrientedBox, IntersectionQueries>(primitiveWorld, intersections);
    public int Intersections(in Sphere primitiveWorld, List<Intersection> intersections) =>
        Intersections<Sphere, IntersectionQueries>(primitiveWorld, intersections);
    public int Intersections(in Triangle primitiveWorld, List<Intersection> intersections) =>
        Intersections<Triangle, IntersectionQueries>(primitiveWorld, intersections);
    public int Intersections(in Line primitiveWorld, List<Intersection> intersections) =>
        Intersections<Line, IntersectionQueries>(primitiveWorld, intersections);
}
