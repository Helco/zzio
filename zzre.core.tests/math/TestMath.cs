using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace zzre.tests;

[TestFixture]
public class TestMath
{
    private const float EPS = 0.00001f;
    private const float PINF = float.PositiveInfinity;
    private const float NAN = float.NaN;

    [Test]
    public void TestRoundtrip()
    {
        foreach (var dir in GenerateUniformPoints())
        {
            var dirQuat = dir.ToZZRotation();
            var roundtripDir = dirQuat.ToZZRotationVector();
            var dirLength = dir.Length();
            var roundtripDirLength = roundtripDir.Length();
            var acosAngle = Vector3.Dot(dir, roundtripDir) / (dirLength * roundtripDirLength);
            // We still occasionally get values slightly outside -1 to 1. Is this a problem in game too?
            acosAngle = Math.Clamp(acosAngle, -1, 1);
            var roundtripAngle = Math.Acos(acosAngle) * MathEx.RadToDeg;

            Assert.That(dir.Length(), Is.EqualTo(1.0f).Within(1).Percent, $"For {dir}");
            Assert.That(roundtripDir.Length(), Is.EqualTo(1.0f).Within(1).Percent, $"For {roundtripDir}");
            Assert.That(roundtripAngle, Is.EqualTo(0).Within(0.1), $"For {dir}");
        }
    }

    private const int PointCount = 10000;
    public static IEnumerable<Vector3> GenerateUniformPoints()
    {
        // TODO: Move uniform sphere point generator to NumericsExtensions
        // (and test that everything still works after that change)

        yield return Vector3.UnitX;
        yield return -Vector3.UnitX;
        yield return Vector3.UnitY;
        yield return -Vector3.UnitY;
        yield return Vector3.UnitZ;
        yield return -Vector3.UnitZ;
        yield return Vector3.Normalize(Vector3.One);

        var random = new Random(42);
        for (int i = 0; i < PointCount; i++)
        {
            // from https://corysimon.github.io/articles/uniformdistn-on-sphere/
            double theta = 2 * Math.PI * random.NextDouble();
            double phi = Math.Acos(1 - 2 * random.NextDouble());
            double sinTheta = Math.Sin(theta), cosTheta = Math.Cos(theta);
            double sinPhi = Math.Sin(phi), cosPhi = Math.Cos(phi);
            var vec = new Vector3(
                (float)(sinPhi * cosTheta),
                (float)(sinPhi * sinTheta),
                (float)(cosPhi));
            yield return vec;
        }
    }

    public readonly record struct AlmostANumber(int value)
        : IComparable<AlmostANumber>, IComparisonOperators<AlmostANumber, AlmostANumber, bool>
    {
        public int CompareTo(AlmostANumber other) => value - other.value;
        public static bool operator <(AlmostANumber left, AlmostANumber right) => left.CompareTo(right) < 0;
        public static bool operator >(AlmostANumber left, AlmostANumber right) => left.CompareTo(right) > 0;
        public static bool operator <=(AlmostANumber left, AlmostANumber right) => left.CompareTo(right) <= 0;
        public static bool operator >=(AlmostANumber left, AlmostANumber right) => left.CompareTo(right) >= 0;
    }
    public static readonly AlmostANumber A = new(1), B = new(2), C = new(3), D = new(4), E = new(5);

    [Test, Repeat(1000)]
    public void TestNextOf()
    {
        var value = Random.Shared.NextOf(new[] { A, B, C, D, E });
        Assert.That(value, Is.AnyOf(A, B, C, D, E));
    }

    [Test]
    public void TestNextOfEmpty()
    {
        Assert.That(() =>
        {
            Random.Shared.NextOf(new AlmostANumber[] { });
        }, Throws.ArgumentException);
    }

    [Test, Repeat(1000)]
    public void TestNextOfExceptEmpty()
    {
        var value = Random.Shared.NextOf<AlmostANumber>([A, B, C, D, E], []);
        Assert.That(value, Is.AnyOf(A, B, C, D, E));
    }

    [Test, Repeat(1000)]
    public void TestNextOfDisjunct()
    {
        var value = Random.Shared.NextOf<AlmostANumber>([A, B, C], [D, E]);
        Assert.That(value, Is.AnyOf(A, B, C));
    }

    [Test, Repeat(1000)]
    public void TestNextOfEmptyInput()
    {
        var value = Random.Shared.NextOf<AlmostANumber>([], [A, B, C, D, E]);
        Assert.That(value, Is.Null);
    }

    [Test, Repeat(1000)]
    public void TestNextOfSuper()
    {
        var value = Random.Shared.NextOf<AlmostANumber>([B, C, D], [A, B, C, D, E]);
        Assert.That(value, Is.Null);
    }

    [Test, Repeat(1000)]
    public void TestNextOfExcept()
    {
        var value = Random.Shared.NextOf<AlmostANumber>([A, B, C, D, E], [B, C, D]);
        Assert.That(value, Is.AnyOf(A, E));
    }

    [Test]
    public void TestNaNVectors()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MathEx.Vector2NaN.X, Is.NaN);
            Assert.That(MathEx.Vector2NaN.Y, Is.NaN);
            Assert.That(MathEx.Vector3NaN.X, Is.NaN);
            Assert.That(MathEx.Vector3NaN.Y, Is.NaN);
            Assert.That(MathEx.Vector3NaN.Z, Is.NaN);
            Assert.That(MathEx.Vector4NaN.X, Is.NaN);
            Assert.That(MathEx.Vector4NaN.Y, Is.NaN);
            Assert.That(MathEx.Vector4NaN.Z, Is.NaN);
            Assert.That(MathEx.Vector4NaN.W, Is.NaN);
        });
    }

    [Test]
    public void TestIsFinite()
    {
        Assert.That(MathEx.IsFinite(new Vector2(0, 0)), Is.True);
        Assert.That(MathEx.IsFinite(new Vector2(42, 1337)), Is.True);
        Assert.That(MathEx.IsFinite(new Vector2(PINF, PINF)), Is.False);
        Assert.That(MathEx.IsFinite(new Vector2(PINF, 42)), Is.False);
        Assert.That(MathEx.IsFinite(new Vector2(1337, PINF)), Is.False);
        Assert.That(MathEx.IsFinite(new Vector2(1337, NAN)), Is.False);
        Assert.That(MathEx.IsFinite(new Vector2(NAN, 1337)), Is.False);
        Assert.That(MathEx.IsFinite(new Vector2(NAN, NAN)), Is.False);

        Assert.That(MathEx.IsFinite(Vector3.Zero), Is.True);
        Assert.That(MathEx.IsFinite(Vector3.One), Is.True);
        Assert.That(MathEx.IsFinite(MathEx.Vector3NaN), Is.False);
        Assert.That(MathEx.IsFinite(new Vector3(1, NAN, 2)), Is.False);

        Assert.That(MathEx.IsFinite(Vector4.Zero), Is.True);
        Assert.That(MathEx.IsFinite(Vector4.One), Is.True);
        Assert.That(MathEx.IsFinite(MathEx.Vector4NaN), Is.False);
        Assert.That(MathEx.IsFinite(new Vector4(1, NAN, 2, 3)), Is.False);
    }
}
