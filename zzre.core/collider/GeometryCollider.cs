using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre;

public static class GeometryCollider
{
    public static TriangleCollider CreateFor(RWGeometry geometry, Location? location = null)
    {
        var collision = geometry.FindChildById(SectionId.CollisionPLG, recursive: true) as RWCollision;
        return (collision?.splits.Any() ?? false)
            ? new GeometryTreeCollider(geometry, location)
            : new GeometryNaiveCollider(geometry, location);
    }

    // TODO: Maybe add a oKD generator for complex RWGeometry
}

public sealed class GeometryTreeCollider : TreeCollider
{
    public RWGeometry Geometry { get; }
    public Location Location { get; }
    public Sphere Sphere => new(
        Location.GlobalPosition + Geometry.morphTargets[0].bsphereCenter,
        Geometry.morphTargets[0].bsphereRadius);
    protected override IRaycastable CoarseCastable => Sphere;
    protected override IIntersectable CoarseIntersectable => Sphere;
    protected override int TriangleCount => Geometry.triangles.Length;

    public GeometryTreeCollider(RWGeometry geometry, Location? location) : base(
        geometry.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        throw new ArgumentException("Given geometry does not have a collision section"))
    {
        Geometry = geometry;
        Location = location ?? new Location();
    }

    public override (Triangle, VertexTriangle) GetTriangle(int i)
    {
        // TODO: Benchmark whether to transform all vertices first

        var vertices = Geometry.morphTargets[0].vertices;
        var indices = Geometry.triangles[i];
        return (new Triangle(
            Vector3.Transform(vertices[indices.v1], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v2], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v3], Location.LocalToWorld)), indices);
    }
}

public sealed class GeometryNaiveCollider : NaiveTriangleCollider
{
    public RWGeometry Geometry { get; }
    public Location Location { get; }
    public Sphere Sphere => new(
        Location.GlobalPosition + Geometry.morphTargets[0].bsphereCenter,
        Geometry.morphTargets[0].bsphereRadius);
    protected override IRaycastable CoarseCastable => Sphere;
    protected override IIntersectable CoarseIntersectable => Sphere;
    protected override int TriangleCount => Geometry.triangles.Length;

    public GeometryNaiveCollider(RWGeometry geometry, Location? location)
    {
        Geometry = geometry;
        Location = location ?? new Location();
    }

    public override (Triangle, VertexTriangle) GetTriangle(int i)
    {
        var vertices = Geometry.morphTargets[0].vertices;
        var indices = Geometry.triangles[i];
        return (new Triangle(
            Vector3.Transform(vertices[indices.v1], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v2], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v3], Location.LocalToWorld)), indices);
    }
}
