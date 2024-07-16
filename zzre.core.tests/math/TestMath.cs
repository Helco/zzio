using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace zzre.core.tests.math;

[TestFixture]
public class TestMath
{
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

            Assert.That(dir.Length(), Is.EqualTo(1.0f).Within(1).Percent);
            Assert.That(roundtripDir.Length(), Is.EqualTo(1.0f).Within(1).Percent);
            Assert.That(roundtripAngle, Is.EqualTo(0).Within(0.1), $"For {dir}");
        }
    }

    private const int PointCount = 10000;
    public static IEnumerable<Vector3> GenerateUniformPoints()
    {
        // TODO: Move uniform sphere point generator to NumericsExtensions
        // (and test that everything still works after that change)

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
}
