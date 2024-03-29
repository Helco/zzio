using System.IO;
using NUnit.Framework;

namespace zzio.tests.utils;

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
        MemoryStream stream = new(expectedZString, false);
        using BinaryReader reader = new(stream);
        Assert.That(reader.ReadZString(), Is.EqualTo("Hello World!"));
    }

    [Test]
    public void WriteZString()
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.WriteZString("Hello World!");
        Assert.That(stream.ToArray(), Is.EqualTo(expectedZString));
    }

    [Test]
    public void ReadSizedString()
    {
        MemoryStream stream = new(testCString, false);
        using BinaryReader reader = new(stream);
        Assert.That(reader.ReadSizedString((int)stream.Length), Is.EqualTo("TestOh no"));
    }

    [Test]
    public void ReadSizedCString()
    {
        MemoryStream stream = new(testCString, false);
        using BinaryReader reader = new(stream);
        Assert.That(reader.ReadSizedCString((int)stream.Length), Is.EqualTo("Test"));
        Assert.That(stream.Length, Is.EqualTo(stream.Position));
    }

    [Test]
    public void WriteSizedString()
    {
        MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        writer.WriteSizedCString("cstring", 8);
        Assert.That(stream.ToArray(), Is.EqualTo(expectedCString));

        stream = new MemoryStream();
        writer = new BinaryWriter(stream);
        writer.WriteSizedCString("cstrings are the best", 8);
        Assert.That(stream.ToArray(), Is.EqualTo(expectedCString));
    }
}
