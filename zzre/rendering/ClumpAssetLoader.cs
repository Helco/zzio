using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using zzio.vfs;

namespace zzre.rendering
{
    public class ClumpAssetLoader : IAssetLoader<ClumpBuffers>
    {
        public ITagContainer DIContainer { get; }

        public ClumpAssetLoader(ITagContainer diContainer)
        {
            DIContainer = diContainer;
        }

        public void Clear() { }

        public bool TryLoad(IResource resource, [NotNullWhen(true)] out ClumpBuffers? asset)
        {
            try
            {
                asset = new ClumpBuffers(DIContainer, resource);
                return true;
            }
            catch (Exception)
            {
                asset = null;
                return false;
            }
        }
    }

    public class CachedClumpAssetLoader : CachedAssetLoader<ClumpBuffers>
    {
        public CachedClumpAssetLoader(ITagContainer diContainer) : base(new ClumpAssetLoader(diContainer)) { }
    }
}
