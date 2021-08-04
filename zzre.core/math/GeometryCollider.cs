using System;
using System.Numerics;
using zzio.rwbs;

namespace zzre
{
    public sealed class GeometryCollider : TreeCollider
    {
        public RWGeometry Geometry { get; }
        public Location Location { get; }

        public GeometryCollider(RWGeometry geometry, Location? location) : base(
            GetBox(geometry),
            geometry.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
            throw new ArgumentException("Given geometry does not have a collision section"))
        {
            Geometry = geometry;
            Location = location ?? new Location();
        }

        protected override Triangle GetTriangle(int i)
        {
            // TODO: Benchmark whether to transform all vertices first

            var vertices = Geometry.morphTargets[0].vertices;
            var indices = Geometry.triangles[i];
            return new Triangle(
                Vector3.Transform(vertices[indices.v1].ToNumerics(), Location.LocalToWorld),
                Vector3.Transform(vertices[indices.v2].ToNumerics(), Location.LocalToWorld),
                Vector3.Transform(vertices[indices.v3].ToNumerics(), Location.LocalToWorld));
        }

        private static Box GetBox(RWGeometry geometry)
        {
            var morphTarget = geometry.morphTargets[0];
            return new Box(
                morphTarget.bsphereCenter.ToNumerics(),
                Vector3.One * morphTarget.bsphereRadius);
        }
    }
}
