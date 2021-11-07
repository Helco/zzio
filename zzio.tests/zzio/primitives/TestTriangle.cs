using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives
{
    [TestFixture]
    public class TestTriangle
    {
        private static readonly byte[] expected = new byte[] {
            0x12, 0x34, // 13330
            0x56, 0x78, // 30806
            0x9a, 0xbc, // 48282
            0xde, 0xf0  // 61662
        };

        [Test]
        public void ctor() {
            Triangle tri = new Triangle(1, 2, 3, 4);
            Assert.AreEqual(1, tri.v1);
            Assert.AreEqual(2, tri.v2);
            Assert.AreEqual(3, tri.v3);
            Assert.AreEqual(4, tri.m);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            using BinaryReader reader = new BinaryReader(stream);
            Triangle tri = Triangle.ReadNew(reader);
            Assert.AreEqual(30806, tri.v1);
            Assert.AreEqual(48282, tri.v2);
            Assert.AreEqual(61662, tri.v3);
            Assert.AreEqual(13330, tri.m);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            Triangle tri = new Triangle(30806, 48282, 61662, 13330);
            tri.Write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
