using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace zzio.tests.primitives
{
    [TestFixture]
    public class TestVector3
    {
        private static readonly byte[] expected = new byte[] {
            0x00, 0x80, 0xac, 0xc3, // -345.0f
            0x00, 0x80, 0x29, 0x44, // +678.0f
            0x66, 0x66, 0xbe, 0x41, // +23.8f
        };

        [Test]
        public void ctor() {
            Vector3 vec = new Vector3(0.1f, 0.2f, 0.3f);
            Assert.AreEqual(0.1f, vec.X);
            Assert.AreEqual(0.2f, vec.Y);
            Assert.AreEqual(0.3f, vec.Z);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            using BinaryReader reader = new BinaryReader(stream);
            Vector3 vec = reader.ReadVector3();
            Assert.AreEqual(-345.0f, vec.X);
            Assert.AreEqual(678.0f, vec.Y);
            Assert.AreEqual(23.8f, vec.Z);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            Vector3 vec = new Vector3(-345.0f, 678.0f, 23.8f);
            writer.Write(vec);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
