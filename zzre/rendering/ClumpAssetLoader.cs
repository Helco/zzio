using System;
using System.Diagnostics.CodeAnalysis;
using zzio.vfs;

namespace zzre.rendering;

public class ClumpAssetLoader : IAssetLoader<ClumpBuffers>, IAssetLoader<ClumpMesh>
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

public class CachedClumpBuffersLoader : CachedAssetLoader<ClumpBuffers>
{
    public CachedClumpBuffersLoader(ITagContainer diContainer) : base(new ClumpAssetLoader(diContainer)) { }
}

public class CachedClumpMeshLoader : CachedAssetLoader<ClumpMesh>
{
    public CachedClumpMeshLoader(ITagContainer diContainer) : base(new ClumpAssetLoader(diContainer)) { }
}
