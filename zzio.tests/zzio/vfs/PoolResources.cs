using zzio.vfs;
using NUnit.Framework;
using System.IO;

namespace zzio.tests.vfs
{
    public static class PoolResources
    {
        private readonly static FilePath resourceDir = new FilePath(TestContext.CurrentContext.TestDirectory).Combine("resources/");

        public static IResourcePool[] AllResourcePools => new IResourcePool[]
        {
            FileResourcePool,
            InMemoryResourcePool,
            PAKArchiveResourcePool,
            PAKResourcePool,
            CombinedResourcePool
        };

        public static IResourcePool[] EndpointResourcePools => new IResourcePool[]
        {
            FileResourcePool,
            InMemoryResourcePool,
            PAKArchiveResourcePool,
            PAKResourcePool
        };

        public static FileResourcePool FileResourcePool => new FileResourcePool(resourceDir.Combine("vfs"));

        public static PAKResourcePool PAKResourcePool = new PAKResourcePool(
            new FileStream(resourceDir.Combine("archive_sample.pak").ToString(), FileMode.Open, FileAccess.Read));

#pragma warning disable CS0618 // Type or member is obsolete
        public static PAKArchiveResourcePool PAKArchiveResourcePool => new PAKArchiveResourcePool(PAKArchive.ReadNew(
            new FileStream(resourceDir.Combine("archive_sample.pak").ToString(), FileMode.Open, FileAccess.Read)));
#pragma warning restore CS0618 // Type or member is obsolete

        public static InMemoryResourcePool InMemoryResourcePool
        {
            get
            {
                // same structure as file resource pool
                var pool = new InMemoryResourcePool();
                pool.CreateFile("answer.txt", "42");
                pool.CreateFile("hello.txt", "Hello World!");
                pool.CreateFile("a/b/content.txt", "1337");
                pool.CreateFile("a/c/content.txt", "Zanzarah");
                return pool;
            }
        }

        public static CombinedResourcePool CombinedResourcePool
        {
            get
            {
                var aSource = new InMemoryResourcePool();
                aSource.CreateFile("content.txt", "from a");
                aSource.CreateFile("a/hello.txt", "also from a");
                aSource.CreateFile("COMMON/CONTENT.TXT", "common from a");
                aSource.CreateFile("COMMON/a.txt", "common extra from a");
                var bSource = new InMemoryResourcePool();
                bSource.CreateFile("content.txt", "from b");
                bSource.CreateFile("b/hello.txt", "also from b");
                bSource.CreateFile("common/content.txt", "common from b");
                bSource.CreateFile("common/b.txt", "common extra from b");

                return new CombinedResourcePool(new[] { aSource, bSource });
            }
        }
    }
}
