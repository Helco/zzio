using System.Numerics;

namespace zzre;

public readonly record struct Raycast(
    float Distance,
    Vector3 Point,
    Vector3 Normal,
    WorldTriangleId? TriangleId = null)
{
    public Intersection AsIntersection(Triangle triangle) =>
        new Intersection(Point, triangle, TriangleId);

    public Raycast TransformToWorld(Location location)
    {
        var localToWorld = location.LocalToWorld;
        return this with
        {
            Point = Vector3.Transform(Point, localToWorld),
            Normal = Vector3.TransformNormal(Normal, localToWorld)
        };
    }
}

public interface IRaycastable
{
    public Raycast? Cast(Ray ray);
    public Raycast? Cast(Line line);
}
