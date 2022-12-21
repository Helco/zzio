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
        Assert.AreEqual(0.1f, quat.X);
        Assert.AreEqual(0.2f, quat.Y);
        Assert.AreEqual(0.3f, quat.Z);
        Assert.AreEqual(0.4f, quat.W);
    }

    [Test]
    public void read()
    {
        MemoryStream stream = new(expected, false);
        using BinaryReader reader = new(stream);
        Quaternion quat = reader.ReadQuaternion();
        Assert.AreEqual(-345.0f, quat.X);
        Assert.AreEqual(678.0f, quat.Y);
        Assert.AreEqual(23.8f, quat.Z);
        Assert.AreEqual(89.12f, quat.W);
    }

    [Test]
    public void write()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        Quaternion quat = new(-345.0f, 678.0f, 23.8f, 89.12f);
        writer.Write(quat);

        byte[] actual = stream.ToArray();
        Assert.AreEqual(actual, expected);
    }
}
