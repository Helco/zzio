using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using zzio;
using zzio.rwbs;

using BoxIntersections = zzre.Enumeratorable<zzre.Intersection, zzre.TreeCollider.IntersectionsEnumerator<zzre.Box, zzre.IntersectionQueries>>;
using OrientedBoxIntersections = zzre.Enumeratorable<zzre.Intersection, zzre.TreeCollider.IntersectionsEnumerator<zzre.OrientedBox, zzre.IntersectionQueries>>;
using SphereIntersections = zzre.Enumeratorable<zzre.Intersection, zzre.TreeCollider.IntersectionsEnumerator<zzre.Sphere, zzre.IntersectionQueries>>;
using TriangleIntersections = zzre.Enumeratorable<zzre.Intersection, zzre.TreeCollider.IntersectionsEnumerator<zzre.Triangle, zzre.IntersectionQueries>>;
using LineIntersections = zzre.Enumeratorable<zzre.Intersection, zzre.TreeCollider.IntersectionsEnumerator<zzre.Line, zzre.IntersectionQueries>>;

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

public abstract partial class TreeCollider : TriangleCollider
{
    public RWCollision Collision { get; }

    protected TreeCollider(RWCollision collision) => Collision = collision;

    protected static RWCollision CreateNaiveCollision(int triangleCount)
    {
        var split = new CollisionSplit()
        {
            left =
            {
                value = float.NegativeInfinity,
                type = CollisionSectorType.X,
                index = -1,
                count = RWCollision.SplitCount
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
        var coarse = CoarseCastable.Cast(ray);
        var result = coarse == null
            ? null
            : RaycastNode(splitI: 0, ray, minDist: 0f, maxLength);
        if (result != null && result.Value.Distance > maxLength)
            return null;
        return result;
    }

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

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive) =>
        //IntersectionsGenerator<T, TQueries>(primitive);
        //IntersectionsList<T, TQueries>(primitive);
        new IntersectionsEnumerable<T, TQueries>(this, primitive);

    public new BoxIntersections Intersections(in Box box) => new(new(this, box));
    public new OrientedBoxIntersections Intersections(in OrientedBox box) => new(new(this, box));
    public new SphereIntersections Intersections(in Sphere sphere) => new(new(this, sphere));
    public new TriangleIntersections Intersections(in Triangle triangle) => new(new(this, triangle));
    public new LineIntersections Intersections(in Line line) => new(new(this, line));

    public static Stack<CollisionSplit> splitStack = new();

    public IEnumerable<Intersection> IntersectionsGenerator<T, TQueries>(T primitive)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (!CoarseIntersectable.Intersects(primitive))
            yield break;

        //var splitStack = new Stack<CollisionSplit>();
        splitStack.Clear();
        splitStack.Push(Collision.splits[0]);
        while (splitStack.Any())
        {
            var curSplit = splitStack.Pop();
            if (TQueries.SideOf(GetPlane(curSplit.right), primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.right.index]);
                else
                {
                    foreach (var i in IntersectionsLeaf<T, TQueries>(primitive, curSplit.right))
                        yield return i;
                }
            }

            if (TQueries.SideOf(GetPlane(curSplit.left), primitive) != PlaneIntersections.Inside)
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

    public void IntersectionsList<T, TQueries>(T primitive, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (!CoarseIntersectable.Intersects(primitive))
            return;

        //var splitStack = new Stack<CollisionSplit>();
        splitStack.Clear();
        splitStack.Push(Collision.splits[0]);
        while (splitStack.Any())
        {
            var curSplit = splitStack.Pop();
            if (TQueries.SideOf(GetPlane(curSplit.right), primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.right.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.right, intersections);
            }

            if (TQueries.SideOf(GetPlane(curSplit.left), primitive) != PlaneIntersections.Inside)
            {
                if (curSplit.left.count == RWCollision.SplitCount)
                    splitStack.Push(Collision.splits[curSplit.left.index]);
                else
                    IntersectionsListLeaf<T, TQueries>(primitive, curSplit.left, intersections);
            }
        }
    }

    private void  IntersectionsListLeaf<T, TQueries>(T primitive, CollisionSector sector, List<Intersection> intersections)
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
        private readonly TreeCollider collider;
        private readonly T primitive;

        internal IntersectionsEnumerable(TreeCollider collider, in T primitive)
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

        private readonly TreeCollider collider;
        private readonly T primitive;
        //private readonly Stack<CollisionSplit> splitStack = new();
        private CollisionSector curSector;
        private SectorSelection sectorSelection;
        private int triangleI;
        private Intersection current;

        public Intersection Current => current;
        object IEnumerator.Current => Current;

        internal IntersectionsEnumerator(TreeCollider collider, in T primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
            Reset();
        }

        public void Reset()
        {
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
            if (TQueries.SideOf(GetPlane(curSplit.right), primitive) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.right.index]);
                else
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.right;
                }
            }

            if (TQueries.SideOf(GetPlane(curSplit.left), primitive) != PlaneIntersections.Inside)
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

        private readonly TreeCollider collider;
        private readonly AnyIntersectionable primitive;
        //private readonly Stack<CollisionSplit> splitStack = new();
        private CollisionSector curSector;
        private SectorSelection sectorSelection;
        private int triangleI;
        private Intersection current;

        public Intersection Current => current;
        object IEnumerator.Current => Current;

        internal IntersectionsEnumeratorVirtCall(TreeCollider collider, in AnyIntersectionable primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
            Reset();
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
            if (primitive.SideOf(GetPlane(curSplit.right)) != PlaneIntersections.Outside)
            {
                if (curSplit.right.count == RWCollision.SplitCount)
                    splitStack.Push(splits[curSplit.right.index]);
                else
                {
                    sectorSelection = SectorSelection.Last;
                    curSector = curSplit.right;
                }
            }

            if (primitive.SideOf(GetPlane(curSplit.left)) != PlaneIntersections.Inside)
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
