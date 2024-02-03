using System.Numerics;
using zzio;

namespace zzre;

public readonly record struct Raycast(
    float Distance,
    Vector3 Point,
    Vector3 Normal,
    VertexTriangle? VertexTriangle = null)
{
    public Intersection AsIntersection(Triangle triangle) =>
        new Intersection(Point, triangle, VertexTriangle);
}

public interface IRaycastable
{
    public Raycast? Cast(Ray ray);
    public Raycast? Cast(Line line);
}
