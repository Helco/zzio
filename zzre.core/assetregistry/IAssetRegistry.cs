using System;
using System.Threading.Tasks;
using System.Threading;

namespace zzre;

/// <summary>Controls when an asset is actually loaded</summary>
public enum AssetLoadPriority
{
    /// <summary>The asset will be completly loaded before the <c>Load</c> method returns</summary>
    Synchronous,
    /// <summary>Loading will be started immediately but may finish asynchronously</summary>
    High,
    /// <summary>Loading will be started at a later point and will always finish asynchronously</summary>
    Low
}

/// <summary>A <see cref="IAssetRegistry"/> facilitates loading, retrieval and disposal of assets</summary>
/// <remarks>This interface may point to a local or a global registry</remarks>
public interface IAssetRegistry : IDisposable
{
    internal IAssetRegistryInternal InternalRegistry { get; }
    /// <summary>The <see cref="ITagContainer"/> given to this registry at construction to be used for loading the asset contents</summary>
    ITagContainer DIContainer { get; }
    /// <summary>Basic statistics of the registry, containing mostly monotonous counters</summary>
    /// <remarks>This property will include the statistics of parent registries</remarks>
    AssetRegistryStats Stats { get; }
    /// <summary>Basic statistics of the registry, containing mostly monotonous counters</summary>
    /// <remarks>This property will not include statistics of parent registries</remarks>
    AssetRegistryStats LocalStats => Stats;

    /// <summary>Registers an asset for loading or returns a handle to a previously-registered asset</summary>
    /// <remarks>Depending on whether the asset is already loaded the apply action will be called immediately or only stored for later execution</remarks>
    /// <typeparam name="TInfo">The type that will determine the asset type</typeparam>
    /// <typeparam name="TApplyContext">The type of the apply context given to the apply action</typeparam>
    /// <param name="info">The value identifying the specific asset to load</param>
    /// <param name="priority">The load priority of the asset (ignored if already loaded)</param>
    /// <param name="applyFnptr">The function pointer to call as apply action</param>
    /// <param name="applyContext">The apply context given to the apply action</param>
    /// <returns>An untyped <see cref="AssetHandle"/> to the asset that controlles the lifetime of the asset instance</returns>
    unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>;

    /// <summary>Registers an asset for loading or returns a handle to a previously-registered asset</summary>
    /// <remarks>Depending on whether the asset is already loaded the apply action will be called immediately or only stored for later execution</remarks>
    /// <typeparam name="TInfo">The type that will determine the asset type</typeparam>
    /// <param name="info">The value identifying the specific asset to load</param>
    /// <param name="priority">The load priority of the asset (ignored if already loaded)</param>
    /// <param name="applyAction">The delegate to call as apply action</param>
    /// <returns>An untyped <see cref="AssetHandle"/> to the asset that controlles the lifetime of the asset instance</returns>
    AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction = null)
        where TInfo : IEquatable<TInfo>;

    /// <summary>Synchronously applies all outstanding removal or apply action of assets, as well as start loading of low-prioritised assets</summary>
    /// <remarks>This method is only to be called from the main thread</remarks>
    void ApplyAssets();

    /// <summary>Asynchronously wait for one or more assets to finish loading</summary>
    /// <remarks>Use this method to wait for secondary assets</remarks>
    /// <param name="assets">Handles to the asset to wait for</param>
    /// <returns>A task completing when all given assets finish loading</returns>
    Task WaitAsyncAll(AssetHandle[] assets) => InternalRegistry.WaitAsyncAll(assets);

    /// <summary>Asynchronously wait for one or more assets to finish loading</summary>
    /// <remarks>Use this method to wait for secondary assets</remarks>
    /// <param name="asset">Handles to the asset to wait for</param>
    /// <returns>A task completing when all given assets finish loading</returns>
    Task WaitAsyncAll(AssetHandle asset) => InternalRegistry.WaitAsyncAll([asset]);
}

internal interface IAssetRegistryInternal : IAssetRegistry
{
    IAssetRegistryInternal IAssetRegistry.InternalRegistry => this;

    bool IsLocalRegistry { get; }
    CancellationToken Cancellation { get; }

    unsafe void AddApplyAction<TApplyContext>(AssetHandle asset,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext);

    void AddApplyAction(AssetHandle asset,
        Action<AssetHandle> applyAction);

    void DisposeHandle(AssetHandle handle);
    void QueueRemoveAsset(IAsset asset);
    void QueueApplyAsset(IAsset asset);
    bool IsLoaded(Guid assetId);
    TAsset GetLoadedAsset<TAsset>(Guid assetId) where TAsset : IAsset;
    ValueTask<TAsset> GetLoadedAssetAsync<TAsset>(Guid assetId) where TAsset : IAsset;
}
