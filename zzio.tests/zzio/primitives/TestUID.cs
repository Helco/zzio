using System;
using System.IO;
using NUnit.Framework;
using zzio.primitives;

namespace zzio.tests.primitives
{
    [TestFixture]
    public class TestUID
    {
        [Test]
        public void properties()
        {
            UID nullUid = new UID();
            UID uid = new UID(0xabcdef01);

            Assert.AreEqual((uint)0, nullUid.raw);
            Assert.AreEqual(0, nullUid.Module);
            Assert.AreEqual(nullUid.GetHashCode(), nullUid.GetHashCode());
            Assert.AreEqual(nullUid.GetHashCode(), new UID().GetHashCode());
            Assert.AreEqual(nullUid.GetHashCode(), new UID(0).GetHashCode());

            Assert.AreEqual(0xabcdef01, uid.raw);
            Assert.AreEqual(1, uid.Module);
            Assert.AreEqual(uid.GetHashCode(), uid.GetHashCode());
            Assert.AreEqual(uid.GetHashCode(), new UID(0xabcdef01).GetHashCode());
        }

        [Test]
        public void read()
        {
            byte[] buffer = new byte[] { 0xef, 0xbe, 0xad, 0xde };
            MemoryStream stream = new MemoryStream(buffer, false);
            using BinaryReader reader = new BinaryReader(stream);
            
            UID uid = UID.ReadNew(reader);
            Assert.AreEqual(0xdeadbeef, uid.raw);
            Assert.AreEqual(0xf, uid.Module);
        }

        [Test]
        public void write()
        {
            MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            UID uid = new UID(0xdeadbeef);
            uid.Write(writer);
            Assert.AreEqual(new byte[] { 0xef, 0xbe, 0xad, 0xde }, stream.ToArray());
        }

        [Test]
        public void parse()
        {
            Assert.AreEqual(0xdeadbeef, UID.Parse("DEADBEEF").raw);
            Assert.AreEqual(0x0000affe, UID.Parse("affe").raw);
        }

        [Test]
        public void tostring()
        {
            Assert.AreEqual("DEADBEEF", new UID(0xdeadbeef).ToString());
            Assert.AreEqual("0000AFFE", new UID(0xaffe).ToString());
        }
    }
}
