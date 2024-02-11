using System.IO;
using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestTriangle
{
    private static readonly byte[] expected = new byte[] {
        0x12, 0x34, // 13330
        0x56, 0x78, // 30806
        0x9a, 0xbc, // 48282
        0xde, 0xf0  // 61662
    };

    [Test]
    public void ctor()
    {
        VertexTriangle tri = new(1, 2, 3, 4);
        Assert.That(tri.v1, Is.EqualTo(1));
        Assert.That(tri.v2, Is.EqualTo(2));
        Assert.That(tri.v3, Is.EqualTo(3));
        Assert.That(tri.m, Is.EqualTo(4));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        VertexTriangle tri = VertexTriangle.ReadNew(reader);
        Assert.That(tri.v1, Is.EqualTo(30806));
        Assert.That(tri.v2, Is.EqualTo(48282));
        Assert.That(tri.v3, Is.EqualTo(61662));
        Assert.That(tri.m, Is.EqualTo(13330));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        VertexTriangle tri = new(30806, 48282, 61662, 13330);
        tri.Write(writer);

        byte[] actual = stream.ToArray();
        Assert.That(expected, Is.EqualTo(actual));
    }
}
