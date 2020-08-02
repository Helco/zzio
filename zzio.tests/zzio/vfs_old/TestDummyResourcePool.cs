using System;
using System.IO;
using NUnit.Framework;
using zzio.vfs;
using zzio.utils;

namespace zzio.tests.vfs_old
{
    [TestFixture]
    public class TestDummyResourcePool
    {
        private readonly FilePath basePath = 
            new FilePath(TestContext.CurrentContext.TestDirectory).Combine("../../../resources/vfs");

        private readonly DummyResourcePool pool;

        public TestDummyResourcePool()
        {
            pool = new DummyResourcePool(new string[] {
                "answer.txt",
                "hello.txt",
                "a/b/content.txt",
                "a/c/content.txt"
            }, new byte[] { 1, 2, 3, 4 });
        }

        [Test]
        public void getresourcetype()
        {
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("answer.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("hello.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("a/b/content.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("a/c/content.txt"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a/"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a/b/"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a/c/"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType(""));

            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("a.txt"));
            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("content.txt"));
            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("a/d/content.txt"));
            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("b"));
        }

        private void testStream(Stream s)
        {
            Assert.NotNull(s);
            Assert.True(s.CanRead);
            Assert.AreEqual(1, s.ReadByte());
            Assert.AreEqual(2, s.ReadByte());
            Assert.AreEqual(3, s.ReadByte());
            Assert.AreEqual(4, s.ReadByte());
            Assert.AreEqual(-1, s.ReadByte());
        }

        [Test]
        public void getfilecontent()
        {
            testStream(pool.GetFileContent("answer.txt"));
            testStream(pool.GetFileContent("hello.txt"));
            testStream(pool.GetFileContent("a/b/content.txt"));
            testStream(pool.GetFileContent("a/c/content.txt"));

            Assert.Null(pool.GetFileContent("a.txt"));
            Assert.Null(pool.GetFileContent("content.txt"));
            Assert.Null(pool.GetFileContent("a/d/content.txt"));
            Assert.Null(pool.GetFileContent("b"));
            Assert.Null(pool.GetFileContent("a"));
            Assert.Null(pool.GetFileContent("a/"));
        }

        [Test]
        public void getdirectorycontent()
        {
            Assert.AreEqual(
                new string [] { "answer.txt", "hello.txt", "a" },
                pool.GetDirectoryContent("")
            );
            Assert.AreEqual(
                new string [] { "content.txt" },
                pool.GetDirectoryContent("a/b/")
            );
            Assert.AreEqual(
                new string [] { "content.txt" },
                pool.GetDirectoryContent("a/c")
            );
            Assert.AreEqual(
                new string [] { "b", "c" },
                pool.GetDirectoryContent("a")
            );
            
            Assert.AreEqual(new string[0], pool.GetDirectoryContent("a/d"));
            Assert.AreEqual(new string[0], pool.GetDirectoryContent("answer.txt"));
        }
    }
}
