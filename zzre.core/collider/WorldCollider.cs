using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;
using zzio;
using System;
using System.Collections;

// I regret it already a bit
using BoxIntersectionEnumerator = zzre.WorldCollider.IntersectionsEnumerator<
    zzre.Box, zzre.IntersectionQueries, zzre.TreeCollider.IntersectionsEnumerator<
        zzre.Box, zzre.IntersectionQueries>>;
using OrientedBoxIntersectionEnumerator = zzre.WorldCollider.IntersectionsEnumerator<
    zzre.OrientedBox, zzre.IntersectionQueries, zzre.TreeCollider.IntersectionsEnumerator<
        zzre.OrientedBox, zzre.IntersectionQueries>>;
using SphereIntersectionEnumerator = zzre.WorldCollider.IntersectionsEnumerator<
    zzre.Sphere, zzre.IntersectionQueries, zzre.TreeCollider.IntersectionsEnumerator<
        zzre.Sphere, zzre.IntersectionQueries>>;
using TriangleIntersectionEnumerator = zzre.WorldCollider.IntersectionsEnumerator<
    zzre.Triangle, zzre.IntersectionQueries, zzre.TreeCollider.IntersectionsEnumerator<
        zzre.Triangle, zzre.IntersectionQueries>>;
using LineIntersectionEnumerator = zzre.WorldCollider.IntersectionsEnumerator<
    zzre.Line, zzre.IntersectionQueries, zzre.TreeCollider.IntersectionsEnumerator<
        zzre.Line, zzre.IntersectionQueries>>;

namespace zzre;

public readonly record struct WorldTriangleId(int AtomicIdx, int TriangleIdx);

public readonly record struct WorldTriangleInfo(RWAtomicSection Atomic, VertexTriangle VertexTriangle)
{
    public Triangle Triangle => new(
        Atomic.vertices[VertexTriangle.v1],
        Atomic.vertices[VertexTriangle.v2],
        Atomic.vertices[VertexTriangle.v3]);
}

public sealed class WorldCollider : BaseGeometryCollider
{
    public RWWorld World { get; }
    public Box Box { get; }

    private readonly RWAtomicSection[] atomicSections;
    private readonly Section rootSection;
    private readonly IReadOnlyDictionary<RWAtomicSection, AtomicTreeCollider> atomicColliders;
    protected override IRaycastable CoarseCastable => Box;
    protected override IIntersectable CoarseIntersectable => Box;

    public WorldCollider(RWWorld world)
    {
        World = world;

        atomicSections = World
            .FindAllChildrenById(SectionId.AtomicSection, recursive: true)
            .Cast<RWAtomicSection>()
            .ToArray();
        var colliders = atomicSections
            .Select((section, i) => new AtomicTreeCollider(section, i))
            .ToArray();

        atomicColliders = colliders.ToDictionary(c => c.Atomic, c => c);
        Box = colliders.Aggregate(colliders.First().Box, (box, atomic) => box.Union(atomic.Box));

        var rootPlane = World.FindChildById(SectionId.PlaneSection, false);
        var rootAtomic = World.FindChildById(SectionId.AtomicSection, false);
        rootSection = rootPlane ?? rootAtomic ?? throw new InvalidDataException("RWWorld has no geometry");
        if (rootPlane != null && rootAtomic != null)
            throw new InvalidDataException("RWWorld has both a root plane and a root atomic");
    }

    public WorldTriangleInfo GetTriangleInfo(WorldTriangleId id) =>
        new(atomicSections[id.AtomicIdx], atomicSections[id.AtomicIdx].triangles[id.TriangleIdx]);

    public override Raycast? Cast(Ray ray, float maxLength)
    {
        if (!Box.Intersects(ray.Start))
        {
            var coarse = ray.Cast(Box);
            if (coarse == null || coarse.Value.Distance > maxLength)
                return null;
        }

        var fine = RaycastSection(rootSection, ray, minDist: 0f, maxLength, prevHit: null);
        if (fine is not null && fine.Value.Distance > maxLength)
            return null;
        return fine;
    }

