using System;
using zzio.rwbs;

namespace zzre
{
    public sealed class AtomicCollider : TreeCollider
    {
        public RWAtomicSection Atomic { get; }
        public Box Box { get; }
        protected override IRaycastable CoarseCastable => Box;
        protected override IIntersectable CoarseIntersectable => Box;

        public AtomicCollider(RWAtomicSection atomic) : base(
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
}
