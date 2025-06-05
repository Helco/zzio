using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public enum AssetLocality
{
    Global,
    Local,
    Unique
}

public interface IAssetLoader
{
    AssetLocality Locality { get; }
    Type InfoType { get; }
    Type AssetType { get; }
    IAssetRegistry Registry { get; }
}

public readonly record struct AssetLoadResult<TAsset>(
    TAsset Asset,
    IReadOnlyList<IAssetHandle> SecondaryAssets
) where TAsset : class, IDisposable;

public interface IAssetLoader<TInfo, TAsset> : IAssetLoader
    where TInfo : struct, IEquatable<TInfo>
    where TAsset : class, IDisposable
{
    Type IAssetLoader.InfoType => typeof(TInfo);
    Type IAssetLoader.AssetType => typeof(TAsset);

    Guid InfoToAssetId(in TInfo info);
    Task<AssetLoadResult<TAsset>> Load(Guid AssetId, in TInfo info, CancellationToken ct);
}

