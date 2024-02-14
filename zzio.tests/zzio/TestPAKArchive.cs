using System;
using System.Text;
using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestPAKArchive
{
    private readonly byte[] sampleData = File.ReadAllBytes(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/archive_sample2.pak")
    );

    [Test]
    public void containsfile()
    {
        MemoryStream stream = new(sampleData, false);
        PAKArchive archive = PAKArchive.ReadNew(stream);

        Assert.That(archive.ContainsFile("A/a.txt"), Is.True);
        Assert.That(archive.ContainsFile("a/B.txt"), Is.True);
        Assert.That(archive.ContainsFile("a/c/d.txt"), Is.True);
        Assert.That(archive.ContainsFile("a/e/f/g.txt"), Is.True);
        Assert.That(archive.ContainsFile("a/A.tXT"), Is.True);
        Assert.That(archive.ContainsFile("a/../a/./\\..\\A\\a.txt"), Is.True);

        Assert.That(archive.ContainsFile("a.txt"), Is.False);
        Assert.That(archive.ContainsFile(".."), Is.False);
        Assert.That(archive.ContainsFile("../A/"), Is.False);
        Assert.That(archive.ContainsFile("../a/Z.txt"), Is.False);
    }

    private static void testStream(Stream stream, string expected)
    {
        byte[] expectedBuffer = Encoding.UTF8.GetBytes(expected);
        byte[] actualBuffer = new byte[expectedBuffer.Length];
        Assert.That(stream.Position, Is.EqualTo(0));
        Assert.That(stream.Read(actualBuffer, 0, expectedBuffer.Length), Is.EqualTo(expectedBuffer.Length));
        Assert.That(stream.ReadByte(), Is.EqualTo(-1));
        Assert.That(actualBuffer, Is.EqualTo(expectedBuffer));
        stream.Close();
    }

    [Test]
    public void readfile()
    {
        MemoryStream stream = new(sampleData, false);
        PAKArchive archive = PAKArchive.ReadNew(stream);

        Assert.That(() => archive.ReadFile("../B/C/D.txt"), Throws.Exception);
        Assert.That(() => archive.ReadFile("../a/"), Throws.Exception);

        testStream(archive.ReadFile("a/A.txt"), "This is file a");
        testStream(archive.ReadFile("a/B.txt"), "This is file b");
        testStream(archive.ReadFile("a/c/d.txt"), "Hello World");
        testStream(archive.ReadFile("a/e/f/g.txt"), "Zanzarah");
    }

    [Test]
    public void getdirectorycontent()
    {
        MemoryStream stream = new(sampleData, false);
        PAKArchive archive = PAKArchive.ReadNew(stream);

        Assert.That(archive.GetDirectoryContent("G/H/I"), Is.EqualTo(Array.Empty<string>()));

        Assert.That(archive.GetDirectoryContent("", true), Is.EquivalentTo(new string[]
        {
            "a", "a/c", "a/e/f", "a/e",
            "A/a.txt", "a/B.txt", "a/c/d.txt", "a/e/f/g.txt",
        }));

        Assert.That(archive.GetDirectoryContent("A", true), Is.EquivalentTo(new string[]
        {
            "a.txt", "B.txt", "c/d.txt", "e/f/g.txt",
            "c", "e/f", "e"
        }));

        Assert.That(archive.GetDirectoryContent("a/c/", true), Is.EquivalentTo(new string[]
        {
            "d.txt"
        }));

        Assert.That(archive.GetDirectoryContent("A/E", true), Is.EquivalentTo(new string[]
        {
            "f/g.txt", "f"
        }));

        Assert.That(archive.GetDirectoryContent("a", false), Is.EquivalentTo(new string[]
        {
            "a.txt", "B.txt", "c", "e"
        }));
    }
}
