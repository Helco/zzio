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
        Assert.AreEqual(1, color.r);
        Assert.AreEqual(2, color.g);
        Assert.AreEqual(3, color.b);
        Assert.AreEqual(4, color.a);
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        IColor color = IColor.ReadNew(reader);
        Assert.AreEqual(0x12, color.r);
        Assert.AreEqual(0x34, color.g);
        Assert.AreEqual(0x56, color.b);
        Assert.AreEqual(0x78, color.a);
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        IColor color = new(0x12, 0x34, 0x56, 0x78);
        color.Write(writer);

        byte[] actual = stream.ToArray();
        Assert.AreEqual(actual, expected);
    }
}
