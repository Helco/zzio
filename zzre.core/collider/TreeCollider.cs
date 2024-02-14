using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre;

public abstract partial class TreeCollider : TriangleCollider
{
    public RWCollision Collision { get; }

    protected TreeCollider(RWCollision collision) => Collision = collision;

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

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive)
    {
        if (!CoarseIntersectable.Intersects(primitive))
            yield break;

        var splitStack = new Stack<CollisionSplit>();
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

    private static Plane GetPlane(CollisionSector sector) => new(sector.type.ToNormal(), sector.value);
}
