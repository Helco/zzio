using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;
using zzio;

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
    private readonly IReadOnlyDictionary<RWAtomicSection, TriangleCollider> atomicColliders;
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
            .Select((section, i) =>
            {
                var collider = AtomicCollider.CreateFor(section);
                collider.AtomicId = i;
                return collider;
            })
            .ToArray();


        atomicColliders = colliders.ToDictionary(c => c.Atomic, c => (TriangleCollider)c);
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

        return RaycastSection(rootSection, ray, minDist: 0f, maxLength, prevHit: null);
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
}