    private Raycast? RaycastSection(Section section, Ray ray, float minDist, float maxDist, Raycast? prevHit)
    {
        if (minDist > maxDist)
            return prevHit;

        switch (section)
        {
            case RWAtomicSection atomic:
                if (!atomicColliders.TryGetValue(atomic, out var atomicCollider))
                    return prevHit;

                var myHit = atomicCollider.Cast(ray, maxDist);
                var isBetterHit = prevHit == null || (myHit != null && myHit.Value.Distance < prevHit.Value.Distance);
                return isBetterHit
                    ? myHit
                    : prevHit;

            case RWPlaneSection plane:
                var compIndex = plane.sectorType.ToIndex();
                var startValue = ray.Start.Component(compIndex);
                var directionDot = ray.Direction.Component(compIndex);
                var rightDist = ray.DistanceTo(plane.sectorType, plane.rightValue);
                var leftDist = ray.DistanceTo(plane.sectorType, plane.leftValue);
                var leftSection = plane.children[0];
                var rightSection = plane.children[1];

                Raycast? hit = prevHit;
                if (directionDot < 0f)
                {
                    if (startValue >= plane.rightValue)
                    {
                        hit = RaycastSection(rightSection, ray, minDist, rightDist ?? maxDist, hit);
                        float hitValue = hit?.Point.Component(compIndex) ?? float.MinValue;
                        if (hitValue > plane.leftValue)
                            return hit;
                    }
                    hit = RaycastSection(leftSection, ray, leftDist ?? minDist, maxDist, hit);
                }
                else
                {
                    if (startValue <= plane.leftValue)
                    {
                        hit = RaycastSection(leftSection, ray, minDist, leftDist ?? maxDist, hit);
                        float hitValue = hit?.Point.Component(compIndex) ?? float.MaxValue;
                        if (hitValue < plane.rightValue)
                            return hit;
                    }
                    hit = RaycastSection(rightSection, ray, rightDist ?? minDist, maxDist, hit);
                }
                return hit;

            default:
                throw new InvalidDataException("Unexpected non-world section");
        }
    }

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive)
    {
        if (!CoarseIntersectable.Intersects(primitive))
            yield break;

        var splitStack = new Stack<Section>();
        splitStack.Push(rootSection);
        while (splitStack.Any())
        {
            switch (splitStack.Pop())
            {
                case RWAtomicSection atomic when atomicColliders.TryGetValue(atomic, out var collider):
                    foreach (var i in TQueries.Intersections(collider, primitive))
                        yield return i;
                    break;

                case RWPlaneSection plane:
                    var leftPlane = new Plane(plane.sectorType.AsNormal(), plane.leftValue);
                    var rightPlane = new Plane(plane.sectorType.AsNormal(), plane.rightValue);
                    var leftSection = plane.children[0];
                    var rightSection = plane.children[1];

                    if (TQueries.SideOf(rightPlane, primitive) != PlaneIntersections.Outside)
                        splitStack.Push(rightSection);
                    if (TQueries.SideOf(leftPlane, primitive) != PlaneIntersections.Inside)
                        splitStack.Push(leftSection);
                    break;
            }
        }
    }

    public void IntersectionsList<T, TQueries>(in T primitive, List<Intersection> intersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
    {
        if (!CoarseIntersectable.Intersects(primitive))
            return;

        var splitStack = new Stack<Section>();
        splitStack.Push(rootSection);
        while (splitStack.Any())
        {
            switch (splitStack.Pop())
            {
                case RWAtomicSection atomic when atomicColliders.TryGetValue(atomic, out var collider):
                    collider.IntersectionsList<T, TQueries>(primitive, intersections);
                    break;

                case RWPlaneSection plane:
                    var leftPlane = new Plane(plane.sectorType.AsNormal(), plane.leftValue);
                    var rightPlane = new Plane(plane.sectorType.AsNormal(), plane.rightValue);
                    var leftSection = plane.children[0];
                    var rightSection = plane.children[1];

                    if (TQueries.SideOf(rightPlane, primitive) != PlaneIntersections.Outside)
                        splitStack.Push(rightSection);
                    if (TQueries.SideOf(leftPlane, primitive) != PlaneIntersections.Inside)
                        splitStack.Push(leftSection);
                    break;
            }
        }
    }

    private delegate TIntersectionEnumerator AtomicIntersectionFn<T, TIntersectionEnumerator>(TreeCollider collider, in T primitive)
        where T : struct, IIntersectable
        where TIntersectionEnumerator : struct, IEnumerator<Intersection>;
    private IEnumerable<Intersection> Intersections<T, TQueries, TIntersectionEnumerator>(T primitive,
        AtomicIntersectionFn<T, TIntersectionEnumerator> atomicIntersections)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
        where TIntersectionEnumerator : struct, IEnumerator<Intersection>
    {
        if (!CoarseIntersectable.Intersects(primitive))
            yield break;

        var splitStack = new Stack<Section>();
        splitStack.Push(rootSection);
        while (splitStack.Any())
        {
            switch (splitStack.Pop())
            {
                case RWAtomicSection atomic when atomicColliders.TryGetValue(atomic, out var collider):
                    if (collider is TreeCollider treeCollider)
                    {
                        var enumerator = atomicIntersections(treeCollider, primitive);
                        while (enumerator.MoveNext())
                            yield return enumerator.Current;
                    }
                    else
                        foreach (var i in TQueries.Intersections(collider, primitive))
                            yield return i;
                    break;

                case RWPlaneSection plane:
                    var leftPlane = new Plane(plane.sectorType.AsNormal(), plane.leftValue);
                    var rightPlane = new Plane(plane.sectorType.AsNormal(), plane.rightValue);
                    var leftSection = plane.children[0];
                    var rightSection = plane.children[1];

                    if (TQueries.SideOf(rightPlane, primitive) != PlaneIntersections.Outside)
                        splitStack.Push(rightSection);
                    if (TQueries.SideOf(leftPlane, primitive) != PlaneIntersections.Inside)
                        splitStack.Push(leftSection);
                    break;
            }
        }
    }

    public unsafe struct IntersectionsEnumerator<T, TQueries, TIntersectionEnumerator> : IEnumerator<Intersection>
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>
        where TIntersectionEnumerator : struct, IEnumerator<Intersection>
    {
        private readonly T primitive;
        private readonly WorldCollider collider;
        private readonly Stack<Section> splitStack = new();
        private readonly delegate*<TreeCollider, in T, TIntersectionEnumerator> atomicIntersections;
        private TIntersectionEnumerator atomicEnumerator;

        public Intersection Current { get; private set; }
        object IEnumerator.Current => Current;

        public IntersectionsEnumerator(WorldCollider collider, in T primitive,
            delegate*<TreeCollider, in T, TIntersectionEnumerator> atomicIntersections)
        {
            this.primitive = primitive;
            this.collider = collider;
            this.atomicIntersections = atomicIntersections;
            Reset();
        }

        public void Reset()
        {
            atomicEnumerator = default;
            splitStack.Clear();
            splitStack.Push(collider.rootSection);
        }

        public bool MoveNext()
        {
            AtomicIntersection:
            if (atomicEnumerator.MoveNext())
            {
                Current = atomicEnumerator.Current;
                return true;
            }

            while(splitStack.TryPop(out var section))
            {
                switch(section)
                {
                    case RWAtomicSection atomic when collider.atomicColliders.TryGetValue(atomic, out var atomicCollider):
                        if (atomicCollider is not TreeCollider treeCollider)
                            throw new NotSupportedException("Allocation-less intersection enumerator does not support non-tree enumerators");
                        atomicEnumerator = atomicIntersections(treeCollider, primitive);
                        goto AtomicIntersection;

                    case RWPlaneSection plane:
                        var leftPlane = new Plane(plane.sectorType.AsNormal(), plane.leftValue);
                        var rightPlane = new Plane(plane.sectorType.AsNormal(), plane.rightValue);
                        var leftSection = plane.children[0];
                        var rightSection = plane.children[1];

                        if (TQueries.SideOf(rightPlane, primitive) != PlaneIntersections.Outside)
                            splitStack.Push(rightSection);
                        if (TQueries.SideOf(leftPlane, primitive) != PlaneIntersections.Inside)
                            splitStack.Push(leftSection);
                        continue;
                }
            }
            return false;
        }

        public void Dispose() { atomicEnumerator.Dispose(); }
    }

    public struct IntersectionsEnumeratorVirtCall : IEnumerator<Intersection>
    {
        private readonly WorldCollider collider;
        private readonly AnyIntersectionable primitive;
        private readonly Stack<Section> splitStack = new();
        private TreeCollider.IntersectionsEnumeratorVirtCall atomicEnumerator;

        public Intersection Current { get; private set; }
        object IEnumerator.Current => Current;

        internal IntersectionsEnumeratorVirtCall(WorldCollider collider, in AnyIntersectionable primitive)
        {
            this.collider = collider;
            this.primitive = primitive;
            Reset();
        }

        public void Reset()
        {
            splitStack.Clear();
            splitStack.Push(collider.rootSection);
            atomicEnumerator = default;
        }

        public bool MoveNext()
        {
            AtomicIntersection:
            if (atomicEnumerator.MoveNext())
            {
                Current = atomicEnumerator.Current;
                return true;
            }

            while(splitStack.TryPop(out var section))
            {
                switch(section)
                {
                    case RWAtomicSection atomic when collider.atomicColliders.TryGetValue(atomic, out var atomicCollider):
                        atomicEnumerator = new(atomicCollider, primitive);
                        goto AtomicIntersection;

                    case RWPlaneSection plane:
                        var leftPlane = new Plane(plane.sectorType.AsNormal(), plane.leftValue);
                        var rightPlane = new Plane(plane.sectorType.AsNormal(), plane.rightValue);
                        var leftSection = plane.children[0];
                        var rightSection = plane.children[1];

                        if (primitive.SideOf(rightPlane) != PlaneIntersections.Outside)
                            splitStack.Push(rightSection);
                        if (primitive.SideOf(leftPlane) != PlaneIntersections.Inside)
                            splitStack.Push(leftSection);
                        continue;
                }
            }
            return false;
        }

        public void Dispose() { }
    }
}
