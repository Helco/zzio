using System.Numerics;
using zzio.rwbs;

namespace zzre;

public sealed class GeometryTreeCollider : TreeCollider<Sphere>
{
    public RWGeometry Geometry { get; }
    public Location Location { get; }
    public Sphere Sphere => new(
        Location.GlobalPosition + Geometry.morphTargets[0].bsphereCenter,
        Geometry.morphTargets[0].bsphereRadius);
    protected override int TriangleCount => Geometry.triangles.Length;

    public GeometryTreeCollider(RWGeometry geometry, Location? location) : base(
        new(
            (location?.GlobalPosition ?? Vector3.Zero) + geometry.morphTargets[0].bsphereCenter,
            geometry.morphTargets[0].bsphereRadius), // TODO: this is not correct.
        geometry.FindChildById(SectionId.CollisionPLG, true) as RWCollision ??
        CreateNaiveCollision(geometry.triangles.Length))
    {
        Geometry = geometry;
        Location = location ?? new Location();
    }

    public override (Triangle Triangle, WorldTriangleId TriangleId) GetTriangle(int i)
    {
        // TODO: Benchmark whether to transform all vertices first

        var vertices = Geometry.morphTargets[0].vertices;
        var indices = Geometry.triangles[i];
        return (new Triangle(
            Vector3.Transform(vertices[indices.v1], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v2], Location.LocalToWorld),
            Vector3.Transform(vertices[indices.v3], Location.LocalToWorld)),
            new WorldTriangleId(0, i));
    }
}
