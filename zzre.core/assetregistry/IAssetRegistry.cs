using System;
using System.Threading;
using DotNext.Threading;

namespace zzre;

public enum AssetPriority
{
    Synchronous,
    High,
    Low
}

public interface IAssetRegistry : IDisposable
{
    bool WasDisposed { get; }
    bool IsMainThread { get; }
    ITagContainer DIContainer { get; }
    IAssetRegistry? ParentRegistry { get; }
    bool IsLocalRegistry { get; }
    CancellationToken Cancellation { get; } // is triggered when registry is disposed

    AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>;

    void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset;

    bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        where TAsset : class, IAsset;

    void Update();
}

internal interface IAssetRegistryInternal : IAssetRegistry
{
    void AddRef(Guid assetId);
    void DelRef(Guid assetId);
    AsyncLazy<IDisposable> GetAsset(Guid assetId);
}
