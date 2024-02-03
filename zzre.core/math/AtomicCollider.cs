using System;
using System.Collections.Generic;
using System.Linq;
using Vortice.Mathematics;
using zzio;
using zzio.rwbs;

namespace zzre;

public interface IAtomicCollider : IIntersectable, IIntersectionable, IRaycastable
{
    RWAtomicSection Atomic { get; }
    Box Box { get; }

    Raycast? Cast(Ray ray, float maxDist);
}

public static class AtomicCollider
{
    public static IAtomicCollider CreateFor(RWAtomicSection section)
    {
        var collision = section.FindChildById(SectionId.CollisionPLG, recursive: true) as RWCollision;
        return (collision?.splits.Any() ?? false)
            ? new AtomicTreeCollider(section)
            : new AtomicSlowCollider(section);
    }
}

public sealed class AtomicTreeCollider : TreeCollider, IAtomicCollider
{
    public RWAtomicSection Atomic { get; }
    public Box Box { get; }
    protected override IRaycastable CoarseCastable => Box;
    protected override IIntersectable CoarseIntersectable => Box;

    public AtomicTreeCollider(RWAtomicSection atomic) : base(
        atomic.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        throw new ArgumentException("Given atomic section does not have a collision section"))
    {
        Atomic = atomic;
        Box = Box.FromMinMax(atomic.bbox1, atomic.bbox2);
    }

    public override (Triangle, VertexTriangle) GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return (new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]), t);
    }
}

public class AtomicSlowCollider : IAtomicCollider
{
    public RWAtomicSection Atomic { get; }
    public Box Box { get; }

    public AtomicSlowCollider(RWAtomicSection atomic)
    {
        Atomic = atomic;
        Box = Box.FromMinMax(atomic.bbox1, atomic.bbox2);
    }

    private int TriangleCount => Atomic.triangles.Length;
    private IEnumerable<(Triangle Triangle, VertexTriangle VertexTriangle)> Triangles => Enumerable
        .Range(0, TriangleCount)
        .Select(GetTriangle);

    public Raycast? Cast(Ray ray) => Box.Cast(ray) == null ? null : Triangles
        .Select(t => ray.Cast(t.Triangle))
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public Raycast? Cast(Ray ray, float maxDist) => Box.Cast(ray) == null ? null : Triangles
        .Select(t => ray.Cast(t.Triangle))
        .Where(c => c?.Distance <= maxDist)
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public Raycast? Cast(Line line) => Box.Cast(line) == null ? null : Triangles
        .Select(t => line.Cast(t.Triangle))
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public IEnumerable<Intersection> Intersections(Box box) => Box.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(OrientedBox box) => Box.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Sphere sphere) => Box.Intersects(sphere) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, sphere))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Triangle triangle) => Box.Intersects(triangle) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, triangle))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Line line) => Box.Intersects(line) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t.Triangle, line))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public bool Intersects(Box box) => Intersections(box).Any();
    public bool Intersects(OrientedBox box) => Intersections(box).Any();
    public bool Intersects(Sphere sphere) => Intersections(sphere).Any();
    public bool Intersects(Plane plane) => Box.Intersects(plane);
    public bool Intersects(Triangle triangle) => Intersections(triangle).Any();
    public bool Intersects(Line line) => Cast(line).HasValue;

    private (Triangle, VertexTriangle) GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return (new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]), t);
    }
}
