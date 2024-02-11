using NUnit.Framework;
using zzio.tests.vfs;
using zzio.vfs;

namespace zzio.tests.zzio.vfs;

[TestFixture]
public class TestIResourceExtensions
{
    [Test]
    public void findAndOpen()
    {
        var pool = PoolResources.CombinedResourcePool;

        MyAssert.Equals("common from b", pool.FindAndOpen("COMMON/CONTENT.txt"));
        MyAssert.Equals("from b", pool.FindAndOpen("content.txt"));
        Assert.That(pool.FindAndOpen("common/c.txt"), Is.Null);
        Assert.That(pool.FindAndOpen("hello.txt"), Is.Null);
    }
}
