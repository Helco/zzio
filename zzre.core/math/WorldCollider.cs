using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio.rwbs;

namespace zzre
{
    public class WorldCollider : IRaycastable, IIntersectable
    {
        public RWWorld World { get; }
        public Box Box { get; }

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
        }

        public Raycast? Cast(Ray ray) => Cast(ray, float.PositiveInfinity);
        public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

        public Raycast? Cast(Ray ray, float maxLength)
        {
            return atomicColliders.Values
                .Select(c => c.Cast(ray, maxLength))
                .OrderBy(h => h?.Distance ?? float.MaxValue)
                .First();
            var first = ray.Cast(Box);
            if (!first.HasValue)
                return null;
            var second = ray.PointOfExit(Box, first.Value);

            var firstDist = first.Value.Distance;
            var minDist = second.HasValue ? firstDist : 0f;
            var maxDist = Math.Min(maxLength, second.HasValue ? second.Value.Distance : firstDist);

            var rootPlane = World.FindChildById(SectionId.PlaneSection, false) as RWPlaneSection;
            var rootAtomic = World.FindChildById(SectionId.AtomicSection, false) as RWAtomicSection;
            if (rootPlane != null && rootAtomic != null)
                throw new InvalidDataException("RWWorld has both a root plane and a root atomic");
            return RaycastSection(
                rootPlane as Section ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry"),
                ray, minDist, maxDist, prevHit: null);
        }

        private Raycast? RaycastSection(Section section, Ray ray, float minDist, float maxDist, Raycast? prevHit)
        {
            if (section is RWAtomicSection atomic)
            {
                if (!atomicColliders.TryGetValue(atomic, out var atomicCollider))
                    return prevHit;

                var myHit = atomicCollider.Cast(ray, maxDist);
                return myHit.HasValue && myHit.Value.Distance < Math.Min(maxDist, prevHit?.Distance ?? maxDist)
                ? myHit
                : prevHit;
            }
            else if (section is RWPlaneSection plane)
            {
                var planeNormal = plane.sectorType.AsNormal().ToNumerics();
                var leftDist = ray.Cast(new Plane(planeNormal, plane.leftValue))?.Distance ?? -1f;
                var rightDist = ray.Cast(new Plane(planeNormal, plane.rightValue))?.Distance ?? -1f;
                var leftSection = plane.children[0];
                var rightSection = plane.children[1];

                Raycast? hit = null;
                if (Vector3.Dot(ray.Direction, planeNormal) < 0f)
                {
                    if (rightDist >= minDist && rightDist <= maxDist)
                        hit = RaycastSection(rightSection, ray, minDist, rightDist, hit);
                    if (leftDist >= minDist && leftDist <= maxDist && (hit?.Distance ?? -1f) >= leftDist)
                        hit = RaycastSection(leftSection, ray, leftDist, maxDist, hit);
                }
                else
                {
                    if (leftDist >= minDist && leftDist <= maxDist)
                        hit = RaycastSection(leftSection, ray, minDist, leftDist, hit);
                    if (rightDist >= minDist && rightDist <= maxDist && (hit?.Distance ?? -1f) >= rightDist)
                        hit = RaycastSection(rightSection, ray, rightDist, maxDist, hit);
                }
                return hit;
            }
            else
                throw new InvalidDataException("Unexpected non-world section");
        }

        public bool Intersects(Box box)
        {
            throw new NotImplementedException();
        }

        public bool Intersects(OrientedBox box)
        {
            throw new NotImplementedException();
        }

        public bool Intersects(Sphere sphere)
        {
            throw new NotImplementedException();
        }

        public bool Intersects(Plane plane)
        {
            throw new NotImplementedException();
        }

        public bool Intersects(Triangle triangle)
        {
            throw new NotImplementedException();
        }
    }
}
