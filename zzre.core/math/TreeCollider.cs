using System;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;

namespace zzre
{
    public abstract partial class TreeCollider : IRaycastable, IIntersectable
    {
        public RWCollision Collision { get; }
        public Box Box { get; }
        public Triangle LastTriangle { get; private set; }

        protected TreeCollider(Box box, RWCollision collision) => (Box, Collision) = (box, collision);

        public abstract Triangle GetTriangle(int i);

        public Raycast? Cast(Ray ray) => Cast(ray, float.PositiveInfinity);
        public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

        public bool Intersects(Box box)           => Intersections(box,      intersectionQueries).Any();
        public bool Intersects(OrientedBox box)   => Intersections(box,      intersectionQueries).Any();
        public bool Intersects(Sphere sphere)     => Intersections(sphere,   intersectionQueries).Any();
        public bool Intersects(Triangle triangle) => Intersections(triangle, intersectionQueries).Any();
        public IEnumerable<Intersection> Intersections(Box box)           => Intersections(box,      intersectionQueries);
        public IEnumerable<Intersection> Intersections(OrientedBox box)   => Intersections(box,      intersectionQueries);
        public IEnumerable<Intersection> Intersections(Sphere sphere)     => Intersections(sphere,   intersectionQueries);
        public IEnumerable<Intersection> Intersections(Triangle triangle) => Intersections(triangle, intersectionQueries);

        // only coarse query for planes
        public bool Intersects(Plane plane) => Box.Intersects(plane);

        public Raycast? Cast(Ray ray, float maxLength)
        {
            var coarse = ray.Cast(Box);
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
            Triangle newTriangle = default;
            if (sector.count == RWCollision.SplitCount)
                myHit = RaycastNode(sector.index, ray, minDist, maxDist);
            else
            {
                myHit = null;
                for (int i = 0; i < sector.count; i++)
                {
                    var triangle = GetTriangle(Collision.map[sector.index + i]);
                    var newHit = ray.Cast(triangle);
                    if (newHit == null)
                        continue;
                    if (newHit.Value.Distance < (myHit?.Distance ?? float.MaxValue))
                    {
                        myHit = newHit;
                        newTriangle = triangle;
                    }
                }
            }

            var isBetterHit = prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance);
            if (sector.count != RWCollision.SplitCount && isBetterHit && myHit != null)
                LastTriangle = newTriangle;
            return isBetterHit
                ? myHit
                : prevHit;
        }

        private IEnumerable<Intersection> Intersections<T>(T primitive, IIntersectionQueries<T> queries) where T : struct, IIntersectable
        {
            if (!Box.Intersects(primitive))
                yield break;

            var splitStack = new Stack<CollisionSplit>();
            splitStack.Push(Collision.splits[0]);
            while(splitStack.Any())
            {
                var curSplit = splitStack.Pop();
                if (queries.SideOf(GetPlane(curSplit.right), primitive) != PlaneIntersections.Outside)
                {
                    if (curSplit.right.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.right.index]);
                    else
                    {
                        foreach (var i in IntersectionsLeaf(primitive, queries, curSplit.right))
                            yield return i;
                    }
                }

                if (queries.SideOf(GetPlane(curSplit.left), primitive) != PlaneIntersections.Inside)
                {
                    if (curSplit.left.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.left.index]);
                    else
                    {
                        foreach (var i in IntersectionsLeaf(primitive, queries, curSplit.left))
                            yield return i;
                    }
                }
            }
        }

        private IEnumerable<Intersection> IntersectionsLeaf<T>(T primitive, IIntersectionQueries<T> queries, CollisionSector sector) where T : struct, IIntersectable
        {
            for (int i = 0; i < sector.count; i++)
            {
                var triangle = GetTriangle(Collision.map[i + sector.index]);
                var intersection = queries.Intersects(triangle, primitive);
                if (intersection != null)
                    yield return intersection.Value;
            }
        }

        private Plane GetPlane(CollisionSector sector) => new Plane(sector.type.ToNormal(), sector.value);

        private interface IIntersectionQueries<T> where T : struct, IIntersectable
        {
            PlaneIntersections SideOf(in Plane plane, in T primitive);
            Intersection? Intersects(in Triangle triangle, in T primitive);
        }

        private readonly struct IntersectionQueries :
            IIntersectionQueries<Box>,
            IIntersectionQueries<OrientedBox>,
            IIntersectionQueries<Sphere>,
            IIntersectionQueries<Triangle>
        {
            public PlaneIntersections SideOf(in Plane plane, in Box primitive) => plane.SideOf(primitive);
            public Intersection? Intersects(in Triangle triangle, in Box primitive) => triangle.Intersects(primitive)
                ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
                : null;

            public PlaneIntersections SideOf(in Plane plane, in Triangle primitive) => plane.SideOf(primitive);
            public Intersection? Intersects(in Triangle triangle, in Triangle primitive) => triangle.Intersects(primitive)
                ? new Intersection(triangle.ClosestPoint((primitive.A + primitive.B + primitive.C) / 3f), triangle)
                : null;

            public PlaneIntersections SideOf(in Plane plane, in OrientedBox primitive) => plane.SideOf(primitive);
            public Intersection? Intersects(in Triangle triangle, in OrientedBox primitive) => triangle.Intersects(primitive)
                ? new Intersection(triangle.ClosestPoint(primitive.Box.Center), triangle)
                : null;

            public PlaneIntersections SideOf(in Plane plane, in Sphere primitive) => plane.SideOf(primitive);
            public Intersection? Intersects(in Triangle triangle, in Sphere primitive) => triangle.Intersects(primitive)
                ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
                : null;
        }

        private static readonly IntersectionQueries intersectionQueries = default;
    }
}
