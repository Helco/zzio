using System.Numerics;

namespace zzre;

public readonly record struct Intersection(
    Vector3 Point,
    Triangle Triangle,
    WorldTriangleId? TriangleId = null)
{
    public Vector3 Normal => Triangle.Normal;
}
