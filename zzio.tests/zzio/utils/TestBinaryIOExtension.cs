using System.IO;
using NUnit.Framework;

namespace zzio.tests.utils
{
    [TestFixture]
    public class TextBinaryIOExtension
    {
        private readonly byte[] expectedZString = new byte[]
        {
            12, 0, 0, 0,
            (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)' ',
            (byte)'W', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'!'
        };

        private readonly byte[] testCString = new byte[]
        {
            (byte)'T', (byte)'e', (byte)'s', (byte)'t', 0,
            (byte)'O', (byte)'h', (byte)' ', (byte)'n', (byte)'o', 0
        };

        private readonly byte[] expectedCString = new byte[]
        {
            (byte)'c', (byte)'s', (byte)'t', (byte)'r', (byte)'i', (byte)'n', (byte)'g', 0
        };

        [Test]
        public void ReadZString()
        {
            MemoryStream stream = new MemoryStream(expectedZString, false);
            using BinaryReader reader = new BinaryReader(stream);
            Assert.AreEqual("Hello World!", reader.ReadZString());
        }

        [Test]
        public void WriteZString()
        {
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString("Hello World!");
            Assert.AreEqual(expectedZString, stream.ToArray());
        }

        [Test]
        public void ReadSizedString()
        {
            MemoryStream stream = new MemoryStream(testCString, false);
            using BinaryReader reader = new BinaryReader(stream);
            Assert.AreEqual("TestOh no", reader.ReadSizedString((int)stream.Length));
        }

        [Test]
        public void ReadSizedCString()
        {
            MemoryStream stream = new MemoryStream(testCString, false);
            using BinaryReader reader = new BinaryReader(stream);
            Assert.AreEqual("Test", reader.ReadSizedCString((int)stream.Length));
            Assert.AreEqual(stream.Position, stream.Length);
        }

        [Test]
        public void WriteSizedString()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteSizedCString("cstring", 8);
            Assert.AreEqual(expectedCString, stream.ToArray());

            stream = new MemoryStream();
            writer = new BinaryWriter(stream);
            writer.WriteSizedCString("cstrings are the best", 8);
            Assert.AreEqual(expectedCString, stream.ToArray());
        }
    }
}
