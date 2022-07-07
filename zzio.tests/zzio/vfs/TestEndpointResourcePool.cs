using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using zzio.vfs;

namespace zzio.tests.vfs
{
    [TestFixture]
    public class TestEndpointResourcePool
    {
        public static IResourcePool[] testPools = PoolResources.EndpointResourcePools;

        private void VisitResources(IResourcePool pool, Action<IResource> action)
        {
            void visit(IResource res)
            {
                action(res);
                foreach (var child in res)
                    visit(child);
            }
            visit(pool.Root);
        }

        [Test, Combinatorial]
        public void endpointPoolIsSetCorrectly([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            Assert.AreSame(pool, res.Pool);
        });

        [Test, Combinatorial]
        public void structure([ValueSource(nameof(testPools))] IResourcePool pool)
        {
            MyAssert.ContainsExactly(
                new[] { "answer.txt", "hello.txt" },
                pool.Root.Files.Select(f => f.Path.ToPOSIXString()));
            MyAssert.ContainsExactly(
                new[] { "a" },
                pool.Root.Directories.Select(d => d.Path.ToPOSIXString()));

            var a = pool.Root.Directories.Single();
            MyAssert.ContainsExactly(
                Array.Empty<IResource>(),
                a.Files);
            MyAssert.ContainsExactly(
                new[] { "a/b", "a/c" },
                a.Directories.Select(d => d.Path.ToPOSIXString()));

            var b = a.Directories.Single(d => d.Path.Parts.Last() == "b");
            MyAssert.ContainsExactly(
                new[] { "a/b/content.txt" },
                b.Files.Select(f => f.Path.ToPOSIXString()));
            MyAssert.ContainsExactly(
                Array.Empty<IResource>(),
                b.Directories);

            var c = a.Directories.Single(d => d.Path.Parts.Last() == "c");
            MyAssert.ContainsExactly(
                new[] { "a/c/content.txt" },
                c.Files.Select(f => f.Path.ToPOSIXString()));
            MyAssert.ContainsExactly(
                Array.Empty<IResource>(),
                c.Directories);
        }

        [Test, Combinatorial]
        public void content([ValueSource(nameof(testPools))] IResourcePool pool)
        {
            Stream? getFileContent(IResource dir, string file) => dir.Files
                .SingleOrDefault(f => f.Path.Parts.Last() == file)
                ?.OpenContent();
            var a = pool.Root.Directories.Single();
            var b = a.Directories.Single(d => d.Path.Parts.Last() == "b");
            var c = a.Directories.Single(d => d.Path.Parts.Last() == "c");

            MyAssert.Equals("42", getFileContent(pool.Root, "answer.txt"));
            MyAssert.Equals("Hello World!", getFileContent(pool.Root, "hello.txt"));
            MyAssert.Equals("1337", getFileContent(b, "content.txt"));
            MyAssert.Equals("Zanzarah", getFileContent(c, "content.txt"));
        }
    }
}
