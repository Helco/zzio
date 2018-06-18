using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives {
    [TestFixture]
    public class TestFColor
    {
        private static readonly byte[] expected = new byte[] {
            0xcd, 0xcc, 0xcc, 0x3d, // 0.1f
            0x9a, 0x99, 0x99, 0x3e, // 0.3f
            0x33, 0x33, 0x33, 0x3f, // 0.7f
            0x00, 0x00, 0x80, 0x3f, // 1.0f
        };

        [Test]
        public void ctor() {
            FColor color = new FColor(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.AreEqual(0.1f, color.r);
            Assert.AreEqual(0.2f, color.g);
            Assert.AreEqual(0.3f, color.b);
            Assert.AreEqual(0.4f, color.a);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            BinaryReader reader = new BinaryReader(stream);
            FColor color = FColor.read(reader);
            Assert.AreEqual(0.1f, color.r);
            Assert.AreEqual(0.3f, color.g);
            Assert.AreEqual(0.7f, color.b);
            Assert.AreEqual(1.0f, color.a);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            FColor color = new FColor(0.1f, 0.3f, 0.7f, 1.0f);
            color.write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
