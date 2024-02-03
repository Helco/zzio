using System.Numerics;
using zzio;

namespace zzre;

public readonly record struct Intersection(
    Vector3 Point,
    Triangle Triangle,
    VertexTriangle? VertexTriangle = null)
{
    public Vector3 Normal => Triangle.Normal;
}
