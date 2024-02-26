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
    public delegate void ApplyWithContextAction<TApplyContext>(AssetHandle handle, in TApplyContext context);

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

    unsafe void AddApplyAction<TApplyContext>(AssetHandle asset,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext);

    void AddApplyAction<TApplyContext>(AssetHandle asset,
        ApplyWithContextAction<TApplyContext> applyAction,
        in TApplyContext applyContext);

    void AddApplyAction(AssetHandle asset,
        Action<AssetHandle> applyAction);

    ValueTask QueueRemoveAsset(IAsset asset);
    ValueTask QueueApplyAsset(IAsset asset);
    Task WaitAsyncAll(AssetHandle[] assets);
    bool IsLoaded(Guid assetId);
    TAsset GetLoadedAsset<TAsset>(Guid assetId);
}
