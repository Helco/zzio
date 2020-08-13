using NUnit.Framework;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zzre.core.tests
{
    [TestFixture]
    public class TestLocation
    {
        private const float EPS = 0.0001f;

        private Location SomeLocation1()
        {
            var location = new Location();
            location.LocalPosition = new Vector3(1.0f, 2.0f, 3.0f);
            location.LocalScale = new Vector3(4.0f, 5.0f, 6.0f);
            location.LocalRotation = Quaternion.CreateFromYawPitchRoll(30.0f, 90.0f, 170.0f);
            return location;
        }
        private Location SomeLocation2()
        {
            var location = new Location();
            location.LocalPosition = new Vector3(-234.0f, 0.0123f, 453.09f);
            location.LocalScale = new Vector3(1.1145f, 0.898f, 2.341f);
            location.LocalRotation = Quaternion.CreateFromYawPitchRoll(-56.0f, 24.0f, 35.0f);
            return location;
        }
        private Location SomeLocation3()
        {
            var location = new Location();
            location.LocalPosition = new Vector3(-0.01f, 23.0f, 5.0f);
            location.LocalScale = new Vector3(-0.4f, 1.0f, 1.0f);
            location.LocalRotation = Quaternion.CreateFromYawPitchRoll(12.0f, 270.0f, -500.0f);
            return location;
        }
        private Location SomeLocation()
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

            var expPoint = Vector3.Transform(origPoint, location.WorldToLocal);
            location.ParentToLocal = location.ParentToLocal;
            var actPoint = Vector3.Transform(origPoint, location.WorldToLocal);

            Assert.AreEqual(expPoint.X, actPoint.X, EPS);
            Assert.AreEqual(expPoint.Y, actPoint.Y, EPS);
            Assert.AreEqual(expPoint.Z, actPoint.Z, EPS);
        }

        [Test]
        public void WorldToLocalToWorld()
        {
            var location = SomeLocation();
            var prevPoint = new Vector3(10.0f, -23.45f, 89.0f);

            var newPoint = Vector3.Transform(prevPoint, location.WorldToLocal);
            newPoint = Vector3.Transform(newPoint, location.LocalToWorld);

            Assert.AreEqual(prevPoint.X, newPoint.X, EPS);
            Assert.AreEqual(prevPoint.Y, newPoint.Y, EPS);
            Assert.AreEqual(prevPoint.Z, newPoint.Z, EPS);
        }

        [Test]
        public void RebuiltHierachy()
        {
            var final = SomeLocation();
            var part1 = SomeLocation1();
            var part2 = SomeLocation2();
            part2.Parent = part1;

            var part3 = new Location();
            part3.Parent = part2;
            part3.LocalToWorld = final.LocalToWorld;

            var origPoint = new Vector3(12.0f, -34.0f, 0.023f);
            var expPoint = Vector3.Transform(origPoint, final.WorldToLocal);
            var actPoint = Vector3.Transform(origPoint, part3.WorldToLocal);

            Assert.AreEqual(expPoint.X, actPoint.X, EPS * 10);
            Assert.AreEqual(expPoint.Y, actPoint.Y, EPS * 10);
            Assert.AreEqual(expPoint.Z, actPoint.Z, EPS * 10);
        }

        [Test]
        public void SettingWorldToLocal()
        {
            var location = SomeLocation();
            var origPoint = new Vector3(10.0f, -23.45f, 89.0f);

            var expPoint = Vector3.Transform(origPoint, location.WorldToLocal);
            location.WorldToLocal = location.WorldToLocal;
            var actPoint = Vector3.Transform(origPoint, location.WorldToLocal);

            Assert.AreEqual(expPoint.X, actPoint.X, EPS * 10);
            Assert.AreEqual(expPoint.Y, actPoint.Y, EPS * 10);
            Assert.AreEqual(expPoint.Z, actPoint.Z, EPS * 10);
        }

        [Test]
        public void SettingLocalToWorld()
        {
            var location = SomeLocation();
            var origPoint = new Vector3(10.0f, -23.45f, 89.0f);

            var expPoint = Vector3.Transform(origPoint, location.WorldToLocal);
            location.LocalToWorld = location.LocalToWorld;
            var actPoint = Vector3.Transform(origPoint, location.WorldToLocal);

            Assert.AreEqual(expPoint.X, actPoint.X, EPS * 10);
            Assert.AreEqual(expPoint.Y, actPoint.Y, EPS * 10);
            Assert.AreEqual(expPoint.Z, actPoint.Z, EPS * 10);
        }
    }
}
