using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio.vfs;

namespace zzio.tests.vfs
{
    [TestFixture]
    public class TestCombinedResourcePool
    {
        private readonly static IResourcePool pool = PoolResources.CombinedResourcePool;

        [Test]
        public void structure()
        {
            MyAssert.ContainsExactly(
                new[] { "content.txt" },
                pool.Root.Files.Select(f => f.Name));
            MyAssert.ContainsExactly(
                new[] { "a", "b", "common" },
                pool.Root.Directories.Select(d => d.Name));

            var a = pool.Root.Directories.First(d => d.Name == "a");
            MyAssert.ContainsExactly(
                new[] { "hello.txt" },
                a.Files.Select(f => f.Name));
            Assert.IsEmpty(a.Directories);

            var b = pool.Root.Directories.First(d => d.Name == "b");
            MyAssert.ContainsExactly(
                new[] { "hello.txt" },
                b.Files.Select(f => f.Name));
            Assert.IsEmpty(b.Directories);

            var common = pool.Root.Directories.First(d => d.Name == "common");
            MyAssert.ContainsExactly(
                new[] { "content.txt", "a.txt", "b.txt" },
                common.Files.Select(f => f.Name));
            Assert.IsEmpty(common.Directories);
        }

        [Test]
        public void content()
        {
            Stream? getFileContent(IResource dir, string file) => dir.Files
                .SingleOrDefault(f => f.Name == file)
                ?.OpenContent();
            var a = pool.Root.Directories.First(d => d.Name == "a");
            var b = pool.Root.Directories.First(d => d.Name == "b");
            var common = pool.Root.Directories.First(d => d.Name == "common");

            MyAssert.Equals("from b", getFileContent(pool.Root, "content.txt"));
            MyAssert.Equals("also from a", getFileContent(a, "hello.txt"));
            MyAssert.Equals("also from b", getFileContent(b, "hello.txt"));
            MyAssert.Equals("common from b", getFileContent(common, "content.txt"));
            MyAssert.Equals("common extra from a", getFileContent(common, "a.txt"));
            MyAssert.Equals("common extra from b", getFileContent(common, "b.txt"));
        }
    }
}
