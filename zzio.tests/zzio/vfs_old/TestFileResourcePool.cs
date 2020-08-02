using System;
using System.IO;
using System.Text;
using System.Linq;
using NUnit.Framework;
using zzio.utils;
using zzio.vfs;

namespace zzio.tests.vfs_old
{
    [TestFixture]
    public class TestFileResourcePool
    {
        private readonly static FilePath baseDir =
            new FilePath(TestContext.CurrentContext.TestDirectory)
            .Combine("../../../resources/vfs");
        private readonly static FileResourcePool_OLD pool =
            new FileResourcePool_OLD(baseDir.ToPOSIXString());

        [Test]
        public void getresourcetype()
        {
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("answer.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("hello.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("a/b/content.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("a/c/content.txt"));

            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType(""));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a/b"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("a/c/"));

            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("nope"));
            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("a/d/content.txt"));
        }

        private void testStream(string expected, Stream stream)
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] actualBytes = new byte[expectedBytes.Length];
            Assert.NotNull(stream);
            Assert.AreEqual(actualBytes.Length, stream.Read(actualBytes, 0, actualBytes.Length));
            Assert.AreEqual(expectedBytes, actualBytes);
            Assert.AreEqual(-1, stream.ReadByte());
        }

        [Test]
        public void getfilecontent()
        {
            testStream("Hello World!", pool.GetFileContent("hello.txt"));
            testStream("42", pool.GetFileContent("answer.txt"));
            testStream("1337", pool.GetFileContent("a/b/content.txt"));
            testStream("Zanzarah", pool.GetFileContent("a/c/content.txt"));

            Assert.Null(pool.GetFileContent("nopenope"));
            Assert.Null(pool.GetFileContent("a"));
            Assert.Null(pool.GetFileContent("a/b"));
        }

        private void containsAll(string[] expected, string[] actual)
        {
            StringComparer comparer = StringComparer.InvariantCultureIgnoreCase;
            Assert.AreEqual(expected.Length, actual.Length);
            Assert.True(
                expected.All(expectedFile => actual.Contains(expectedFile, comparer))
            );
        }

        [Test]
        public void getdircontent()
        {
            containsAll(new string[]
            {
                "answer.txt",
                "hello.txt",
                "a"
            }, pool.GetDirectoryContent(""));

            containsAll(new string[]
            {
                "b",
                "c"
            }, pool.GetDirectoryContent("a"));

            containsAll(new string[] { "content.txt" }, pool.GetDirectoryContent("a/b"));
            containsAll(new string[] { "content.txt" }, pool.GetDirectoryContent("a/c"));

            Assert.AreEqual(new string[0], pool.GetDirectoryContent("nopenope"));
            Assert.AreEqual(new string[0], pool.GetDirectoryContent("answer.txt"));
            Assert.AreEqual(new string[0], pool.GetDirectoryContent("a/d"));
        }
    }
}
