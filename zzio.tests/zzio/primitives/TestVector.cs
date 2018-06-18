using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives {
    [TestFixture]
    public class TestVector
    {
        private static readonly byte[] expected = new byte[] {
            0x00, 0x80, 0xac, 0xc3, // -345.0f
            0x00, 0x80, 0x29, 0x44, // +678.0f
            0x66, 0x66, 0xbe, 0x41, // +23.8f
        };

        [Test]
        public void ctor() {
            Vector vec = new Vector(0.1f, 0.2f, 0.3f);
            Assert.AreEqual(0.1f, vec.x);
            Assert.AreEqual(0.2f, vec.y);
            Assert.AreEqual(0.3f, vec.z);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            BinaryReader reader = new BinaryReader(stream);
            Vector vec = Vector.read(reader);
            Assert.AreEqual(-345.0f, vec.x);
            Assert.AreEqual(678.0f, vec.y);
            Assert.AreEqual(23.8f, vec.z);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            Vector vec = new Vector(-345.0f, 678.0f, 23.8f);
            vec.write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
