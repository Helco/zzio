using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using zzio;
using zzio.rwbs;

namespace zzre;

public readonly struct Enumeratorable<TElement, TEnumerator>(TEnumerator enumerator) : IEnumerable<TElement>
    where TEnumerator : struct, IEnumerator<TElement>
{
    public IEnumerator<TElement> GetEnumerator()
    {
        enumerator.Reset();
        return enumerator;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public abstract partial class TreeCollider<TCoarse> : TriangleCollider
    where TCoarse : struct, IIntersectable, IRaycastable
{
    public readonly TCoarse Coarse;

    public RWCollision Collision { get; }
    protected sealed override IRaycastable CoarseCastable => Coarse;
    protected sealed override IIntersectable CoarseIntersectable => Coarse;

    protected TreeCollider(TCoarse coarse, RWCollision collision)
    {
        Collision = collision;
        this.Coarse = coarse;
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

    public override Raycast? Cast(Ray ray, float maxLength)
    {
        var coarse = this.Coarse.Cast(ray);
        var result = coarse == null
            ? null
            : RaycastNode(splitI: 0, ray, minDist: 0f, maxLength);
        if (result != null && result.Value.Distance > maxLength)
            return null;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Raycast? RaycastNode(int splitI, Ray ray, float minDist, float maxDist)
    {
        ref readonly var split = ref Collision.splits[splitI];
        // the kd-optimization: no need for a full dot product
        var compIndex = split.right.type.ToIndex();
        var startValue = ray.Start.Component(compIndex);
        var directionDot = ray.Direction.Component(compIndex);
        var rightDist = ray.DistanceTo(split.right.type, split.right.value);
        var leftDist = ray.DistanceTo(split.left.type, split.left.value);

        Raycast? hit = null;
        if (directionDot < 0f)
        {
            if (startValue >= split.right.value)
            {
                hit = RaycastSector(split.right, ray, minDist, rightDist ?? maxDist, hit);
                float hitValue = hit?.Point.Component(compIndex) ?? float.MinValue;
                if (hitValue > split.left.value)
                    return hit;
            }
            hit = RaycastSector(split.left, ray, leftDist ?? minDist, maxDist, hit);
        }
        else
        {
            if (startValue <= split.left.value)
            {
                hit = RaycastSector(split.left, ray, minDist, leftDist ?? maxDist, hit);
                float hitValue = hit?.Point.Component(compIndex) ?? float.MaxValue;
                if (hitValue < split.right.value)
                    return hit;
            }
            hit = RaycastSector(split.right, ray, rightDist ?? minDist, maxDist, hit);
        }

        return hit;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Raycast? RaycastSector(CollisionSector sector, Ray ray, float minDist, float maxDist, Raycast? prevHit)
    {
        if (minDist > maxDist)
            return prevHit;

        Raycast? myHit;
        if (sector.count == RWCollision.SplitCount)
            myHit = RaycastNode(sector.index, ray, minDist, maxDist);
        else
        {
            myHit = null;
            for (int i = 0; i < sector.count; i++)
            {
                var t = GetTriangle(Collision.map[sector.index + i]);
                var newHit = ray.Cast(t.Triangle, t.TriangleId);
                if (newHit == null)
                    continue;
                if (newHit.Value.Distance < (myHit?.Distance ?? float.MaxValue))
                    myHit = newHit;
            }
        }

        var isBetterHit = prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance);
        return isBetterHit
            ? myHit
            : prevHit;
    }

    private static Stack<(CollisionSector, bool?)> sectorStack = new(128);
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Raycast? CastIterative(Ray ray, float maxDist)
    {
        //var coarseResult = Coarse.Cast(ray);
        //if (coarseResult is null || coarseResult.Value.Distance > maxDist)
          //  return null;

        var splits = Collision.splits;
        var map = Collision.map;
        Vector3 start = ray.Start;
        ReadOnlySpan<float> directionSign =
        [
            ray.Direction.X < 0 ? -1f : 1f,
            ray.Direction.Y < 0 ? -1f : 1f,
            ray.Direction.Z < 0 ? -1f : 1f
        ];
        var nonZeroCompIndex =
            MathEx.CmpZero(ray.Direction.X)
            ? MathEx.CmpZero(ray.Direction.Y)
            ? 2 : 1 : 0;
        Raycast bestHit = new(maxDist, Vector3.Zero, Vector3.One);

        sectorStack.Clear();
        sectorStack.Push((new()
        {
            count = RWCollision.SplitCount,
            index = 0,
            value = ray.Start.Component(nonZeroCompIndex),
            type = (CollisionSectorType)(nonZeroCompIndex * 4)
        }, true));
        while (sectorStack.TryPop(out var tt))
        {
            var (sector, f) = tt;
            if (bestHit.Distance < maxDist && f is bool ff)
            {
                var compValue = bestHit.Point.Component(sector.type.ToIndex());
                if (ff && compValue > sector.value ||
                    !ff && compValue < sector.value)
                    continue;
            }

            if (sector.count == RWCollision.SplitCount)
            {
                ref readonly var split = ref splits[sector.index];
                var compIndex = split.right.type.ToIndex();
                if (directionSign[compIndex] < 0)
                {
                    // ray goes right to left
                    sectorStack.Push((split.left, true));
                    if (start.Component(compIndex) >= split.right.value)
                        sectorStack.Push((split.right, null));
                }
                else
                {
                    // ray goes left to right
                    sectorStack.Push((split.right, false));
                    if (start.Component(compIndex) <= split.left.value)
                        sectorStack.Push((split.left, null));
                }
            }
            else
            {
                for (int i = 0; i < sector.count; i++)
                {
                    var t = GetTriangle(map[sector.index + i]);
                    var newHit = ray.Cast(t.Triangle, t.TriangleId);
                    if (newHit is not null && newHit.Value.Distance < bestHit.Distance)
                        bestHit = newHit.Value;
                }
            }
        }

        return bestHit.Distance >= maxDist ? null : bestHit;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Raycast? CastRWPrevious(Ray ray, float maxDistTotal = float.PositiveInfinity)
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

        var stackArray = ArrayPool<(CollisionSector, float, float)>.Shared.Rent(128);
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
                    var triI = map[sector.index + i];
                    var newHit = ray.TryCast(triangles[triI]);
                    if (newHit.Distance < bestHit.Distance) // for misses: NaN < bestDistance is always false
                        bestHit = newHit with { TriangleId = triangleIds[triI] };
                }
            }
        }

        ArrayPool<(CollisionSector, float, float)>.Shared.Return(stackArray);
        return bestHit.Distance < maxDistTotal ? bestHit : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Raycast? CastRWNext(Ray ray, float maxDistTotal = float.PositiveInfinity)
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

        var stackArray = ArrayPool<(CollisionSector, float, float)>.Shared.Rent(128);
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
                    var triI = map[sector.index + i];
                    var newHit = ray.TryCastMT(triangles[triI]);
                    if (newHit.Distance < bestHit.Distance) // for misses: NaN < bestDistance is always false
                        bestHit = newHit with { TriangleId = triangleIds[triI] };
                }
            }
        }

        ArrayPool<(CollisionSector, float, float)>.Shared.Return(stackArray);
        return bestHit.Distance < maxDistTotal ? bestHit : null;
    }

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive) =>
        IntersectionsGenerator<T, TQueries>(primitive);
        //IntersectionsList<T, TQueries>(primitive);
        //new IntersectionsEnumerable<T, TQueries>(this, primitive);


    public static Stack<CollisionSplit> splitStack = new();
    public static Stack<int> splitStackI = new();

    public IEnumerable<Intersection> IntersectionsGenerator<T, TQueries>(T primitive)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (!Coarse.Intersects(primitive) || Collision.splits.Length == 0)
            yield break;

        //var splitStack = new Stack<CollisionSplit>();
        splitStack.Clear();
        splitStack.Push(Collision.splits[0]);
        while (splitStack.Any())
        {
            var curSplit = splitStack.Pop();
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.right.index]);
                else
                {
                    foreach (var i in IntersectionsLeaf<T, TQueries>(primitive, curSplit.right))
                        yield return i;
                }
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.left.index]);
                else
                {
                    foreach (var i in IntersectionsLeaf<T, TQueries>(primitive, curSplit.left))
                        yield return i;
                }
            }
        }
    }

    private IEnumerable<Intersection> IntersectionsLeaf<T, TQueries>(T primitive, CollisionSector sector)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        for (int i = 0; i < sector.count; i++)
        {
            var t = GetTriangle(Collision.map[i + sector.index]);
            var intersection = TQueries.Intersect(t.Triangle, primitive);
            if (intersection != null)
                yield return intersection.Value with { TriangleId = t.TriangleId };
        }
    }

    public IEnumerable<Intersection> IntersectionsList<T, TQueries>(T primitive)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        var l = new List<Intersection>(32);
        IntersectionsList<T, TQueries>(primitive, l);
        return l;
    }

    public void IntersectionsList<T, TQueries>(in T primitive, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        //var splitStack = new Stack<CollisionSplit>();
        splitStack.Clear();
        splitStack.Push(Collision.splits[0]);
        while (splitStack.Count > 0)
        {
            var curSplit = splitStack.Pop();
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.right.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.right, intersections);
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.left.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.left, intersections);
            }
        }
    }

    public void IntersectionsListInty<T, TQueries>(in T primitive, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        //var splitStack = new Stack<CollisionSplit>();
        var splits = Collision.splits;
        splitStackI.Clear();
        splitStackI.Push(0);
        while (splitStackI.TryPop(out var splitI))
        {
            var curSplit = splits[splitI];
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStackI.Push(curSplit.right.index);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.right, intersections);
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStackI.Push(curSplit.left.index);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.left, intersections);
            }
        }
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

    public struct IntersectionsEnumerable<T, TQueries> : IEnumerable<Intersection>
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        private readonly TreeCollider<TCoarse> collider;
        private readonly T primitive;

        internal IntersectionsEnumerable(TreeCollider<TCoarse> collider, in T primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
        }

        public IEnumerator<Intersection> GetEnumerator() =>
            new IntersectionsEnumerator<T, TQueries>(collider, primitive);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct IntersectionsEnumerator<T, TQueries> : IEnumerator<Intersection>
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        private enum SectorSelection
        {
            None,
            Last,
            AlsoTakeLeft
        }

        private readonly TreeCollider<TCoarse> collider;
        private readonly T primitive;
        //private readonly Stack<CollisionSplit> splitStack = new();
        private CollisionSector curSector;
        private SectorSelection sectorSelection;
        private int triangleI;
        private Intersection current;

        public Intersection Current => current;
        object IEnumerator.Current => Current;

        public IntersectionsEnumerator(TreeCollider<TCoarse> collider, in T primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
            Reset();
        }

        public void Reset()
        {
            if (collider is null)
                return;
            splitStack.Clear();
            splitStack.Push(collider.Collision.splits[0]);
            sectorSelection = SectorSelection.None;
            triangleI = 0;
        }

        public bool MoveNext()
        {
            if (splitStack is null || collider is null)
                return false;
            while (splitStack.Count > 0)
            {
                if (sectorSelection is SectorSelection.None)
                    MoveNextInSplits();
                else if (MoveNextInSector())
                    return true;
            }
            return false;
        }

        private void MoveNextInSplits()
        {
            var splits = collider.Collision.splits;
            var curSplit = splitStack.Pop();
            if (TQueries.SideOf((int)curSplit.right.type / 4, curSplit.right.value, primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.right.index]);
                else
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.right;
                }
            }

            if (TQueries.SideOf((int)curSplit.left.type / 4, curSplit.left.value, primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.left.index]);
                else if (sectorSelection is SectorSelection.None)
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.left;
                }
                else
                    sectorSelection = SectorSelection.AlsoTakeLeft;
            }

            if (sectorSelection is not SectorSelection.None)
                splitStack.Push(curSplit);
        }

        private bool MoveNextInSector()
        {
            var map = collider.Collision.map;
            for (; triangleI < curSector.count;)
            {
                // increment triangleI already to be able to return
                var t = collider.GetTriangle(map[triangleI++ + curSector.index]);
                var intersection = TQueries.Intersect(t.Triangle, primitive);
                if (intersection != null)
                {
                    current = intersection.Value with { TriangleId = t.TriangleId };
                    return true;
                }
            }

            triangleI = 0;
            if (sectorSelection == SectorSelection.Last)
            {
                sectorSelection = SectorSelection.None;
                splitStack.Pop();
            }
            else
            {
                sectorSelection = SectorSelection.Last;
                curSector = splitStack.Peek().left;
            }
            return false;
        }

        readonly void IDisposable.Dispose() { }

    }

    public struct IntersectionsEnumeratorVirtCall : IEnumerator<Intersection>
    {
        private enum SectorSelection
        {
            None,
            Last,
            AlsoTakeLeft
        }

        private readonly TreeCollider<TCoarse> collider;
        private readonly AnyIntersectionable primitive;
        //private readonly Stack<CollisionSplit> splitStack = new();
        private CollisionSector curSector;
        private SectorSelection sectorSelection;
        private int triangleI;
        private Intersection current;

        public Intersection Current => current;
        object IEnumerator.Current => Current;

        public IntersectionsEnumeratorVirtCall(TreeCollider<TCoarse> collider, in AnyIntersectionable primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
            Reset();
            // TODO: This is not really correct yet, no coarse intersection is made... maybe not necessary?
        }

        public void Reset()
        {
            splitStack.Clear();
            splitStack.Push(collider.Collision.splits[0]);
            sectorSelection = SectorSelection.None;
            triangleI = 0;
        }

        public bool MoveNext()
        {
            if (splitStack is null || collider is null)
                return false;
            while (splitStack.Count > 0)
            {
                if (sectorSelection is SectorSelection.None)
                    MoveNextInSplits();
                else if (MoveNextInSector())
                    return true;
            }
            return false;
        }

        private void MoveNextInSplits()
        {
            var splits = collider.Collision.splits;
            var curSplit = splitStack.Pop();
            if (primitive.SideOf((int)curSplit.right.type / 4, curSplit.right.value) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.right.index]);
                else
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.right;
                }
            }

            if (primitive.SideOf((int)curSplit.left.type / 4, curSplit.left.value) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.left.index]);
                else if (sectorSelection is SectorSelection.None)
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.left;
                }
                else
                    sectorSelection = SectorSelection.AlsoTakeLeft;
            }

            if (sectorSelection is not SectorSelection.None)
                splitStack.Push(curSplit);
        }

        private bool MoveNextInSector()
        {
            var map = collider.Collision.map;
            for (; triangleI < curSector.count;)
            {
                // increment triangleI already to be able to return
                var t = collider.GetTriangle(map[triangleI++ + curSector.index]);
                var intersection = primitive.Intersect(t.Triangle);
                if (intersection != null)
                {
                    current = intersection.Value with { TriangleId = t.TriangleId };
                    return true;
                }
            }

            triangleI = 0;
            if (sectorSelection == SectorSelection.Last)
            {
                sectorSelection = SectorSelection.None;
                splitStack.Pop();
            }
            else
            {
                sectorSelection = SectorSelection.Last;
                curSector = splitStack.Peek().left;
            }
            return false;
        }

        readonly void IDisposable.Dispose() { }

    }


    private static Plane GetPlane(CollisionSector sector) => new(sector.type.ToNormal(), sector.value);
}
