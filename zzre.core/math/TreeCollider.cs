using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio.rwbs;

namespace zzre
{
    public abstract class TreeCollider : IRaycastable, IIntersectable
    {
        public RWCollision Collision { get; }
        public Box Box { get; }

        protected TreeCollider(Box box, RWCollision collision) => Collision = collision;

        protected abstract Triangle GetTriangle(int i);

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
            if (!ray.Intersects(Box))
                return null;

            Raycast? bestRaycast = null;
            var splitStack = new Stack<CollisionSplit>();
            splitStack.Push(Collision.splits[0]);
            while (splitStack.Any())
            {
                var curSplit = splitStack.Pop();

                if (ray.Cast(GetPlane(curSplit.right))?.Distance < maxLength)
                {
                    if (curSplit.right.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.right.index]);
                    else
                        bestRaycast = RaycastLeaf(curSplit.right, ray, maxLength, bestRaycast);
                }

                if (ray.Cast(GetPlane(curSplit.left))?.Distance < maxLength)
                {
                    if (curSplit.left.count == RWCollision.SplitCount)
                        splitStack.Push(Collision.splits[curSplit.left.index]);
                    else
                        bestRaycast = RaycastLeaf(curSplit.left, ray, maxLength, bestRaycast);
                }
            }
            return bestRaycast;
        }

        private Raycast? RaycastLeaf(CollisionSector sector, Ray ray, float maxLength, Raycast? prevRaycast)
        {
            var myRaycast = Collision.map
                .Skip(sector.index)
                .Take(sector.count)
                .Select(i => ray.Cast(GetTriangle(i)))
                .OrderBy(c => c?.Distance ?? float.MaxValue)
                .FirstOrDefault();

            return myRaycast.HasValue && myRaycast.Value.Distance < Math.Min(maxLength, prevRaycast?.Distance ?? maxLength)
                ? myRaycast
                : prevRaycast;
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

        private Plane GetPlane(CollisionSector sector) => new Plane(
            sector.type switch
            {
                CollisionSectorType.X => Vector3.UnitX,
                CollisionSectorType.Y => Vector3.UnitY,
                CollisionSectorType.Z => Vector3.UnitZ,
                _ => throw new ArgumentOutOfRangeException($"Unknown collision sector type {sector.type}")
            }, sector.value);
    }
}
