using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using zzio;
using zzio.vfs;

namespace zzre.rendering;

public interface IAssetLoader<TAsset> where TAsset : class, IDisposable
{
    ITagContainer DIContainer { get; }

    bool TryLoad(IResource resource, [NotNullWhen(true)] out TAsset? asset);
    void Clear();

    TAsset Load(IResource resource)
    {
        if (!TryLoad(resource, out var asset))
            throw new InvalidDataException($"Could not load {typeof(TAsset).Name} from \"{resource.Path.ToPOSIXString()}\"");
        return asset;
    }

    TAsset Load(FilePath path)
    {
        var resource = DIContainer.GetTag<IResourcePool>().FindFile(path) ?? throw new FileNotFoundException($"Could not find asste at \"{path.ToPOSIXString()}\"");
        return Load(resource);
    }
}

public interface IAssetLoaderValidation<TAsset> where TAsset : class, IDisposable
{
    void ValidateAsset(TAsset asset);
}
