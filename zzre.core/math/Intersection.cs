using System.Numerics;

namespace zzre;

public readonly record struct Intersection(
    Vector3 Point,
    Triangle Triangle,
    WorldTriangleId? TriangleId = null)
{
    public Vector3 Normal => Triangle.Normal;

    public Intersection TransformToWorld(Location location)
    {
        var localToWorld = location.LocalToWorld;
        return new(
            Vector3.Transform(Point, localToWorld),
            new(
                Vector3.Transform(Triangle.A, localToWorld),
                Vector3.Transform(Triangle.B, localToWorld),
                Vector3.Transform(Triangle.C, localToWorld)),
            TriangleId);
    }
}
