using NUnit.Framework;
using zzio.tests.vfs;
using zzio.vfs;

namespace zzio.tests.zzio.vfs
{
    [TestFixture]
    public class TestIResourceExtensions
    {
        [Test]
        public void findAndOpen()
        {
            var pool = PoolResources.CombinedResourcePool;

            MyAssert.Equals("common from b", pool.FindAndOpen("COMMON/CONTENT.txt"));
            MyAssert.Equals("from b", pool.FindAndOpen("content.txt"));
            Assert.IsNull(pool.FindAndOpen("common/c.txt"));
            Assert.IsNull(pool.FindAndOpen("hello.txt"));
        }
    }
}
