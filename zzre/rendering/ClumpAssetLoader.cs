using System;
using System.Diagnostics.CodeAnalysis;
using zzio.vfs;

namespace zzre.rendering;

public class ClumpAssetLoader : IAssetLoader<ClumpMesh>
{
    public ITagContainer DIContainer { get; }

    public ClumpAssetLoader(ITagContainer diContainer)
    {
        DIContainer = diContainer;
    }

    public void Clear() { }

    public bool TryLoad(IResource resource, [NotNullWhen(true)] out ClumpMesh? asset)
    {
        try
        {
            asset = new ClumpMesh(DIContainer, resource);
            return true;
        }
        catch (Exception)
        {
            asset = null;
            return false;
        }
    }
}

public class CachedClumpMeshLoader : CachedAssetLoader<ClumpMesh>
{
    public CachedClumpMeshLoader(ITagContainer diContainer) : base(new ClumpAssetLoader(diContainer)) { }
}
