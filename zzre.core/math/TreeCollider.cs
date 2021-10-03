using System;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;

namespace zzre
{
    public abstract class TreeCollider : IRaycastable, IIntersectable
    {
        public RWCollision Collision { get; }
        public Box Box { get; }

        protected TreeCollider(Box box, RWCollision collision) => (Box, Collision) = (box, collision);

        public abstract Triangle GetTriangle(int i);

        public Raycast? Cast(Ray ray) => Cast(ray, float.PositiveInfinity);
        public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

        public bool Intersects(Box box)           => Intersects(box,      (p, b) => p.SideOf(b), (t, b) => t.Intersects(b));
        public bool Intersects(OrientedBox box)   => Intersects(box,      (p, b) => p.SideOf(b), (t, b) => t.Intersects(b));
        public bool Intersects(Sphere sphere)     => Intersects(sphere,   (p, s) => p.SideOf(s), (t, s) => t.Intersects(s));
        public bool Intersects(Triangle triangle) => Intersects(triangle, (p, t) => p.SideOf(t), (t, o) => t.Intersects(o));

        // only coarse query for planes
        public bool Intersects(Plane plane) => Box.Intersects(plane);

        public Raycast? Cast(Ray ray, float maxLength)
        {
            var coarse = ray.Cast(Box);
            return coarse == null || coarse.Value.Distance > maxLength
                ? null 
                : RaycastNode(splitI: 0, ray, minDist: 0f, maxLength);
        }

        private Raycast? RaycastNode(int splitI, Ray ray, float minDist, float maxDist)
        {
            ref readonly var split = ref Collision.splits[splitI];
            // the kd-optimization: no need for a full dot product
            var directionDot = ray.Direction.Component(split.right.type.ToIndex());
            var rightDist = ray.DistanceTo(split.right.type, split.right.value);
            var leftDist = ray.DistanceTo(split.left.type, split.left.value);

            Raycast? hit = null;
            if (directionDot < 0f)
            {
                hit = RaycastSector(split.right, ray, minDist, rightDist ?? maxDist, hit);
                if ((hit?.Distance ?? float.MaxValue) >= (leftDist ?? 0f))
                {
                    hit = RaycastSector(split.left, ray, leftDist ?? minDist, maxDist, hit);
                }
            }
            else
            {
                hit = RaycastSector(split.left, ray, minDist, leftDist ?? maxDist, hit);
                if ((hit?.Distance ?? float.MaxValue) >= (rightDist ?? 0f))
                {
                    hit = RaycastSector(split.right, ray, rightDist ?? minDist, maxDist, hit);
                }
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
                    var triangle = GetTriangle(Collision.map[sector.index + i]);
                    var newHit = ray.Cast(triangle);
                    if (newHit == null)
                        continue;
                    if (newHit.Value.Distance < (myHit?.Distance ?? float.MaxValue))
                    {
                        myHit = newHit;
                    }
                }
            }

            return prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance)
                ? myHit
                : prevHit;
        }

        private bool Intersects<T>(T query, Func<Plane, T, PlaneIntersections> sideOf, Func<Triangle, T, bool> intersects) where T : IIntersectable
        {
            if (!Box.Intersects(query))
                return false;

            var splitStack = new Stack<CollisionSplit>();
            splitStack.Push(Collision.splits[0]);
            while(splitStack.Any())
            {
                var curSplit = splitStack.Pop();
                if (sideOf(GetPlane(curSplit.right), query) != PlaneIntersections.Outside)
                {
                    if (curSplit.right.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.right.index]);
                    else if (IntersectsLeaf(query, intersects, curSplit.right))
                        return true;
                }

                if (sideOf(GetPlane(curSplit.left), query) != PlaneIntersections.Inside)
                {
                    if (curSplit.left.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.left.index]);
                    else if (IntersectsLeaf(query, intersects, curSplit.left))
                        return true;
                }
            }
            return false;
        }

        private bool IntersectsLeaf<T>(T query, Func<Triangle, T, bool> intersects, CollisionSector sector) =>
            Collision.map
                .Skip(sector.index)
                .Take(sector.count)
                .Select(GetTriangle)
                .Any(t => intersects(t, query));

        private Plane GetPlane(CollisionSector sector) => new Plane(sector.type.ToNormal(), sector.value);
    }
}
