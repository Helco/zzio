using System;
using System.Text;
using System.IO;
using NUnit.Framework;
using zzio.vfs;
using zzio.utils;

namespace zzio.tests.vfs_old
{
    [TestFixture]
    public class TestPAKResourcePool
    {
        private readonly static FilePath baseDir =
            new FilePath(TestContext.CurrentContext.TestDirectory)
            .Combine("../../../resources/archive_sample2.pak");
        private readonly static PAKResourcePool_OLD pool =
            new PAKResourcePool_OLD(baseDir.ToPOSIXString() + "?a");

        [Test]
        public void getresourcetype()
        {
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("a.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("b.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("c/D.txt"));
            Assert.AreEqual(ResourceType_OLD.File, pool.GetResourceType("e/f/g.txt"));
            
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("."));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("c"));
            Assert.AreEqual(ResourceType_OLD.Directory, pool.GetResourceType("e/F/"));

            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("Z"));
            Assert.AreEqual(ResourceType_OLD.NonExistant, pool.GetResourceType("BLA/z.txt"));
        }
        
        private void testStream(string expected, Stream stream)
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] actualBytes = new byte[expectedBytes.Length];
            Assert.NotNull(stream);
            Assert.AreEqual(actualBytes.Length, stream.Read(actualBytes, 0, actualBytes.Length));
            Assert.AreEqual(expectedBytes, actualBytes);
            Assert.AreEqual(-1, stream.ReadByte());
            stream.Close();
        }

        [Test]
        public void getfilecontent()
        {
            testStream("This is file a", pool.GetFileContent("a.txt"));
            testStream("This is file b", pool.GetFileContent("B.txt"));
            testStream("Hello World", pool.GetFileContent("C/d.txt"));
            testStream("Zanzarah", pool.GetFileContent("e/f/g.txt"));

            Assert.Null(pool.GetFileContent("nopenope"));
            Assert.Null(pool.GetFileContent("a"));
            Assert.Null(pool.GetFileContent("e/f/bla.txt"));
        }

        [Test]
        public void getdircontent()
        {
            Assert.AreEqual(new string[]
            {
                "a.txt", "B.txt", "c", "e"
            }, pool.GetDirectoryContent("."));

            Assert.AreEqual(new string[]
            {
                "d.txt"
            }, pool.GetDirectoryContent("c"));

            Assert.AreEqual(new string[]
            {
                "g.txt"
            }, pool.GetDirectoryContent("e/f"));

            Assert.AreEqual(new string[0], pool.GetDirectoryContent("ABC"));
            Assert.AreEqual(new string[0], pool.GetDirectoryContent("e/H"));
        }
    }
}
