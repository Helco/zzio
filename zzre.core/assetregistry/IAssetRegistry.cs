using System;
using System.Threading.Tasks;
using System.Threading;
namespace zzre;

public enum AssetLoadPriority
{
    Synchronous,
    High,
    Low
}

public interface IAssetRegistry : IDisposable
{
    internal IAssetRegistryInternal InternalRegistry { get; }
    ITagContainer DIContainer { get; }
    AssetRegistryStats Stats { get; }

    unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>;

    AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction = null)
        where TInfo : IEquatable<TInfo>;

    void Unload(AssetHandle handle);

    void ApplyAssets();
}

internal interface IAssetRegistryInternal : IAssetRegistry
{
    IAssetRegistryInternal IAssetRegistry.InternalRegistry => this;

    bool IsLocalRegistry { get; }
    CancellationToken Cancellation { get; }

    ValueTask QueueRemoveAsset(IAsset asset);
    ValueTask QueueApplyAsset(IAsset asset);
    Task WaitAsyncAll(AssetHandle[] assets);
    bool IsLoaded(Guid assetId);
    TAsset GetLoadedAsset<TAsset>(Guid assetId);
}
