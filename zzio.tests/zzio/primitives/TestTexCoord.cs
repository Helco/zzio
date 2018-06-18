using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives {
    [TestFixture]
    public class TestTexCoord
    {
        private static readonly byte[] expected = new byte[] {
            0x00, 0x80, 0xac, 0xc3, // -345.0f
            0x00, 0x80, 0x29, 0x44, // +678.0f
        };

        [Test]
        public void ctor() {
            TexCoord tex = new TexCoord(0.1f, 0.2f);
            Assert.AreEqual(0.1f, tex.u);
            Assert.AreEqual(0.2f, tex.v);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            BinaryReader reader = new BinaryReader(stream);
            TexCoord tex = TexCoord.read(reader);
            Assert.AreEqual(-345.0f, tex.u);
            Assert.AreEqual(678.0f, tex.v);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            TexCoord tex = new TexCoord(-345.0f, 678.0f);
            tex.write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
