using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives {
    [TestFixture]
    public class TestQuaternion
    {
        private static readonly byte[] expected = new byte[] {
            0x00, 0x80, 0xac, 0xc3, // -345.0f
            0x00, 0x80, 0x29, 0x44, // +678.0f
            0x66, 0x66, 0xbe, 0x41, // +23.8f
            0x71, 0x3d, 0xb2, 0x42, // 89.12f
        };

        [Test]
        public void ctor() {
            Quaternion quat = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.AreEqual(0.1f, quat.x);
            Assert.AreEqual(0.2f, quat.y);
            Assert.AreEqual(0.3f, quat.z);
            Assert.AreEqual(0.4f, quat.w);
        }

        [Test]
        public void read() {
            MemoryStream stream = new MemoryStream(expected, false);
            BinaryReader reader = new BinaryReader(stream);
            Quaternion quat = Quaternion.read(reader);
            Assert.AreEqual(-345.0f, quat.x);
            Assert.AreEqual(678.0f, quat.y);
            Assert.AreEqual(23.8f, quat.z);
            Assert.AreEqual(89.12f, quat.w);
        }

        [Test]
        public void write() {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            Quaternion quat = new Quaternion(-345.0f, 678.0f, 23.8f, 89.12f);
            quat.write(writer);

            byte[] actual = stream.ToArray();
            Assert.AreEqual(actual, expected);
        }
    }
}
