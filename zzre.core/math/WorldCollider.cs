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

        public Raycast? Cast(Ray ray) => Cast(ray, float.MaxValue);
        public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

        public Raycast? Cast(Ray ray, float maxLength)
        {
            var coarse = ray.Cast(Box);
            if (coarse == null || coarse.Value.Distance > maxLength)
                return null;

            var rootPlane = World.FindChildById(SectionId.PlaneSection, false);
            var rootAtomic = World.FindChildById(SectionId.AtomicSection, false);
            var rootSection = rootPlane ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry");
            if (rootPlane != null && rootAtomic != null)
                throw new InvalidDataException("RWWorld has both a root plane and a root atomic");

            return RaycastSection(rootSection, ray, maxLength, prevHit: null);
        }

        private Raycast? RaycastSection(Section section, Ray ray, float maxDist, Raycast? prevHit)
        {
            switch (section)
            {
                case RWAtomicSection atomic:
                {
                    if (!atomicColliders.TryGetValue(atomic, out var atomicCollider))
                        return prevHit;

                    var myHit = atomicCollider.Cast(ray, maxDist);
                    return prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance)
                        ? myHit
                        : prevHit;
                }

                case RWPlaneSection plane:
                {
                    var directionDot = ray.Direction.Component(plane.sectorType.ToIndex());
                    var rightDist = ray.DistanceTo(plane.sectorType, plane.rightValue);
                    var leftDist = ray.DistanceTo(plane.sectorType, plane.leftValue);
                    var leftSection = plane.children[0];
                    var rightSection = plane.children[1];

                    Raycast? hit = prevHit;
                    if (directionDot < 0f)
                    {
                        hit = RaycastSection(rightSection, ray, rightDist ?? maxDist, hit);
                        if ((hit?.Distance ?? float.MaxValue) >= (leftDist ?? 0f))
                        {
                            hit = RaycastSection(leftSection, ray, maxDist, hit);
                        }
                    }
                    else
                    {
                        hit = RaycastSection(leftSection, ray, leftDist ?? maxDist, hit);
                        if ((hit?.Distance ?? float.MaxValue) >= (rightDist ?? 0f))
                        {
                            hit = RaycastSection(rightSection, ray, maxDist, hit);
                        }
                    }
                    return hit;
                }

                default:
                    throw new InvalidDataException("Unexpected non-world section");
            }
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
