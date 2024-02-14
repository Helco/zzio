using NUnit.Framework;
using System.Numerics;

namespace zzre.core.tests;

[TestFixture]
public class TestLocation
{
    private const float EPS = 0.0001f;

    private static Location SomeLocation1()
    {
        var location = new Location
        {
            LocalPosition = new Vector3(1.0f, 2.0f, 3.0f),
            LocalRotation = Quaternion.CreateFromYawPitchRoll(30.0f, 90.0f, 170.0f)
        };
        return location;
    }
    private static Location SomeLocation2()
    {
        var location = new Location
        {
            LocalPosition = new Vector3(-234.0f, 0.0123f, 453.09f),
            LocalRotation = Quaternion.CreateFromYawPitchRoll(-56.0f, 24.0f, 35.0f)
        };
        return location;
    }
    private static Location SomeLocation3()
    {
        var location = new Location
        {
            LocalPosition = new Vector3(-0.01f, 23.0f, 5.0f),
            LocalRotation = Quaternion.CreateFromYawPitchRoll(12.0f, 270.0f, -500.0f)
        };
        return location;
    }
    private static Location SomeLocation()
    {
        var l1 = SomeLocation1();
        var l2 = SomeLocation2();
        var l3 = SomeLocation3();
        l3.Parent = l2;
        l2.Parent = l1;
        return l3;
    }

    [Test]
    public void ParentToLocal()
    {
        var location = SomeLocation();
        var origPoint = new Vector3(12.0f, -34.0f, 0.023f);

        var expPoint = Vector3.Transform(origPoint, location.LocalToWorld);
        location.ParentToLocal = location.ParentToLocal;
        var actPoint = Vector3.Transform(origPoint, location.LocalToWorld);

        Assert.That(actPoint.X, Is.EqualTo(expPoint.X).Within(EPS));
        Assert.That(actPoint.Y, Is.EqualTo(expPoint.Y).Within(EPS));
        Assert.That(actPoint.Z, Is.EqualTo(expPoint.Z).Within(EPS));
    }

    [Test]
    public void LocalToWorldToLocal()
    {
        var location = SomeLocation();
        var prevPoint = new Vector3(10.0f, -23.45f, 89.0f);

        var newPoint = Vector3.Transform(prevPoint, location.LocalToWorld);
        newPoint = Vector3.Transform(newPoint, location.WorldToLocal);

        Assert.That(newPoint.X, Is.EqualTo(prevPoint.X).Within(EPS));
        Assert.That(newPoint.Y, Is.EqualTo(prevPoint.Y).Within(EPS));
        Assert.That(newPoint.Z, Is.EqualTo(prevPoint.Z).Within(EPS));
    }

    [Test]
    public void RebuiltHierachy()
    {
        var final = SomeLocation();
        var part1 = SomeLocation1();
        var part2 = SomeLocation2();
        part2.Parent = part1;

        var part3 = new Location
        {
            Parent = part2,
            WorldToLocal = final.WorldToLocal
        };

        var origPoint = new Vector3(12.0f, -34.0f, 0.023f);
        var expPoint = Vector3.Transform(origPoint, final.LocalToWorld);
        var actPoint = Vector3.Transform(origPoint, part3.LocalToWorld);

        Assert.That(actPoint.X, Is.EqualTo(expPoint.X).Within(EPS * 10));
        Assert.That(actPoint.Y, Is.EqualTo(expPoint.Y).Within(EPS * 10));
        Assert.That(actPoint.Z, Is.EqualTo(expPoint.Z).Within(EPS * 10));
    }

    [Test]
    public void SettingWorldToLocal()
    {
        var location = SomeLocation();
        var origPoint = new Vector3(10.0f, -23.45f, 89.0f);

        var expPoint = Vector3.Transform(origPoint, location.LocalToWorld);
        location.LocalToWorld = location.LocalToWorld;
        var actPoint = Vector3.Transform(origPoint, location.LocalToWorld);

        Assert.That(actPoint.X, Is.EqualTo(expPoint.X).Within(EPS * 10));
        Assert.That(actPoint.Y, Is.EqualTo(expPoint.Y).Within(EPS * 10));
        Assert.That(actPoint.Z, Is.EqualTo(expPoint.Z).Within(EPS * 10));
    }

    [Test]
    public void SettingLocalToWorld()
    {
        var location = SomeLocation();
        var origPoint = new Vector3(10.0f, -23.45f, 89.0f);

        var expPoint = Vector3.Transform(origPoint, location.LocalToWorld);
        location.WorldToLocal = location.WorldToLocal;
        var actPoint = Vector3.Transform(origPoint, location.LocalToWorld);

        Assert.That(actPoint.X, Is.EqualTo(expPoint.X).Within(EPS * 10));
        Assert.That(actPoint.Y, Is.EqualTo(expPoint.Y).Within(EPS * 10));
        Assert.That(actPoint.Z, Is.EqualTo(expPoint.Z).Within(EPS * 10));
    }
}
