using System;
using System.Text;
using System.IO;
using NUnit.Framework;
using zzio;

namespace zzio.tests
{
    [TestFixture]
    public class TestPAKArchive
    {
        private readonly byte[] sampleData = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "resources/archive_sample2.pak")
        );

        [Test]
        public void containsfile()
        {
            MemoryStream stream = new MemoryStream(sampleData, false);
            PAKArchive archive = PAKArchive.ReadNew(stream);

            Assert.True(archive.ContainsFile("A/a.txt"));
            Assert.True(archive.ContainsFile("a/B.txt"));
            Assert.True(archive.ContainsFile("a/c/d.txt"));
            Assert.True(archive.ContainsFile("a/e/f/g.txt"));
            Assert.True(archive.ContainsFile("a/A.tXT"));
            Assert.True(archive.ContainsFile("a/../a/./\\..\\A\\a.txt"));
            
            Assert.False(archive.ContainsFile("a.txt"));
            Assert.False(archive.ContainsFile(".."));
            Assert.False(archive.ContainsFile("../A/"));
            Assert.False(archive.ContainsFile("../a/Z.txt"));
        }

        private void testStream(Stream stream, string expected)
        {
            byte[] expectedBuffer = Encoding.UTF8.GetBytes(expected);
            byte[] actualBuffer = new byte[expectedBuffer.Length];
            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(expectedBuffer.Length, stream.Read(actualBuffer, 0, expectedBuffer.Length));
            Assert.AreEqual(-1, stream.ReadByte());
            Assert.AreEqual(expectedBuffer, actualBuffer);
            stream.Close();
        }

        [Test]
        public void readfile()
        {
            MemoryStream stream = new MemoryStream(sampleData, false);
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
            MemoryStream stream = new MemoryStream(sampleData, false);
            PAKArchive archive = PAKArchive.ReadNew(stream);

            Assert.AreEqual(new string[0], archive.GetDirectoryContent("G/H/I"));

            // TODO: these tests should not rely on item order

            Assert.AreEqual(new string[]
            {
                "A/a.txt", "a/B.txt", "a/c/d.txt", "a/e/f/g.txt",
                "a", "a/c", "a/e/f", "a/e"
            }, archive.GetDirectoryContent("", true));

            Assert.AreEqual(new string[]
            {
                "a.txt", "B.txt", "c/d.txt", "e/f/g.txt",
                "c", "e/f", "e"
            }, archive.GetDirectoryContent("A", true));

            Assert.AreEqual(new string[]
            {
                "d.txt"
            }, archive.GetDirectoryContent("a/c/", true));

            Assert.AreEqual(new string[]
            {
                "f/g.txt", "f"
            }, archive.GetDirectoryContent("A/E", true));

            Assert.AreEqual(new string[]
            {
                "a.txt", "B.txt", "c", "e"
            }, archive.GetDirectoryContent("a", false));
        }
    }
}
