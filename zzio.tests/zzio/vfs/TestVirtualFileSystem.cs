using System;
using System.IO;
using NUnit.Framework;
using zzio.vfs;

namespace zzio.tests.vfs
{
    [TestFixture]
    public class TestVirtualFileSystem
    {
        private static readonly VirtualFileSystem uncached;
        private static readonly DummyResourcePool pool1;
        private static readonly DummyResourcePool pool2;
        private static readonly CachedVirtualFileSystem cached;

        public static VirtualFileSystem[] testSystems => 
            new VirtualFileSystem[] { uncached, cached };

        static TestVirtualFileSystem()
        {
            pool1 = new DummyResourcePool(new string[]
            {
                "content.txt",
                "a/d.txt",
                "c/a.txt",
                "d/e/f.txt",
                "d/e/g.txt",
                "d/h/i.txt"
            }, new byte[] { 1, 2, 3, 4 });

            pool2 = new DummyResourcePool(new string[]
            {
                "content.txt",
                "a/b.txt",
                "a/c.txt",
                "b/a.txt"
            }, new byte[] { 5, 6, 7, 8 });

            uncached = new VirtualFileSystem();
            uncached.AddResourcePool(pool1);
            uncached.AddResourcePool(pool2);

            cached = new CachedVirtualFileSystem();
            cached.AddResourcePool(pool1);
            cached.AddResourcePool(pool2);
        }

        [Test, Combinatorial]
        public void getresourcetype([ValueSource("testSystems")] VirtualFileSystem vfs)
        {
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("content.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("a/d.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("c/a.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("d/E\\f.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("d/e/sdf\\\\.././g.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("d/h/i.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("a/b.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("a/c.txt"));
            Assert.AreEqual(ResourceType.File, vfs.GetResourceType("b/a.txt"));

            Assert.AreEqual(ResourceType.Directory, vfs.GetResourceType("a/"));
            Assert.AreEqual(ResourceType.Directory, vfs.GetResourceType("d/e"));
            Assert.AreEqual(ResourceType.Directory, vfs.GetResourceType("b"));

            Assert.AreEqual(ResourceType.NonExistant, vfs.GetResourceType("asd"));
            Assert.AreEqual(ResourceType.NonExistant, vfs.GetResourceType("d/e/asldkj"));
        }

        private void testStream(byte[] expected, Stream stream)
        {
            byte[] actual = new byte[expected.Length];
            Assert.NotNull(stream);
            Assert.AreEqual(4, stream.Read(actual, 0, actual.Length));
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(-1, stream.ReadByte());
        }

        [Test, Combinatorial]
        public void getfilecontent([ValueSource("testSystems")] VirtualFileSystem vfs)
        {
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("a/d.txt"));
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("c/a.txt"));
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("d/e/f.txt"));
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("d/e/g.txt"));
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("d/h/i.txt"));

            testStream(new byte[] { 5, 6, 7, 8 }, vfs.GetFileContent("a/b.txt"));
            testStream(new byte[] { 5, 6, 7, 8 }, vfs.GetFileContent("a/c.txt"));
            testStream(new byte[] { 5, 6, 7, 8 }, vfs.GetFileContent("b/a.txt"));

            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent("content.txt"));
            testStream(new byte[] { 1, 2, 3, 4 }, vfs.GetFileContent(".//\\\\b/../a/./d.txt"));

            Assert.IsNull(vfs.GetFileContent("nope"));
            Assert.IsNull(vfs.GetFileContent("a/a.txt"));
            Assert.IsNull(vfs.GetFileContent("a/../b/../d/e/j.txt"));
        }

        [Test, Combinatorial]
        public void getdirectorycontent([ValueSource("testSystems")] VirtualFileSystem vfs)
        {
            string[] rootDirContent = new string[]
            {
                "content.txt",
                "a",
                "c",
                "d",
                "b"
            };
            Assert.AreEqual(rootDirContent, vfs.GetDirectoryContent(""));
            Assert.AreEqual(rootDirContent, vfs.GetDirectoryContent("."));
            Assert.AreEqual(rootDirContent, vfs.GetDirectoryContent("./"));
            Assert.AreEqual(rootDirContent, vfs.GetDirectoryContent(".\\"));
            Assert.AreEqual(rootDirContent, vfs.GetDirectoryContent("./b/../asdasd\\..\\\\///"));

            Assert.AreEqual(new string[]
            {
                "d.txt",
                "b.txt",
                "c.txt"
            }, vfs.GetDirectoryContent("a"));
            
            Assert.AreEqual(new string[] { "a.txt" }, vfs.GetDirectoryContent("c"));
            Assert.AreEqual(new string[] { "e", "h" }, vfs.GetDirectoryContent("d"));
            Assert.AreEqual(new string[] { "a.txt" }, vfs.GetDirectoryContent("b"));

            Assert.AreEqual(new string[0], vfs.GetDirectoryContent("content.txt"));
            Assert.AreEqual(new string[0], vfs.GetDirectoryContent("nopenopenope"));
            Assert.AreEqual(new string[0], vfs.GetDirectoryContent(".."));
            Assert.AreEqual(new string[0], vfs.GetDirectoryContent("a/b/../c//d"));
        }
    }
}
