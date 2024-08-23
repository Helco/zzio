﻿using zzio.rwbs;

namespace zzre;

public sealed class AtomicTreeCollider : TreeCollider
{
    public int AtomicId { get; }
    public RWAtomicSection Atomic { get; }
    public Box Box { get; }
    protected override IRaycastable CoarseCastable => Box;
    protected override IIntersectable CoarseIntersectable => Box;
    protected override int TriangleCount => Atomic.triangles.Length;

    public AtomicTreeCollider(RWAtomicSection atomic, int atomicId) : base(
        atomic.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        CreateNaiveCollision(atomic.triangles.Length))
    {
        Atomic = atomic;
        Box = Box.FromMinMax(atomic.bbox1, atomic.bbox2);
        AtomicId = atomicId;
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
