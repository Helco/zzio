using System;
using System.Numerics;
using zzio.rwbs;

namespace zzre;

public sealed class GeometryCollider : TreeCollider
{
    public RWGeometry Geometry { get; }

    private GeometryCollider(
        RWGeometry geometry,
        Box coarse,
        ReadOnlyMemory<CollisionSplit> splits,
        ReadOnlyMemory<Triangle> triangles,
        ReadOnlyMemory<WorldTriangleId> triangleIds)
        : base(coarse, splits, triangles, triangleIds)
    {
        Geometry = geometry;
    }

    public static GeometryCollider Create(RWGeometry geometry, Location? location)
    {
        // TODO: Do not instantiate collider for every geomtry instance
        if (geometry.morphTargets.Length != 1)
            throw new ArgumentException("GeometryTreeCollider does not support geometries with zero or more-than-one morph targets");
        var morphTarget = geometry.morphTargets[0];
        var coarse = new Box(morphTarget.bsphereCenter, Vector3.One * morphTarget.bsphereRadius);
        var collision = geometry.FindChildById(SectionId.CollisionPLG, true) as RWCollision
            ?? CreateNaiveCollision(geometry.triangles.Length);

        // unmap triangles and remove degenerated ones
        var triangles = new Triangle[collision.map.Length];
        var triangleIds = new WorldTriangleId[triangles.Length];
        var vertices = morphTarget.vertices;
        for (int triangleI = 0; triangleI < triangles.Length; triangleI++)
        {
            var t = geometry.triangles[collision.map[triangleI]];
            triangles[triangleI] = new(vertices[t.v1], vertices[t.v2], vertices[t.v3]);
            if (triangles[triangleI].IsDegenerated)
                triangles[triangleI] = new(MathEx.Vector3NaN, MathEx.Vector3NaN, MathEx.Vector3NaN);
            triangleIds[triangleI] = new(0, collision.map[triangleI]);
            triangleI++;
        }

        return new GeometryCollider(geometry, coarse, collision.splits, triangles, triangleIds)
        {
            Location = location
        };
    }

}
