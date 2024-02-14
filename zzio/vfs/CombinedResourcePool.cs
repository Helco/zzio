using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zzio;

namespace zzio.vfs;

public class CombinedResourcePool : IResourcePool
{
    private readonly IResourcePool[] pools;
    private readonly Dictionary<FilePath, CombinedDirectory> knownDirectories = new();
    public IResource Root => GetDirectoryFor(null, new FilePath(""), pools.Reverse().Select(p => p.Root)); // reverse for easier overwrite behaviour

    public CombinedResourcePool(IResourcePool[] pools)
    {
        this.pools = pools.ToArray();
    }

    private CombinedDirectory GetDirectoryFor(IResource? parent, FilePath path, IEnumerable<IResource> sources)
    {
        lock (knownDirectories)
        {
            if (knownDirectories.TryGetValue(path, out var known))
                return known;

            var dir = new CombinedDirectory(this, parent, path, sources);
            knownDirectories.Add(path, dir);
            return dir;
        }
    }

    private sealed class CombinedDirectory : IResource
    {
        private readonly CombinedResourcePool pool;
        private readonly IResource[] sources;

        public ResourceType Type => ResourceType.Directory;
        public FilePath Path { get; }
        public IResourcePool Pool => pool;
        public IResource? Parent { get; }
        public Stream? OpenContent() => null;

        public CombinedDirectory(CombinedResourcePool pool, IResource? parent, FilePath path, IEnumerable<IResource> sources)
        {
            this.pool = pool;
            this.sources = sources.ToArray();
            Path = path;
            Parent = parent;
        }

        public IEnumerable<IResource> Files => sources
            .SelectMany(s => s.Files)
            .GroupBy(r => r.Name.ToLowerInvariant())
            .Select(group => new CombinedFile(this, group.First()));

        public IEnumerable<IResource> Directories => sources
            .SelectMany(s => s.Directories)
            .GroupBy(r => r.Name.ToLowerInvariant())
            .Select(group => pool.GetDirectoryFor(this, Path.Combine(group.Key), group));
    }

    // Combined file is still necessary so the parent reference is correct
    private sealed class CombinedFile : IResource
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
