using System;
using System.Numerics;
using NUnit.Framework;

namespace zzre.core.tests.math;

[TestFixture]
public class TestIntersectionsBox
{
    private const float EPS = 0.0001f;

    [Test]
    public void TestOBBvsPoint()
    {
        var box = new Box(Vector3.Zero, new Vector3(1.0f, 10.0f, 1.0f));
        var point = new Vector3(0.0f, 0.0f, 3.0f);
        var loc = new Location
        {
            LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90f * MathF.PI / 180f)
        };

        Assert.That(box.Intersects(point), Is.False);
        Assert.That(box.Intersects(new Location(), point), Is.False);
        Assert.That(box.Intersects(loc, point));
    }

    [Test]
    public void TestAABBClosestPointvsPoint()
    {
        var box = new Box(Vector3.Zero, new Vector3(1.0f, 2.0f, 3.0f) * 2f);

        Assert.That(box.ClosestPoint(Vector3.One * -100), Is.EqualTo(new Vector3(-1.0f, -2.0f, -3.0f)));
        Assert.That(box.ClosestPoint(Vector3.One * 100), Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)));
        Assert.That(box.ClosestPoint(new Vector3(1, -1, 1) * 100), Is.EqualTo(new Vector3(1.0f, -2.0f, 3.0f)));
    }

    [Test]
    public void TestOBBClosestPointvsPoint()
    {
        var box = new Box(Vector3.One * 10f, new Vector3(2.0f, 6.0f, 4.0f));
        var loc = new Location
        {
            LocalPosition = Vector3.One * -10f,
            LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90f * MathF.PI / 180f)
        };

        Assert.That(Vector3.Distance(new Vector3(-1.0f, -22.0f, -3.0f), box.ClosestPoint(loc, Vector3.One * -100)), Is.EqualTo(0.0f).Within(EPS));
        Assert.That(Vector3.Distance(new Vector3(1.0f, -18.0f, 3.0f), box.ClosestPoint(loc, Vector3.One * 100)), Is.EqualTo(0.0f).Within(EPS));
        Assert.That(Vector3.Distance(new Vector3(1.0f, -22.0f, 0.0f), box.ClosestPoint(loc, new Vector3(1, -1, 0) * 100)), Is.EqualTo(0.0f).Within(EPS));

    }
}
