using System.Numerics;
using zzio.rwbs;

namespace zzre;

public sealed class GeometryTreeCollider : TreeCollider
{
    public RWGeometry Geometry { get; }
    public Sphere Sphere => new(
        Geometry.morphTargets[0].bsphereCenter,
        Geometry.morphTargets[0].bsphereRadius);
    protected override int TriangleCount => Geometry.triangles.Length;

    public GeometryTreeCollider(RWGeometry geometry, Location? location) : base(
        new(geometry.morphTargets[0].bsphereCenter,
            Vector3.One * 2 * geometry.morphTargets[0].bsphereRadius),
        geometry.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        CreateNaiveCollision(geometry.triangles.Length),
        location)
    {
        Geometry = geometry;
    }

    public override (Triangle Triangle, WorldTriangleId TriangleId) GetTriangle(int i)
    {
        // TODO: Benchmark whether to transform all vertices first

        var vertices = Geometry.morphTargets[0].vertices;
        var indices = Geometry.triangles[i];
        return (new Triangle(
            vertices[indices.v1],
            vertices[indices.v2],
            vertices[indices.v3]),
            new WorldTriangleId(0, i));
    }
}
