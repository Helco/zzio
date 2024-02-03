using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre;

public static class GeometryCollider
{
    public static IIntersectionable CreateFor(RWGeometry geometry, Location? location) =>
        geometry.FindChildById(SectionId.CollisionPLG, recursive: true) == null
        ? new GeometrySlowCollider(geometry, location)
        : new GeometryTreeCollider(geometry, location);
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

public class GeometrySlowCollider : IRaycastable, IIntersectable, IIntersectionable
{
    public RWGeometry Geometry { get; }
    public Location Location { get; }
    public Sphere Sphere => new(
        Location.GlobalPosition + Geometry.morphTargets[0].bsphereCenter,
        Geometry.morphTargets[0].bsphereRadius);

    private int TriangleCount => Geometry.triangles.Length;
    private IEnumerable<(Triangle Triangle, VertexTriangle VertexTriangle)> Triangles => Enumerable
        .Range(0, TriangleCount)
        .Select(GetTriangle);

    public GeometrySlowCollider(RWGeometry geometry, Location? location)
    {
        Geometry = geometry;
        Location = location ?? new Location();
    }

    public Raycast? Cast(Ray ray) => Sphere.Cast(ray) == null ? null : Triangles
        .Select(t => ray.Cast(t.Triangle))
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public Raycast? Cast(Line line) => Sphere.Cast(line) == null ? null : Triangles
        .Select(t => line.Cast(t.Triangle))
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public IEnumerable<Intersection> Intersections(Box box) => Sphere.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(OrientedBox box) => Sphere.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Sphere sphere) => Sphere.Intersects(sphere) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, sphere))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Triangle triangle) => Sphere.Intersects(triangle) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, triangle))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Line line) => Sphere.Intersects(line) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, line))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public bool Intersects(Box box) => Intersections(box).Any();
    public bool Intersects(OrientedBox box) => Intersections(box).Any();
    public bool Intersects(Sphere sphere) => Intersections(sphere).Any();
    public bool Intersects(Plane plane) => Sphere.Intersects(plane);
    public bool Intersects(Triangle triangle) => Intersections(triangle).Any();
    public bool Intersects(Line line) => Cast(line).HasValue;

    private (Triangle, VertexTriangle) GetTriangle(int i)
    {
        var vertices = Geometry.morphTargets[0].vertices;
        var indices = Geometry.triangles[i];
        return (new Triangle(
            Vector3.Transform(vertices[indices.v1], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v2], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v3], Location.LocalToWorld)), indices);
    }
}
