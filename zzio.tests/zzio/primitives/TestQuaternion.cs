using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace zzio.tests.primitives;

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
    public void ctor()
    {
        Quaternion quat = new(0.1f, 0.2f, 0.3f, 0.4f);
        Assert.That(quat.X, Is.EqualTo(0.1f));
        Assert.That(quat.Y, Is.EqualTo(0.2f));
        Assert.That(quat.Z, Is.EqualTo(0.3f));
        Assert.That(quat.W, Is.EqualTo(0.4f));
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        Quaternion quat = reader.ReadQuaternion();
        Assert.That(quat.X, Is.EqualTo(-345.0f));
        Assert.That(quat.Y, Is.EqualTo(678.0f));
        Assert.That(quat.Z, Is.EqualTo(23.8f));
        Assert.That(quat.W, Is.EqualTo(89.12f));
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        Quaternion quat = new(-345.0f, 678.0f, 23.8f, 89.12f);
        writer.Write(quat);

        byte[] actual = stream.ToArray();
        Assert.That(expected, Is.EqualTo(actual));
    }
}
