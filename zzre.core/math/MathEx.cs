using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

public static class MathEx
{
    internal const MethodImplOptions MIOptions = MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    public const float DegToRad = MathF.PI / 180f;
    public const float RadToDeg = 180f / MathF.PI;

    [MethodImpl(MIOptions)]
    public static float Lerp(float from, float to, float t) =>
        Math.Clamp(from + (to - from) * t, Math.Min(from, to), Math.Max(from, to));

    [MethodImpl(MIOptions)]
    public static float Lerp(float from, float to, float t, float startT, float duration) =>
        Lerp(from, to, (t - startT) / duration);

    [MethodImpl(MIOptions)]
    public static bool Cmp(float a, float b) =>
        Math.Abs(a - b) <= float.Epsilon * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));

    [MethodImpl(MIOptions)]
    public static bool CmpZero(float a) => Math.Abs(a) < 0.1E-10;

    [MethodImpl(MIOptions)]
    public static Vector2 Floor(Vector2 v) =>
        new(MathF.Floor(v.X), MathF.Floor(v.Y));

    [MethodImpl(MIOptions)]
    public static Vector3 Floor(Vector3 v) =>
        new(MathF.Floor(v.X), MathF.Floor(v.Y), MathF.Floor(v.Z));

    [MethodImpl(MIOptions)]
    public static Vector4 Floor(Vector4 v) =>
        new(MathF.Floor(v.X), MathF.Floor(v.Y), MathF.Floor(v.Z), MathF.Floor(v.W));

    [MethodImpl(MIOptions)]
    public static Vector3 Project(Vector3 length, Vector3 dir)
    {
        var dot = Vector3.Dot(length, dir);
        return dir * (dot / dir.Length());
    }

    [MethodImpl(MIOptions)]
    public static Vector3 SafeNormalize(Vector3 v)
    {
        var lengthSqr = v.LengthSquared();
        return CmpZero(lengthSqr)
            ? Vector3.Zero
            : v * (1f / MathF.Sqrt(lengthSqr));
    }

    [MethodImpl(MIOptions)]
    public static Vector3 SafeNormalize(Vector3 v, Vector3 alternative)
    {
        var lengthSqr = v.LengthSquared();
        return CmpZero(lengthSqr)
            ? alternative
            : v * (1f / MathF.Sqrt(lengthSqr));
    }

    [MethodImpl(MIOptions)]
    public static Vector3 Perpendicular(Vector3 length, Vector3 dir) =>
        length - Project(length, dir);

    // TODO: Remove interface usage in SATIntersects
    [MethodImpl(MIOptions)]
    public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axesA, IEnumerable<Vector3> axesB) =>
        SATIntersects(pointsA, pointsB,
            axesA.Concat(axesB).Concat(axesA.SelectMany(a => axesB.Select(b => Vector3.Cross(a, b)))));

    [MethodImpl(MIOptions)]
    public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axes)
    {
        foreach (var axis in axes)
        {
            var i1 = new Interval(pointsA.Select(p => Vector3.Dot(p, axis)));
            var i2 = new Interval(pointsB.Select(p => Vector3.Dot(p, axis)));
            if (!CmpZero(axis.LengthSquared()) && !i1.Intersects(i2))
                return false;
        }
        return true;
    }

    [MethodImpl(MIOptions)]
    public static Vector3 SATCrossEdge(Line e1, Line e2)
    {
        var cross = Vector3.Cross(e1.Vector, e2.Vector);
        return CmpZero(cross.LengthSquared())
            ? Vector3.Cross(e1.Vector, Vector3.Cross(e1.Vector, e2.Start - e1.Start))
            : cross;
    }

    [MethodImpl(MIOptions)]
    public static Vector3 HorizontalSlerp(Vector3 from, Vector3 to, float curvature, float time)
    {
        var fromAngle = MathF.Atan2(from.X, from.Z);
        var angleDelta = MathF.Atan2(to.X, to.Z) - fromAngle;
        if (angleDelta < -MathF.PI)
            angleDelta += 2 * MathF.PI;
        if (angleDelta > MathF.PI)
            angleDelta -= 2 * MathF.PI;
        var newAngle = (1f - 1f / MathF.Pow(curvature, time)) * angleDelta + fromAngle;

        return new Vector3(
            MathF.Sin(newAngle),
            from.Y,
            MathF.Cos(newAngle));
    }
}
