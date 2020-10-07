using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace zzre.core.tests.math
{
    [TestFixture]
    public class TestIntersectionsBox
    {
        const float EPS = 0.0001f;

        [Test]
        public void TestOBBvsPoint()
        {
            var box = new Box(Vector3.Zero, new Vector3(1.0f, 10.0f, 1.0f));
            var point = new Vector3(0.0f, 0.0f, 3.0f);
            var loc = new Location();
            loc.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90f * MathF.PI / 180f);

            Assert.IsFalse(box.Intersects(point));
            Assert.IsFalse(box.Intersects(new Location(), point));
            Assert.IsTrue(box.Intersects(loc, point));
        }

        [Test]
        public void TestAABBClosestPointvsPoint()
        {
            var box = new Box(Vector3.Zero, new Vector3(1.0f, 2.0f, 3.0f) * 2f);

            Assert.AreEqual(new Vector3(-1.0f, -2.0f, -3.0f), box.ClosestPoint(Vector3.One * -100));
            Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), box.ClosestPoint(Vector3.One * 100));
            Assert.AreEqual(new Vector3(1.0f, -2.0f, 3.0f), box.ClosestPoint(new Vector3(1, -1, 1) * 100));
        }

        [Test]
        public void TestOBBClosestPointvsPoint()
        {
            var box = new Box(Vector3.One * 10f, new Vector3(2.0f, 6.0f, 4.0f));
            var loc = new Location();
            loc.LocalPosition = Vector3.One * -10f;
            loc.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90f * MathF.PI / 180f);

            Assert.AreEqual(0.0f, Vector3.Distance(new Vector3(-1.0f, -22.0f, -3.0f), box.ClosestPoint(loc, Vector3.One * -100)), EPS);
            Assert.AreEqual(0.0f, Vector3.Distance(new Vector3(1.0f, -18.0f, 3.0f), box.ClosestPoint(loc, Vector3.One * 100)), EPS);
            Assert.AreEqual(0.0f, Vector3.Distance(new Vector3(1.0f, -22.0f, 0.0f), box.ClosestPoint(loc, new Vector3(1, -1, 0) * 100)), EPS);
            
        }
    }
}
