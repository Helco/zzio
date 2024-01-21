using System;
using System.Collections.Generic;
using System.Linq;
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

    public override Triangle GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]);
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
    private IEnumerable<Triangle> Triangles => Enumerable
        .Range(0, TriangleCount)
        .Select(GetTriangle);

    public Raycast? Cast(Ray ray) => Box.Cast(ray) == null ? null : Triangles
        .Select(ray.Cast)
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public Raycast? Cast(Ray ray, float maxDist) => Box.Cast(ray) == null ? null : Triangles
        .Select(ray.Cast)
        .Where(c => c?.Distance <= maxDist)
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public Raycast? Cast(Line line) => Box.Cast(line) == null ? null : Triangles
        .Select(line.Cast)
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    public IEnumerable<Intersection> Intersections(Box box) => Box.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(OrientedBox box) => Box.Intersects(box) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t, box))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Sphere sphere) => Box.Intersects(sphere) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t, sphere))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public IEnumerable<Intersection> Intersections(Triangle triangle) => Box.Intersects(triangle) ? Triangles
        .Select(t => IntersectionQueries.Default.Intersects(t, triangle))
        .NotNull()
        : Enumerable.Empty<Intersection>();

    public bool Intersects(Box box) => Intersections(box).Any();
    public bool Intersects(OrientedBox box) => Intersections(box).Any();
    public bool Intersects(Sphere sphere) => Intersections(sphere).Any();
    public bool Intersects(Plane plane) => Box.Intersects(plane);
    public bool Intersects(Triangle triangle) => Intersections(triangle).Any();

    private Triangle GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]);
    }
}
