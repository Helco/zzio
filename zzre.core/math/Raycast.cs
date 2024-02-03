using System.Numerics;
using zzio;
using zzio.rwbs;

namespace zzre;

public readonly record struct Raycast(
    float Distance,
    Vector3 Point,
    Vector3 Normal,
    WorldTriangleId? TriangleId = null)
{
    public Intersection AsIntersection(Triangle triangle) =>
        new Intersection(Point, triangle, TriangleId);
}

public interface IRaycastable
{
    public Raycast? Cast(Ray ray);
    public Raycast? Cast(Line line);
}
