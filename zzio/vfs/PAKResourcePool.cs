using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class PAKResourcePool : IResourcePool
    {
        private readonly PAKArchive archive;
        public IResource Root => new PAKDirectory(this, null, "");

        public PAKResourcePool(PAKArchive archive)
        {
            this.archive = archive;
        }

        private class PAKDirectory : IResource
        {
            private readonly PAKResourcePool pool;
            public ResourceType Type => ResourceType.Directory;
            public FilePath Path { get; }
            public IResourcePool Pool => pool;
            public IResource Parent { get; }
            public Stream? OpenContent() => null;

            public PAKDirectory(PAKResourcePool pool, IResource parent, string pathText) : this(pool, parent, new FilePath(pathText)) { }
            public PAKDirectory(PAKResourcePool pool, IResource parent, FilePath path)
            {
                this.pool = pool;
                Path = path;
                Parent = parent;
            }

            public IEnumerable<IResource> Files => pool.archive
                .GetFilesIn(Path.ToPOSIXString(), false)
                .Select(fileName => new PAKFile(pool, this, Path.Combine(fileName)));
            public IEnumerable<IResource> Directories => pool.archive
                .GetDirectoriesIn(Path.ToPOSIXString(), false)
                .Select(fileName => new PAKDirectory(pool, this, Path.Combine(fileName)));
        }

        private class PAKFile : IResource
        {
            private readonly PAKResourcePool pool;
            public ResourceType Type => ResourceType.File;
            public FilePath Path { get; }
            public IResourcePool Pool => pool;
            public IResource Parent { get; }
            public IEnumerator<IResource> GetEnumerator() => Enumerable.Empty<IResource>().GetEnumerator();

            public PAKFile(PAKResourcePool pool, IResource parent, FilePath path)
            {
                this.pool = pool;
                Path = path;
                Parent = parent;
            }

            public Stream OpenContent() => pool.archive.ReadFile(Path.ToPOSIXString());
        }
    }
}
