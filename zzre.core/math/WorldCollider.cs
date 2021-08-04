using System;
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
            if (!ray.Intersects(Box))
                return null;

            throw new NotImplementedException();
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
