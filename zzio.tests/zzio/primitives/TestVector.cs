using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestVector3
{
    private static readonly byte[] expected = new byte[] {
        0x00, 0x80, 0xac, 0xc3, // -345.0f
        0x00, 0x80, 0x29, 0x44, // +678.0f
        0x66, 0x66, 0xbe, 0x41, // +23.8f
    };

    [Test]
    public void ctor()
    {
        Vector3 vec = new(0.1f, 0.2f, 0.3f);
        Assert.That(vec.X, Is.EqualTo(0.1f));
        Assert.That(vec.Y, Is.EqualTo(0.2f));
        Assert.That(vec.Z, Is.EqualTo(0.3f));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        Vector3 vec = reader.ReadVector3();
        Assert.That(vec.X, Is.EqualTo(-345.0f));
        Assert.That(vec.Y, Is.EqualTo(678.0f));
        Assert.That(vec.Z, Is.EqualTo(23.8f));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        Vector3 vec = new(-345.0f, 678.0f, 23.8f);
        writer.Write(vec);

        byte[] actual = stream.ToArray();
        Assert.That(expected, Is.EqualTo(actual));
    }
}
