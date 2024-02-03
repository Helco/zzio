using System;
using System.Linq;
using zzio;
using zzio.rwbs;

namespace zzre;

public static class AtomicCollider
{
    public static IAtomicCollider CreateFor(RWAtomicSection section)
    {
        var collision = section.FindChildById(SectionId.CollisionPLG, recursive: true) as RWCollision;
        return (collision?.splits.Any() ?? false)
            ? new AtomicTreeCollider(section)
            : new AtomicNaiveCollider(section);
    }
}

public interface IAtomicCollider
{
    public int AtomicId { get; set; }
    public Box Box { get; }
    public RWAtomicSection Atomic { get; }
}

public sealed class AtomicTreeCollider : TreeCollider, IAtomicCollider
{
    public int AtomicId { get; set; }
    public RWAtomicSection Atomic { get; }
    public Box Box { get; }
    protected override IRaycastable CoarseCastable => Box;
    protected override IIntersectable CoarseIntersectable => Box;
    protected override int TriangleCount => Atomic.triangles.Length;

    public AtomicTreeCollider(RWAtomicSection atomic) : base(
        atomic.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        throw new ArgumentException("Given atomic section does not have a collision section"))
    {
        Atomic = atomic;
        Box = Box.FromMinMax(atomic.bbox1, atomic.bbox2);
    }

    public override (Triangle, WorldTriangleId) GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return (new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]),
            new WorldTriangleId(AtomicId, i));
    }
}

public sealed class AtomicNaiveCollider : NaiveTriangleCollider, IAtomicCollider
{
    public int AtomicId { get; set; }
    public RWAtomicSection Atomic { get; }
    public Box Box { get; }
    protected override IRaycastable CoarseCastable => Box;
    protected override IIntersectable CoarseIntersectable => Box;
    protected override int TriangleCount => Atomic.triangles.Length;

    public AtomicNaiveCollider(RWAtomicSection atomic)
    {
        Atomic = atomic;
        Box = Box.FromMinMax(atomic.bbox1, atomic.bbox2);
    }

    public override (Triangle, WorldTriangleId) GetTriangle(int i)
    {
        var t = Atomic.triangles[i];
        return (new Triangle(
            Atomic.vertices[t.v1],
            Atomic.vertices[t.v2],
            Atomic.vertices[t.v3]),
            new WorldTriangleId(AtomicId, i));
    }
}
