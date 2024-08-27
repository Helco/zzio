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
        var closest = Plane.ClosestPoint(point);
        if (IntersectionQueries.Intersects(this, closest))
            return closest;

        var closestAB = AB.ClosestPoint(point);
        var closestBC = BC.ClosestPoint(point);
        var closestAC = AC.ClosestPoint(point);
        var distAB = (closestAB - point).LengthSquared();
        var distBC = (closestBC - point).LengthSquared();
        var distAC = (closestAC - point).LengthSquared();
        if (distAB < distBC && distAB < distAC)
            return closestAB;
        if (distBC < distAB && distBC < distAC)
            return closestBC;
        return closestAC;
    }

    [MethodImpl(MIOptions)]
    public Vector3 Barycentric(Vector3 point)
    {
        if (Sse41.IsSupported)
            return BarycentricSse41(point);
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

    [MethodImpl(MIOptions)]
    private Vector3 BarycentricSse41(Vector3 point)
    {
        var a = A.AsVector128();
        var b = B.AsVector128();
        var c = C.AsVector128();
        var p = point.AsVector128();
        var ab = Sse.Subtract(b, a);
        var ac = Sse.Subtract(c, a);
        var abxac = CrossProductSse41(ab, ac);
        var abxac_len = Sse41.DotProduct(abxac, abxac, 0b01110001).GetElement(0);
        if (CmpZero(abxac_len))
            return new Vector3(-1f);

        var ap = Sse.Subtract(p, a);
        float d00 = Sse41.DotProduct(ab, ab, 0b01110001).GetElement(0);
        float d01 = Sse41.DotProduct(ab, ac, 0b01110001).GetElement(0);
        float d11 = Sse41.DotProduct(ac, ac, 0b01110001).GetElement(0);
        float d20 = Sse41.DotProduct(ap, ab, 0b01110001).GetElement(0);
        float d21 = Sse41.DotProduct(ap, ac, 0b01110001).GetElement(0);
        float denom = d00 * d11 - d01 * d01;
        if (CmpZero(denom))
            return new Vector3(-1f);

        Vector3 result;
        result.Y = (d11 * d20 - d01 * d21) / denom;
        result.Z = (d00 * d21 - d01 * d20) / denom;
        result.X = 1f - result.Y - result.Z;
        return result;
    }

    [MethodImpl(MIOptions)]
    private static Vector128<float> CrossProductSse41(Vector128<float> a, Vector128<float> b)
    {
        // based on https://geometrian.com/programming/tutorials/cross-product/index.php method 5
        var tmp0 = Sse.Shuffle(a, a, 0b11001001);
        var tmp1 = Sse.Shuffle(b, b, 0b11010010);
        var tmp2 = Sse.Multiply(tmp0, b);
        var tmp3 = Sse.Multiply(tmp0, tmp1);
        var tmp4 = Sse.Shuffle(tmp2, tmp2, 0b11001001);
        return Sse.Subtract(tmp3, tmp4);
    }
}
