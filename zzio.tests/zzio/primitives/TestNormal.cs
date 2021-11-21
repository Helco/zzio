using System.IO;
using NUnit.Framework;

namespace zzio.tests.primitives
{
    [TestFixture]
    public class TestNormal
    {
        private static readonly byte M123 = unchecked((byte)-123);
        private static readonly byte[] expected = new byte[] {
            0x12, 0x34, 0x56, M123
        };

        [Test]
        public void ctor() {
            Normal norm = new Normal(1, 2, 3, 4);
            Assert.AreEqual(1, norm.x);
            Assert.AreEqual(2, norm.y);
            Assert.AreEqual(3, norm.z);
            Assert.AreEqual(4, norm.p);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            using BinaryReader reader = new BinaryReader(stream);
            Normal norm = Normal.ReadNew(reader);
            Assert.AreEqual(0x12, norm.x);
            Assert.AreEqual(0x34, norm.y);
            Assert.AreEqual(0x56, norm.z);
            Assert.AreEqual(-123, norm.p);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            Normal norm = new Normal(0x12, 0x34, 0x56, -123);
            norm.Write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
