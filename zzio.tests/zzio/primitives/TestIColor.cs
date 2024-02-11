using System.IO;
using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestIColor
{
    private static readonly byte[] expected = new byte[] {
        0x12, 0x34, 0x56, 0x78
    };

    [Test]
    public void ctor()
    {
        IColor color = new(1, 2, 3, 4);
        Assert.That(color.r, Is.EqualTo(1));
        Assert.That(color.g, Is.EqualTo(2));
        Assert.That(color.b, Is.EqualTo(3));
        Assert.That(color.a, Is.EqualTo(4));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        IColor color = IColor.ReadNew(reader);
        Assert.That(color.r, Is.EqualTo(0x12));
        Assert.That(color.g, Is.EqualTo(0x34));
        Assert.That(color.b, Is.EqualTo(0x56));
        Assert.That(color.a, Is.EqualTo(0x78));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        IColor color = new(0x12, 0x34, 0x56, 0x78);
        color.Write(writer);

        byte[] actual = stream.ToArray();
        Assert.That(expected, Is.EqualTo(actual));
    }
}
