using NUnit.Framework;
using System;
using System.Linq;
using zzio.vfs;

namespace zzio.tests.vfs
{
    [TestFixture]
    public class TestCommonResourcePool
    {
        public static IResourcePool[] testPools = PoolResources.AllResourcePools;

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
        public void rootIsDirectory([ValueSource(nameof(testPools))] IResourcePool pool)
        {
            Assert.AreEqual(ResourceType.Directory, pool.Root.Type);
        }

        [Test, Combinatorial]
        public void rootHasNoParent([ValueSource(nameof(testPools))] IResourcePool pool)
        {
            Assert.IsNull(pool.Root.Parent);
        }

        [Test, Combinatorial]
        public void rootHasEmptyPath([ValueSource(nameof(testPools))] IResourcePool pool)
        {
            Assert.AreEqual("", pool.Root.Path.ToString());
        }

        [Test, Combinatorial]
        public void poolIsAlwaysSet([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            Assert.IsNotNull(res.Pool);
        });

        [Test, Combinatorial]
        public void enumeratorVsSplit([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            MyAssert.ContainsExactly(res, res.Files.Concat(res.Directories));
        });

        [Test, Combinatorial]
        public void resourcesTypesInSplit([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            foreach (var file in res.Files)
                Assert.AreEqual(ResourceType.File, file.Type);
            foreach (var dir in res.Directories)
                Assert.AreEqual(ResourceType.Directory, dir.Type);
        });

        [Test, Combinatorial]
        public void parentIsSetCorrectly([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            foreach (var child in res)
                Assert.AreEqual(res, child.Parent);
        });

        [Test, Combinatorial]
        public void openContent([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            using var stream = res.OpenContent();
            if (res.Type == ResourceType.File)
            {
                Assert.IsNotNull(stream);
                Assert.IsTrue(stream!.CanRead);
            }
            else
                Assert.IsNull(stream);
        });

        [Test, Combinatorial]
        public void filesHaveNoChildren([ValueSource(nameof(testPools))] IResourcePool pool) => VisitResources(pool, res =>
        {
            if (res.Type == ResourceType.File)
            {
                Assert.IsEmpty(res);
            }
        });
    }
}
