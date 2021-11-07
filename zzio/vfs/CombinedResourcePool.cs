using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class CombinedResourcePool : IResourcePool
    {
        private readonly IResourcePool[] pools;
        public IResource Root => new CombinedDirectory(this, null, "", pools.Reverse().Select(p => p.Root)); // reverse for easier overwrite behaviour

        public CombinedResourcePool(IResourcePool[] pools)
        {
            this.pools = pools.ToArray();
        }

        private class CombinedDirectory : IResource
        {
            private readonly IResource[] sources;
            public ResourceType Type => ResourceType.Directory;
            public FilePath Path { get; }
            public IResourcePool Pool { get; }
            public IResource? Parent { get; }
            public Stream? OpenContent() => null;

            public CombinedDirectory(IResourcePool pool, IResource? parent, string pathText, IEnumerable<IResource> sources) : this(pool, parent, new FilePath(pathText), sources) { }
            public CombinedDirectory(IResourcePool pool, IResource? parent, FilePath path, IEnumerable<IResource> sources)
            {
                this.sources = sources.ToArray();
                Path = path;
                Pool = pool;
                Parent = parent;
            }

            public IEnumerable<IResource> Files => sources
                .SelectMany(s => s.Files)
                .GroupBy(r => r.Name.ToLowerInvariant())
                .Select(group => new CombinedFile(this, group.First()));

            public IEnumerable<IResource> Directories => sources
                .SelectMany(s => s.Directories)
                .GroupBy(r => r.Name.ToLowerInvariant())
                .Select(group => new CombinedDirectory(Pool, this, Path.Combine(group.Key), group));
        }

        // Combined file is still necessary so the parent reference is correct
        private class CombinedFile : IResource
        {
            private readonly IResource source;
            public ResourceType Type => ResourceType.File;
            public FilePath Path => source.Path;
            public IResourcePool Pool => source.Pool;
            public IResource Parent { get; }
            public Stream? OpenContent() => source.OpenContent();
            public IEnumerator<IResource> GetEnumerator() => source.GetEnumerator();

            public CombinedFile(IResource parent, IResource source)
            {
                Parent = parent;
                this.source = source;
            }
        }
    }
}
