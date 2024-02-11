using System.IO;
using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestUID
{
    [Test]
    public void properties()
    {
        UID nullUid = new();
        UID uid = new(0xabcdef01);

        Assert.That(nullUid.raw, Is.EqualTo((uint)0));
        Assert.That(nullUid.Module, Is.EqualTo(0));
        Assert.That(nullUid.GetHashCode(), Is.EqualTo(nullUid.GetHashCode()));
        Assert.That(new UID().GetHashCode(), Is.EqualTo(nullUid.GetHashCode()));
        Assert.That(new UID(0).GetHashCode(), Is.EqualTo(nullUid.GetHashCode()));

        Assert.That(uid.raw, Is.EqualTo(0xabcdef01));
        Assert.That(uid.Module, Is.EqualTo(1));
        Assert.That(uid.GetHashCode(), Is.EqualTo(uid.GetHashCode()));
        Assert.That(new UID(0xabcdef01).GetHashCode(), Is.EqualTo(uid.GetHashCode()));
    }

    [Test]
    public void read()
    {
        byte[] buffer = new byte[] { 0xef, 0xbe, 0xad, 0xde };
        MemoryStream stream = new(buffer, false);
        using BinaryReader reader = new(stream);

        UID uid = UID.ReadNew(reader);
        Assert.That(uid.raw, Is.EqualTo(0xdeadbeef));
        Assert.That(uid.Module, Is.EqualTo(0xf));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        UID uid = new(0xdeadbeef);
        uid.Write(writer);
        Assert.That(stream.ToArray(), Is.EqualTo(new byte[] { 0xef, 0xbe, 0xad, 0xde }));
    }

    [Test]
    public void parse()
    {
        Assert.That(UID.Parse("DEADBEEF").raw, Is.EqualTo(0xdeadbeef));
        Assert.That(UID.Parse("affe").raw, Is.EqualTo(0x0000affe));
    }

    [Test]
    public void tostring()
    {
        Assert.That(new UID(0xdeadbeef).ToString(), Is.EqualTo("DEADBEEF"));
        Assert.That(new UID(0xaffe).ToString(), Is.EqualTo("0000AFFE"));
    }
}
