using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.rwbs;
using static System.MathF;
using static zzre.MathEx;

namespace zzre;

public static class ZZIOExtensions
{
    public static Vector4 ToNumerics(this FColor c) => new(c.r, c.g, c.b, c.a);
    public static void CopyFromNumerics(this ref FColor c, Vector4 v)
    {
        c.r = v.X;
        c.g = v.Y;
        c.b = v.Z;
        c.a = v.W;
    }

    public static Quaternion ToZZRotation(this Vector3 v)
    {
        v *= -1f;
        var up = Vector3.UnitY;
        if (Vector3.Cross(v, up).LengthSquared() < 0.001f)
            up = Vector3.UnitZ;
        return Quaternion.Conjugate(Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(Vector3.Zero, v, up)));
    }

    public static Vector3 ToZZRotationVector(this Quaternion rotation) =>
        Vector3.Transform(-Vector3.UnitZ, rotation) * -1f;

    public static RgbaByte ToVeldrid(this IColor c) => new(c.r, c.g, c.b, c.a);
    public static RgbaFloat ToVeldrid(this FColor c) => new(c.r, c.g, c.b, c.a);
    public static FColor ToFColor(this Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static RgbaFloat ToRgbaFloat(this Vector4 v) => new(v);

    public static Vector3 ToNormal(this CollisionSectorType sectorType) => sectorType switch
    {
        CollisionSectorType.X => Vector3.UnitX,
        CollisionSectorType.Y => Vector3.UnitY,
        CollisionSectorType.Z => Vector3.UnitZ,
        _ => throw new ArgumentOutOfRangeException($"Unknown collision sector type {sectorType}")
    };

    // adapted from https://gist.github.com/ciembor/1494530 - "It's the do what you want license :)"
    
    public static FColor RGBToHSL(this FColor c)
    {
        float max = Max(Max(c.r, c.g), c.b);
        float min = Min(Min(c.r, c.g), c.b);
        float mid = (max + min) / 2;

        FColor result = new(mid, mid, mid, c.a);
        if (Cmp(max, min))
            result.r = result.g = 0f; // achromatic
        else
        {
            float d = max - min;
            result.g = (result.b > 0.5) ? d / (2 - max - min) : d / (max + min);

            if (Cmp(max, c.r))
            {
                result.r = (c.g - c.b) / d + (c.g < c.b ? 6 : 0);
            }
            else if (Cmp(max, c.g))
            {
                result.r = (c.b - c.r) / d + 2;
            }
            else if (Cmp(max, c.b))
            {
                result.r = (c.r - c.g) / d + 4;
            }

            result.r /= 6;
        }
        return result;
    }

    private static float HueToRGB(float p, float q, float t)
    {
        if (t < 0)
            t += 1;
        if (t > 1)
            t -= 1;
        return t switch
        {
            < 1f / 6 => p + (q - p) * 6 * t,
            < 1f / 2 => q,
            < 2f / 3 => p + (q - p) * (2f / 3 - t) * 6,
            _ => p
        };
    }

    public static FColor HSLToRGB(this FColor c)
    {
        if (CmpZero(c.g))
            return new(c.b, c.b, c.b, c.a);

        float q = c.b < 0.5f
            ? c.b * (1 + c.g)
            : c.b + c.g - c.b * c.g;
        float p = 2 * c.b - q;
        return new(
            HueToRGB(p, q, c.r + 1f / 3),
            HueToRGB(p, q, c.r),
            HueToRGB(p, q, c.r - 1f / 3),
            c.a);
    }
}
