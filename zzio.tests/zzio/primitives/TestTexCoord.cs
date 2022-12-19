using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace zzio.tests.primitives
{
    [TestFixture]
    public class TestVector2
    {
        private static readonly byte[] expected = new byte[] {
            0x00, 0x80, 0xac, 0xc3, // -345.0f
            0x00, 0x80, 0x29, 0x44, // +678.0f
        };

        [Test]
        public void ctor()
        {
            Vector2 tex = new(0.1f, 0.2f);
            Assert.AreEqual(0.1f, tex.X);
            Assert.AreEqual(0.2f, tex.Y);
        }

        [Test]
        public void read()
        {
            MemoryStream stream = new(expected, false);
            using BinaryReader reader = new(stream);
            Vector2 tex = reader.ReadVector2();
            Assert.AreEqual(-345.0f, tex.X);
            Assert.AreEqual(678.0f, tex.Y);
        }

        [Test]
        public void write()
        {
            MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            Vector2 tex = new(-345.0f, 678.0f);
            writer.Write(tex);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
