using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    IAssetRegistry? ParentRegistry { get; }
    bool IsLocalRegistry { get; }
    CancellationToken Cancellation { get; } // is triggered when registry is disposed

    AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>;

    void Update();
    Task WaitForAll(IEnumerable<IAssetHandle> assets, CancellationToken ct);
}

internal interface IAssetRegistryInternal : IAssetRegistry
{
    void AddRef(Guid assetId);
    void DelRef(Guid assetId);
    AsyncLazy<IDisposable> GetAsset(Guid assetId);
}
