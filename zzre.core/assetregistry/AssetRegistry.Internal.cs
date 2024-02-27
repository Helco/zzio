using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

partial class AssetRegistry
{
    CancellationToken IAssetRegistryInternal.Cancellation => cancellationSource.Token;
    bool IAssetRegistryInternal.IsLocalRegistry => apparentRegistry != this;

    void IAssetRegistryInternal.DisposeHandle(AssetHandle handle)
    {
        if (handle.Registry != this)
            throw new ArgumentException("Tried to unload asset at wrong registry");
        lock (assets)
        {
            if (assets.TryGetValue(handle.AssetID, out var asset))
                asset.DelRef();
        }
    }

    private IAsset? TryGetForApplying(AssetHandle handle)
    {
        lock (assets)
        {
            var asset = assets.GetValueOrDefault(handle.AssetID);
            if (asset is not null)
            {
                asset.ThrowIfError();
                if (asset.State == AssetState.Disposed)
                    asset = null;
            }
            return asset;
        }
    }

    unsafe void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock (asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyFnptr(handle, in applyContext);
            else
            {
                asset.ApplyAction.Next += ConvertFnptr(applyFnptr, in applyContext);
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        IAssetRegistry.ApplyWithContextAction<TApplyContext> applyAction,
        in TApplyContext applyContext)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock (asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyAction(handle, in applyContext);
            else
            {
                var applyContextCopy = applyContext;
                asset.ApplyAction.Next += handle => applyAction(handle, in applyContextCopy);
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    void IAssetRegistryInternal.AddApplyAction(AssetHandle handle,
        Action<AssetHandle> applyAction)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock (asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyAction(handle);
            else
            {
                asset.ApplyAction.Next += applyAction;
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    ValueTask IAssetRegistryInternal.QueueRemoveAsset(IAsset asset)
    {
        stats.OnAssetRemoved();
        if (IsMainThread)
        {
            RemoveAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToRemove.Writer.WriteAsync(asset, Cancellation);
    }

    ValueTask IAssetRegistryInternal.QueueApplyAsset(IAsset asset)
    {
        stats.OnAssetLoaded();
        if (IsMainThread)
        {
            ApplyAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToApply.Writer.WriteAsync(asset, Cancellation);
    }

    Task IAssetRegistryInternal.WaitAsyncAll(AssetHandle[] secondaryHandles)
    {
        lock (assets)
        {
            foreach (var handle in secondaryHandles)
            {
                if (assets.TryGetValue(handle.AssetID, out var asset))
                    asset.StartLoading();
            }
            return Task.WhenAll(secondaryHandles.Select(h => assets[h.AssetID].LoadTask));
        }
    }

    bool IAssetRegistryInternal.IsLoaded(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            return false;
        lock (asset)
            return asset.State == AssetState.Loaded;
    }

    TAsset IAssetRegistryInternal.GetLoadedAsset<TAsset>(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");
        lock (asset)
        {
            asset.ThrowIfError();
            switch (asset.State)
            {
                case AssetState.Disposed:
                    throw new ObjectDisposedException(asset.ToString());
                default:
                    asset.Complete();
                    asset.ApplyAction.Invoke(new(this, assetId));
                    return (TAsset)asset;
            }
        }
    }
}
