using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzio;
using Quaternion = System.Numerics.Quaternion;

namespace zzre;

public static class NumericsExtensions
{
    private const float EPS = 0.001f;

    public static Vector3 SomeOrthogonal(this Vector3 v) => Math.Abs(v.Y) < EPS && Math.Abs(v.Z) < EPS
        ? Vector3.Cross(v, Vector3.UnitY)
        : Vector3.Cross(v, Vector3.UnitX);

    // from https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
    public static Vector3 ToEuler(this Quaternion q)
    {
        var euler = Vector3.Zero;
        double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        euler.Z = (float)Math.Atan2(sinr_cosp, cosr_cosp);

        double sinp = 2 * (q.W * q.Y - q.Z * q.X);
        euler.X = Math.Abs(sinp) >= 1
            ? (float)Math.CopySign(Math.PI / 2, sinp) // use 90 degrees if out of range
            : (float)Math.Asin(sinp);

        double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        euler.Y = (float)Math.Atan2(siny_cosp, cosy_cosp);

        return euler;
    }

    public static Quaternion LookAt(Vector3 source, Vector3 dest) => LookIn(Vector3.Normalize(dest - source), Vector3.UnitY);
    public static Quaternion LookAt(Vector3 source, Vector3 dest, Vector3 up) => LookIn(Vector3.Normalize(dest - source), up);
    public static Quaternion LookIn(Vector3 dir) => LookIn(dir, Vector3.UnitY);
    public static Quaternion LookIn(Vector3 dir, Vector3 up)
    {
        var matrix = Matrix4x4.CreateLookAt(dir, Vector3.Zero, up);
        return Quaternion.Inverse(Quaternion.CreateFromRotationMatrix(matrix));
    }

    public static (Vector3 right, Vector3 up, Vector3 forward) UnitVectors(this Quaternion q) => (
        Vector3.Transform(Vector3.UnitX, q),
        Vector3.Transform(Vector3.UnitY, q),
        Vector3.Transform(Vector3.UnitZ, q));

    public static float MaxComponent(this Vector2 v) => Math.Max(v.X, v.Y);
    public static float MinComponent(this Vector2 v) => Math.Min(v.X, v.Y);
    public static float MaxComponent(this Vector3 v) => Math.Max(Math.Max(v.X, v.Y), v.Z);
    public static float MinComponent(this Vector3 v) => Math.Min(Math.Min(v.X, v.Y), v.Z);
    public static float MaxComponent(this Vector4 v) => Math.Max(Math.Max(Math.Max(v.X, v.Y), v.Z), v.W);
    public static float MinComponent(this Vector4 v) => Math.Min(Math.Min(Math.Min(v.X, v.Y), v.Z), v.W);

    public static unsafe float Component(this Vector2 v, int i) => i >= 0 && i < 2
        ? ((float*)&v)[i]
        : throw new ArgumentOutOfRangeException(nameof(i));
    public static unsafe float Component(this Vector3 v, int i) => i >= 0 && i < 3
        ? ((float*)&v)[i]
        : throw new ArgumentOutOfRangeException(nameof(i));
    public static unsafe float Component(this Vector4 v, int i) => i >= 0 && i < 4
        ? ((float*)&v)[i]
        : throw new ArgumentOutOfRangeException(nameof(i));

    /// <summary>
    /// Generates a random number between 0 and 1 (exclusive)
    /// </summary>
    /// <returns>A random number between 0 and 1 (exclusive)</returns>
    public static float NextFloat(this Random random) => (float)random.NextDouble();

    public static Vector2 InPositiveSquare(this Random random) => new(random.NextFloat(), random.NextFloat());
    public static Vector3 InPositiveCube(this Random random) => new(random.NextFloat(), random.NextFloat(), random.NextFloat());

    public static float InLine(this Random random) => random.NextSign() * (float)random.NextDouble();
    public static Vector2 InSquare(this Random random) => new(random.InLine(), random.InLine());
    public static Vector3 InCube(this Random random) => new(random.InLine(), random.InLine(), random.InLine());
    public static Vector3 InSphere(this Random random) => random.OnSphere() * random.NextFloat();

    public static Vector3 OnSphere(this Random random)
    {
        // polar coordinates
        float
            hor = random.NextFloat() * 2 * MathF.PI,
            ver = random.NextFloat() * 2 * MathF.PI,
            horSin = MathF.Sin(hor), horCos = MathF.Cos(hor),
            verSin = MathF.Sin(ver), verCos = MathF.Cos(ver);
        return new Vector3(
            horCos * verSin,
            verCos,
            horSin * verSin);
    }

    public static int NextSign(this Random random) => random.Next(2) * 2 - 1;

    public static uint Next(this Random random, uint exclusiveMax) =>
        checked((uint)random.Next((int)exclusiveMax));

    public static float Next(this Random random, float min, float max) =>
        min + random.NextFloat() * (max - min);

    public static float In(this Random random, ValueRangeAnimation range) =>
        range.value + random.InLine() * range.width;

    public static T NextOf<T>(this Random random, IReadOnlyList<T> list) => !list.Any()
        ? throw new ArgumentException("List is empty, cannot get random element")
        : list[random.Next(list.Count)];

    public static T NextOf<T>(this Random random) where T : struct, Enum =>
        random.NextOf(Enum.GetValues<T>());

    public static bool IsFinite(this Vector2 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y);

    public static bool IsFinite(this Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    public static bool IsFinite(this Vector4 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
}
