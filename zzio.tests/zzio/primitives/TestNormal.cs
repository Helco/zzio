using System.IO;
using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestNormal
{
    private static readonly byte M123 = unchecked((byte)-123);
    private static readonly byte[] expected = new byte[] {
        0x12, 0x34, 0x56, M123
    };

    [Test]
    public void ctor()
    {
        Normal norm = new(1, 2, 3, 4);
        Assert.That(norm.x, Is.EqualTo(1));
        Assert.That(norm.y, Is.EqualTo(2));
        Assert.That(norm.z, Is.EqualTo(3));
        Assert.That(norm.p, Is.EqualTo(4));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        Normal norm = Normal.ReadNew(reader);
        Assert.That(norm.x, Is.EqualTo(0x12));
        Assert.That(norm.y, Is.EqualTo(0x34));
        Assert.That(norm.z, Is.EqualTo(0x56));
        Assert.That(norm.p, Is.EqualTo(-123));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        Normal norm = new(0x12, 0x34, 0x56, -123);
        norm.Write(writer);

        byte[] actual = stream.ToArray();
        Assert.That(expected, Is.EqualTo(actual));
    }
}
