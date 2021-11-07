using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzio.utils;
using zzio.vfs;
using zzre.rendering;

namespace zzmaps
{
    internal class RefCachedAssetLoader<TAsset> : CachedAssetLoader<TAsset> where TAsset : class, IDisposable
    {
        private readonly Dictionary<FilePath, int> refCounts = new Dictionary<FilePath, int>();

        public RefCachedAssetLoader(IAssetLoader<TAsset> parent) : base(parent)
        {}

        private int RefCountFor(FilePath path) =>
            refCounts.TryGetValue(path, out var refCount) ? refCount : 0;

        public override void Clear()
        {
            lock (cache)
            {
                base.Clear();
            }
        }

        public void ClearUnused()
        {
            lock (cache)
            {
                var unusedPaths = refCounts
                    .Where(kv => kv.Value <= 0)
                    .Select(kv => kv.Key)
                    .ToArray();
                foreach (var path in unusedPaths)
                {
                    cache[path].Dispose();
                    cache.Remove(path);
                    refCounts.Remove(path);
                }
            }
        }

        public override bool TryLoad(IResource resource, [NotNullWhen(true)] out TAsset? asset)
        {
            lock (cache)
            {
                var result = base.TryLoad(resource, out asset);
                if (!result || asset == null)
                    return false;

                refCounts[resource.Path] = RefCountFor(resource.Path) + 1;
                return true;
            }
        }

        public void Release(IEnumerable<IResource> resources)
        {
            lock(cache)
            {
                foreach (var resource in resources)
                    refCounts[resource.Path] = RefCountFor(resource.Path) - 1;
            }
        }
    }
}
