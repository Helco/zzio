using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public partial class AssetRegistry
{
    CancellationToken IAssetRegistryInternal.Cancellation => cancellationSource.Token;
    bool IAssetRegistryInternal.IsLocalRegistry => apparentRegistry != this;

    void IAssetRegistryInternal.DisposeHandle(AssetHandle handle)
    {
        if (handle.Registry != this)
            throw new ArgumentException("Tried to unload asset at wrong registry");
        if (WasDisposed)
            return; // not particularly nice, but all assets should be disposed anyway so ignoring handle disposal should be fine
            
        IAsset? asset = null;
        lock (assets)
            asset = assets.GetValueOrDefault(handle.AssetID);
        if (asset is null)
            throw new InvalidOperationException("Asset was not found or already deleted, this should not happen if you used a valid handle");
        asset.DelRef();
    }

    private IAsset? TryGetForApplying(AssetHandle handle)
    {
        lock (assets)
            return assets.GetValueOrDefault(handle.AssetID);
    }

    unsafe void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        if (applyFnptr is null)
            return;

        var asset = TryGetForApplying(handle);
        if (asset is not null)
            LoadApply(asset, new FnPtrApplyAdapter<TApplyContext>(applyFnptr, applyContext));
    }

    void IAssetRegistryInternal.AddApplyAction(AssetHandle handle,
        Action<AssetHandle> applyAction)
    {
        if (applyAction is null)
            return;

        var asset = TryGetForApplying(handle);
        if (asset is not null)
            LoadApply(asset, new ActionApplyAdapter(applyAction));
    }

    void IAssetRegistryInternal.QueueRemoveAsset(IAsset asset)
    {
        stats.OnAssetRemoved();
        if (IsMainThread)
            RemoveAsset(asset);
        else
            assetsToRemove.Writer.TryWrite(asset);
    }

    void IAssetRegistryInternal.QueueApplyAsset(IAsset asset)
    {
        stats.OnAssetLoaded();
        if (IsMainThread)
            ApplyAsset(asset);
        else
            assetsToApply.Writer.TryWrite(asset);
    }

    Task IAssetRegistry.WaitAsyncAll(AssetHandle[] secondaryHandles)
    {
        // Task.WhenAll(IEnumerable) would create a List anyway. So we can use it
        // to prefilter, which we have to do anyways
        var secondaryTasks = new List<Task>(secondaryHandles.Length);
        lock (assets)
        {
            foreach (var handle in secondaryHandles)
            {
                handle.CheckDisposed();
                if (!assets.TryGetValue(handle.AssetID, out var asset))
                    continue;
                try
                {
                    asset.ThrowIfError();
                    if (asset.State == AssetState.Loaded)
                        continue;
                    secondaryTasks.Add(asset.LoadTask.Completion.AsTask());
                }
                catch(Exception e) // if the asset was already erroneous
                {
                    throw new AggregateException([e]);
                }
            }
            return Task.WhenAll(secondaryTasks).WithAggregateException();
        }
    }

    bool IAssetRegistryInternal.IsLoaded(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        return asset is IAsset { State: AssetState.Loaded };
        // no use locking the asset. If there is non-synchronized access it could just as well change before 
        // the caller decides on their action based on our return value.
    }

    TAsset IAssetRegistryInternal.GetLoadedAsset<TAsset>(Guid assetId)
    {
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset tasset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");

        asset.ThrowIfError();
        if (asset.State is AssetState.Loaded)
            return tasset;

        // We ended up having to (potentially start and) wait for loading.
        // Let's use the normal synchronous load mechanism for that
        Load(asset, AssetLoadPriority.Synchronous, applyAction: null, addRef: false);
        Debug.Assert(asset.State == AssetState.Loaded);
        return tasset;
    }

    ValueTask<TAsset> IAssetRegistryInternal.GetLoadedAssetAsync<TAsset>(Guid assetId)
    {
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset tasset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");

        asset.ThrowIfError();
        if (asset.State is AssetState.Loaded)
            return ValueTask.FromResult(tasset);
            
        // We still use the normal loading mechanism but asynchronously and wait ourselves
        Load(asset, AssetLoadPriority.High, applyAction: null, addRef: false);
        return new(CompleteHighAndReturn(tasset));

        static async Task<TAsset> CompleteHighAndReturn(TAsset asset)
        {
            await asset.Complete(AssetLoadPriority.High);
            asset.ThrowIfError();
            return asset;
        }
    }

    void IAssetRegistryInternal.AddRefOf(Guid assetId)
    {
        IAsset? asset = null;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset is null)
            throw new InvalidOperationException("Asset was not found or already deleted, this should not happen if you used a valid handle");

        asset.ThrowIfError();
        asset.AddRef();
    }
}
