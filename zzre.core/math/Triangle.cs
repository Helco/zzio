using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static zzre.MathEx;

namespace zzre;

public readonly partial struct Triangle : IRaycastable, IIntersectable
{
    public readonly Vector3 A, B, C;
    public Line AB { [MethodImpl(MIOptions)] get => new(A, B); }
    public Line AC { [MethodImpl(MIOptions)] get => new(A, C); }
    public Line BA { [MethodImpl(MIOptions)] get => new(B, A); }
    public Line BC { [MethodImpl(MIOptions)] get => new(B, C); }
    public Line CA { [MethodImpl(MIOptions)] get => new(C, A); }
    public Line CB { [MethodImpl(MIOptions)] get => new(C, B); }
    public Vector3 NormalUn { [MethodImpl(MIOptions)] get => Vector3.Cross(AB.Vector, AC.Vector); }
    public Vector3 Normal { [MethodImpl(MIOptions)] get => Vector3.Normalize(NormalUn); }
    public Plane Plane { [MethodImpl(MIOptions)] get => new(Normal, Vector3.Dot(A, Normal)); }
    public bool IsDegenerated { [MethodImpl(MIOptions)] get => NormalUn.LengthSquared() < 1e-10f; }

    [MethodImpl(MIOptions)]
    public Triangle(Vector3 a, Vector3 b, Vector3 c) => (A, B, C) = (a, b, c);

    // TODO: Remove allocations in Triangle

    public IEnumerable<Vector3> Corners()
    {
        yield return A;
        yield return B;
        yield return C;
    }

    public IEnumerable<Line> Edges()
    {
        yield return AB;
        yield return BC;
        yield return CA;
    }

    [MethodImpl(MIOptions)]
    public Vector3 ClosestPoint(Vector3 point)
    {
        var ab = B - A;
        var ac = C - A;
        var ap = point - A;
        var bp = point - B;
        var cp = point - C;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return A; //#1

        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return B; //#2

        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return C; //#3

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
            return A + d1 / (d1 - d3) * ab; //#4

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
            return A + d2 / (d2 - d6) * ac; //#5

        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            return B + (d4 - d3) / ((d4 - d3) + (d5 - d6)) * (C - B); //#6

        float denom = 1 / (va + vb + vc);
        float v = vb * denom;
        float w = vc * denom;
        return A + v * ab + w * ac; //#0
    }

    [MethodImpl(MIOptions)]
    public Vector3 Barycentric(Vector3 point)
    {
        if (CmpZero(Vector3.Cross(AB.Vector, AC.Vector).LengthSquared()))
            return new Vector3(-1f);
        var AP_Vector = point - A;
        float d00 = Vector3.Dot(AB.Vector, AB.Vector);
        float d01 = Vector3.Dot(AB.Vector, AC.Vector);
        float d11 = Vector3.Dot(AC.Vector, AC.Vector);
        float d20 = Vector3.Dot(AP_Vector, AB.Vector);
        float d21 = Vector3.Dot(AP_Vector, AC.Vector);
        float denom = d00 * d11 - d01 * d01;
        if (CmpZero(denom))
            return new Vector3(-1f); // more obvious than Zero

        Vector3 result;
        result.Y = (d11 * d20 - d01 * d21) / denom;
        result.Z = (d00 * d21 - d01 * d20) / denom;
        result.X = 1f - result.Y - result.Z;
        return result;
    }
}
