using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio.rwbs;

namespace zzre
{
#if DEBUG_TREE_COLLIDER
    public enum TreeTraceFlags : byte
    {
        Hit = (1 << 0), // so default is invalid
        TookBothBranches = (1 << 1),
        TookLeftFirst = (1 << 2),
    }
#endif

    public abstract class TreeCollider : IRaycastable, IIntersectable
    {
#if DEBUG_TREE_COLLIDER
        private readonly List<(int split, TreeTraceFlags flags)> trace = new List<(int, TreeTraceFlags)>();
        public IReadOnlyList<(int split, TreeTraceFlags flags)> Trace => trace;
        public Triangle HitTriangle { get; private set; }
#endif

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
#if DEBUG_TREE_COLLIDER
            trace.Clear();
#endif

            var  first = ray.Cast(Box);
            if (!first.HasValue)
                return null;
            var second = ray.PointOfExit(Box, first.Value);

            var firstDist = first.Value.Distance;
            var minDist = second.HasValue ? firstDist : 0f;
            var maxDist = Math.Min(maxLength, second.HasValue ? second.Value.Distance : firstDist);
            return RaycastNode(splitI: 0, ray, minDist, maxDist);
        }

        private Raycast? RaycastNode(int splitI, Ray ray, float minDist, float maxDist)
        {
            var split = Collision.splits[splitI];
            var planeNormal = GetPlane(split.right).Normal;
            var rightDist = ray.Cast(GetPlane(split.right))?.Distance;
            var leftDist = ray.Cast(GetPlane(split.left))?.Distance;

            Raycast? hit = null;
#if DEBUG_TREE_COLLIDER
            TreeTraceFlags flags = TreeTraceFlags.Hit;
#endif
            if (Vector3.Dot(ray.Direction, planeNormal) < 0f)
            {
                leftDist ??= minDist;
                rightDist ??= maxDist;
                hit = RaycastSector(split.right, ray, minDist, rightDist.Value, hit);
                if ((hit?.Distance ?? float.MaxValue) >= leftDist)
                {
#if DEBUG_TREE_COLLIDER
                    flags |= TreeTraceFlags.TookBothBranches;
#endif
                    hit = RaycastSector(split.left, ray, leftDist.Value, maxDist, hit);
                }
            }
            else
            {
#if DEBUG_TREE_COLLIDER
                flags |= TreeTraceFlags.TookLeftFirst;
#endif
                leftDist ??= maxDist;
                rightDist ??= minDist;
                hit = RaycastSector(split.left, ray, minDist, leftDist.Value, hit);
                if ((hit?.Distance ?? float.MaxValue) >= rightDist)
                {
#if DEBUG_TREE_COLLIDER
                    flags |= TreeTraceFlags.TookBothBranches;
#endif
                    hit = RaycastSector(split.right, ray, rightDist.Value, maxDist, hit);
                }
            }

#if DEBUG_TREE_COLLIDER
            //trace.Add((splitI, flags));
#endif

            return hit;
        }

        private Raycast? RaycastSector(CollisionSector sector, Ray ray, float minDist, float maxDist, Raycast? prevHit)
        {
            Raycast? myHit;
            if (sector.count == RWCollision.SplitCount)
                myHit = RaycastNode(sector.index, ray, minDist, maxDist);
            else
            {
                /*myHit = Collision.map
                    .Skip(sector.index)
                    .Take(sector.count)
                    .Select(i => ray.Cast(GetTriangle(i)))
                    .OrderBy(c => c?.Distance ?? float.MaxValue)
                    .FirstOrDefault();*/
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
#if DEBUG_TREE_COLLIDER
                        HitTriangle = triangle;
#endif
                    }
                }
            }

            return myHit.HasValue && myHit.Value.Distance < Math.Min(maxDist, prevHit?.Distance ?? maxDist)
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
