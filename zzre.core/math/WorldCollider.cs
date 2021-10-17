using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;

namespace zzre
{
    public class WorldCollider : IRaycastable, IIntersectable
    {
        public RWWorld World { get; }
        public Box Box { get; }
        public Triangle LastTriangle { get; private set; }

        private readonly Section rootSection;
        private readonly IReadOnlyDictionary<RWAtomicSection, AtomicCollider> atomicColliders;

        public WorldCollider(RWWorld world)
        {
            World = world;

            atomicColliders = World
                .FindAllChildrenById(SectionId.AtomicSection, recursive: true)
                .Where(s => s.FindChildById(SectionId.CollisionPLG) != null)
                .Cast<RWAtomicSection>()
                .Select(atomic => new AtomicCollider(atomic))
                .ToDictionary(c => c.Atomic, c => c);

            Box = atomicColliders.Values.Aggregate(
                atomicColliders.Values.First().Box,
                (box, atomic) => box.Union(atomic.Box));

            var rootPlane = World.FindChildById(SectionId.PlaneSection, false);
            var rootAtomic = World.FindChildById(SectionId.AtomicSection, false);
            rootSection = rootPlane ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry");
            if (rootPlane != null && rootAtomic != null)
                throw new InvalidDataException("RWWorld has both a root plane and a root atomic");
        }

        public Raycast? Cast(Ray ray) => Cast(ray, float.MaxValue);
        public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

        public Raycast? Cast(Ray ray, float maxLength)
        {
            if (!Box.Intersects(ray.Start))
            {
                var coarse = ray.Cast(Box);
                if (coarse == null || coarse.Value.Distance > maxLength)
                    return null;
            }

            return RaycastSection(rootSection, ray, minDist: 0f, maxLength, prevHit: null);
        }

        private Raycast? RaycastSection(Section section, Ray ray, float minDist, float maxDist, Raycast? prevHit)
        {
            if (minDist > maxDist)
                return prevHit;

            switch (section)
            {
                case RWAtomicSection atomic:
                {
                    if (!atomicColliders.TryGetValue(atomic, out var atomicCollider))
                        return prevHit;

                    var myHit = atomicCollider.Cast(ray, maxDist);
                    var isBetterHit = prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance);
                    if (isBetterHit && myHit != null)
                        LastTriangle = atomicCollider.LastTriangle;
                    return isBetterHit
                        ? myHit
                        : prevHit;
                }

                case RWPlaneSection plane:
                {
                    var compIndex = plane.sectorType.ToIndex();
                    var startValue = ray.Start.Component(compIndex);
                    var directionDot = ray.Direction.Component(compIndex);
                    var rightDist = ray.DistanceTo(plane.sectorType, plane.rightValue);
                    var leftDist = ray.DistanceTo(plane.sectorType, plane.leftValue);
                    var leftSection = plane.children[0];
                    var rightSection = plane.children[1];

                    Raycast? hit = prevHit;
                    if (directionDot < 0f)
                    {
                        if (startValue >= plane.rightValue)
                        {
                            hit = RaycastSection(rightSection, ray, minDist, rightDist ?? maxDist, hit);
                            float hitValue = hit?.Point.Component(compIndex) ?? float.MinValue;
                            if (hitValue > plane.leftValue)
                                return hit;
                        }
                        hit = RaycastSection(leftSection, ray, leftDist ?? minDist, maxDist, hit);
                    }
                    else
                    {
                        if (startValue <= plane.leftValue)
                        {
                            hit = RaycastSection(leftSection, ray, minDist, leftDist ?? maxDist, hit);
                            float hitValue = hit?.Point.Component(compIndex) ?? float.MaxValue;
                            if (hitValue < plane.rightValue)
                                return hit;
                        }
                        hit = RaycastSection(rightSection, ray, rightDist ?? minDist, maxDist, hit);
                    }
                    return hit;
                }

                default:
                    throw new InvalidDataException("Unexpected non-world section");
            }
        }

        public bool Intersects(Box box) => Intersections(box, intersectionQueries).Any();
        public bool Intersects(OrientedBox box) => Intersections(box, intersectionQueries).Any();
        public bool Intersects(Sphere sphere) => Intersections(sphere, intersectionQueries).Any();
        public bool Intersects(Triangle triangle) => Intersections(triangle, intersectionQueries).Any();
        public IEnumerable<Intersection> Intersections(Box box) => Intersections(box, intersectionQueries);
        public IEnumerable<Intersection> Intersections(OrientedBox box) => Intersections(box, intersectionQueries);
        public IEnumerable<Intersection> Intersections(Sphere sphere) => Intersections(sphere, intersectionQueries);
        public IEnumerable<Intersection> Intersections(Triangle triangle) => Intersections(triangle, intersectionQueries);

        // only coarse query for planes
        public bool Intersects(Plane plane) => Box.Intersects(plane);

        private IEnumerable<Intersection> Intersections<T>(T primitive, IIntersectionQueries<T> queries) where T : struct, IIntersectable
        {
            if (!Box.Intersects(primitive))
                yield break;

            var splitStack = new Stack<Section>();
            splitStack.Push(rootSection);
            while (splitStack.Any())
            {
                switch(splitStack.Pop())
                {
                    case RWAtomicSection atomic when atomicColliders.TryGetValue(atomic, out var collider):
                        foreach (var i in queries.Intersections(collider, primitive))
                            yield return i;
                        break;

                    case RWPlaneSection plane:
                        var leftPlane = new Plane(plane.sectorType.AsNormal().ToNumerics(), plane.leftValue);
                        var rightPlane = new Plane(plane.sectorType.AsNormal().ToNumerics(), plane.rightValue);
                        var leftSection = plane.children[0];
                        var rightSection = plane.children[1];

                        if (queries.SideOf(rightPlane, primitive) != PlaneIntersections.Outside)
                            splitStack.Push(rightSection);
                        if (queries.SideOf(leftPlane, primitive) != PlaneIntersections.Inside)
                            splitStack.Push(leftSection);
                        break;
                }
            }
        }

        private interface IIntersectionQueries<T> where T : struct, IIntersectable
        {
            PlaneIntersections SideOf(in Plane plane, in T primitive);
            IEnumerable<Intersection> Intersections(AtomicCollider collider, in T primitive);
        }

        private readonly struct IntersectionQueries :
            IIntersectionQueries<Box>,
            IIntersectionQueries<OrientedBox>,
            IIntersectionQueries<Sphere>,
            IIntersectionQueries<Triangle>
        {
            public PlaneIntersections SideOf(in Plane plane, in Box primitive) => plane.SideOf(primitive);
            public PlaneIntersections SideOf(in Plane plane, in Triangle primitive) => plane.SideOf(primitive);
            public PlaneIntersections SideOf(in Plane plane, in OrientedBox primitive) => plane.SideOf(primitive);
            public PlaneIntersections SideOf(in Plane plane, in Sphere primitive) => plane.SideOf(primitive);
            public IEnumerable<Intersection> Intersections(AtomicCollider collider, in Box primitive) => collider.Intersections(primitive);
            public IEnumerable<Intersection> Intersections(AtomicCollider collider, in Triangle primitive) => collider.Intersections(primitive);
            public IEnumerable<Intersection> Intersections(AtomicCollider collider, in OrientedBox primitive) => collider.Intersections(primitive);
            public IEnumerable<Intersection> Intersections(AtomicCollider collider, in Sphere primitive) => collider.Intersections(primitive);
        }

        private static readonly IntersectionQueries intersectionQueries = default;
    }
}
