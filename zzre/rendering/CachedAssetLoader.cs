using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using zzio.utils;
using zzio.vfs;

namespace zzre.rendering
{
    public class CachedAssetLoader<TAsset> : BaseDisposable, IAssetLoader<TAsset> where TAsset : class, IDisposable
    {
        private readonly IAssetLoader<TAsset> parent;
        protected readonly Dictionary<FilePath, TAsset> cache = new Dictionary<FilePath, TAsset>();
        public ITagContainer DIContainer => parent.DIContainer;

        public CachedAssetLoader(IAssetLoader<TAsset> parent)
        {
            this.parent = parent;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Clear();
        }

        public virtual void Clear()
        {
            foreach (var asset in cache.Values)
                asset.Dispose();
            cache.Clear();
        }

        public virtual bool TryLoad(IResource resource, [NotNullWhen(true)] out TAsset? asset)
        {
            if (cache.TryGetValue(resource.Path, out asset))
                return true;
            if (parent.TryLoad(resource, out asset))
            {
                cache.Add(resource.Path, asset);
                return true;
            }
            return false;
        }
    }
}
